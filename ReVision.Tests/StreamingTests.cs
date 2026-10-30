using System.Net;
using System.Net.Sockets;
using ReVision.Dispatch;
using ReVision.Streamer;

namespace ReVision.Tests;

public sealed class StreamingTests
{
    private static EyeFrame Frame(byte value = 127) => new(ImageDataDispatcher.Eye.Left, 200, 200, Enumerable.Repeat(value, 40000).ToArray());

    [Fact]
    public void FrameRejectsInvalidDimensionsAndByteCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EyeFrame(ImageDataDispatcher.Eye.Left, int.MaxValue, 2, []));
        Assert.Throws<ArgumentException>(() => new EyeFrame(ImageDataDispatcher.Eye.Left, 200, 200, new byte[39999]));
    }

    [Fact]
    public void DispatcherUnsubscribesFaultsAndTracksFreshness()
    {
        using var frames = new ImageDataDispatcher();
        Assert.Null(frames.FrameAgeSeconds(ImageDataDispatcher.Eye.Left));
        using var failing = frames.Register(_ => throw new IOException("test failure"));
        int count = 0;
        using var subscription = frames.Register(_ => count++);
        frames.Dispatch(Frame());
        Assert.Equal(1, count);
        Assert.Equal(1, frames.SubscriberCount);
        Assert.InRange(frames.FrameAgeSeconds(ImageDataDispatcher.Eye.Left)!.Value, 0, 1);
        subscription.Dispose();
        frames.Dispatch(Frame());
        Assert.Equal(1, count);
        Assert.Equal(2, frames.LeftFrames);
        frames.Dispose();
        Assert.True(frames.Stopped.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => frames.Register(_ => { }));
    }

    [Fact]
    public async Task EncoderSharesFramesAndDropsSlowClientBacklog()
    {
        using var frames = new ImageDataDispatcher();
        await using var broadcast = new JpegBroadcast(frames, ImageDataDispatcher.Eye.Left, maxFps: 240);
        frames.Dispatch(Frame());
        await Task.Delay(30);
        Assert.Equal(0, broadcast.EncodedFrames);
        using var fast = broadcast.Subscribe();
        using var slow = broadcast.Subscribe();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        frames.Dispatch(Frame(40));
        byte[] first = await fast.Frames.ReadAsync(timeout.Token);
        Assert.Same(first, await slow.Frames.ReadAsync(timeout.Token));
        Assert.Equal(0xff, first[0]); Assert.Equal(0xd8, first[1]);
        Assert.Equal(0xff, first[^2]); Assert.Equal(0xd9, first[^1]);
        // Baseline SOF0: 8-bit precision, height=200, width=200.
        Assert.Contains("FFC000110800C800C8", Convert.ToHexString(first));
        frames.Dispatch(Frame(70));
        await fast.Frames.ReadAsync(timeout.Token);
        frames.Dispatch(Frame(90));
        byte[] latest = await fast.Frames.ReadAsync(timeout.Token);
        Assert.Same(latest, await slow.Frames.ReadAsync(timeout.Token));
        Assert.False(slow.Frames.TryRead(out _));
    }

    [Fact]
    public async Task EncoderLimitsClientsAndCompletesOnDeviceRemoval()
    {
        using var frames = new ImageDataDispatcher();
        await using var broadcast = new JpegBroadcast(frames, ImageDataDispatcher.Eye.Left);
        var clients = Enumerable.Range(0, 8).Select(_ => broadcast.Subscribe()).ToArray();
        try
        {
            Assert.Throws<InvalidOperationException>(() => broadcast.Subscribe());
            frames.Dispose();
            await clients[0].Frames.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally { foreach (var client in clients) client.Dispose(); }
    }

    [Fact]
    public async Task HttpStreamsSyntheticFramesAndStopsWithConnectedClient()
    {
        using var source = new TestSource();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        await using var server = new MJPEGStreamServer(source, new StreamOptions(port));
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task running = server.RunAsync(stop.Token);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                try { await client.GetStringAsync("/health", stop.Token); break; }
                catch (HttpRequestException) when (attempt < 30) { await Task.Delay(30, stop.Token); }
            }
            Assert.Contains("false", await client.GetStringAsync("/health", stop.Token));
            Assert.Contains("Tobii Eye Feed", await client.GetStringAsync("/", stop.Token));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/missing/image/left", stop.Token)).StatusCode);
            using var invalidHost = new HttpRequestMessage(HttpMethod.Get, "/health");
            invalidHost.Headers.Host = "example.com";
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(invalidHost, stop.Token)).StatusCode);
            Task<HttpResponseMessage> request = client.GetAsync("/test/image/left", HttpCompletionOption.ResponseHeadersRead, stop.Token);
            while (source.Frames.SubscriberCount == 0) await Task.Delay(10, stop.Token);
            source.Frames.Dispatch(Frame());
            using var response = await request;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("multipart/x-mixed-replace", response.Content.Headers.ContentType!.MediaType);
            Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
            using var stream = await response.Content.ReadAsStreamAsync(stop.Token);
            byte[] buffer = new byte[4096];
            int read = await stream.ReadAsync(buffer, stop.Token);
            Assert.Contains("Content-Type: image/jpeg", System.Text.Encoding.ASCII.GetString(buffer, 0, read));
            stop.Cancel();
            await running.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally { stop.Cancel(); await running; }
    }

    private sealed class TestSource : IEyeDeviceSource, IDisposable
    {
        public ImageDataDispatcher Frames { get; } = new();
        public DeviceSnapshot[] GetDeviceSnapshots(int port = 4442) => [];
        public bool GetImageDataDispatcher(string serial, out ImageDataDispatcher dispatcher)
        { dispatcher = Frames; return serial == "test"; }
        public void Dispose() => Frames.Dispose();
    }
}
