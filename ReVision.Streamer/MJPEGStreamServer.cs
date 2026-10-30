using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReVision.Dispatch;

namespace ReVision.Streamer;

public sealed record StreamOptions(int Port = 4442, int JpegQuality = 85, int MaxFps = 120);

public sealed class MJPEGStreamServer : IAsyncDisposable
{
    private readonly record struct StreamKey(ImageDataDispatcher Dispatcher, ImageDataDispatcher.Eye Eye);
    private readonly ConcurrentDictionary<StreamKey, Lazy<JpegBroadcast>> _streams = new();
    private readonly IEyeDeviceSource? _devices;
    private readonly WebApplication _app;
    private readonly StreamOptions _options;
    private static readonly byte[] NewLine = "\r\n"u8.ToArray();

    public MJPEGStreamServer(IEyeDeviceSource? devices, StreamOptions? options = null)
    {
        _devices = devices;
        _options = options ?? new StreamOptions();
        if (_options.Port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(options));
        if (_options.JpegQuality is < 1 or > 100 || _options.MaxFps is < 1 or > 240) throw new ArgumentOutOfRangeException(nameof(options));
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.Listen(IPAddress.Loopback, _options.Port);
            server.Limits.MaxConcurrentConnections = 32;
            server.Limits.MaxRequestBodySize = 0;
            server.AddServerHeader = false;
        });
        _app = builder.Build();
        _app.Use(async (context, next) =>
        {
            string host = context.Request.Host.Host;
            if (host != "127.0.0.1" && !host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            { context.Response.StatusCode = 400; return; }
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self'; style-src 'self'; script-src 'self'; frame-ancestors 'none'";
            await next(context);
        });
        _app.MapGet("/", () => Results.Content(Assets.Read("index.html"), "text/html"));
        _app.MapGet("/app.js", () => Results.Content(Assets.Read("app.js"), "text/javascript"));
        _app.MapGet("/style.css", () => Results.Content(Assets.Read("style.css"), "text/css"));
        _app.MapGet("/health", () => Json(new { status = "ok", cameraVerified = Snapshots().Any(d => d.IsStreaming) }));
        _app.MapGet("/devices", () => Json(Snapshots()));
        _app.MapGet("/{device}/image/{eye}", StreamImageAsync);
    }

    private DeviceSnapshot[] Snapshots() => _devices?.GetDeviceSnapshots(_options.Port) ?? [];
    private static IResult Json<T>(T value) => Results.Text(JsonSerializer.Serialize(value), "application/json");

    public async Task RunAsync(CancellationToken token)
    {
        await _app.StartAsync(token);
        using var maintenanceStop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var maintenance = MaintainAsync(maintenanceStop.Token);
        try { await _app.WaitForShutdownAsync(token); }
        finally
        {
            maintenanceStop.Cancel();
            try { await maintenance; } catch (OperationCanceledException) when (maintenanceStop.IsCancellationRequested) { }
        }
    }

    private async Task MaintainAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(token))
            foreach (var item in _streams)
                if (item.Key.Dispatcher.Disposed && _streams.TryRemove(item.Key, out var stream) && stream.IsValueCreated)
                    await stream.Value.DisposeAsync();
    }

    private async Task StreamImageAsync(HttpContext context, string device, string eye)
    {
        ImageDataDispatcher.Eye source;
        if (eye == "left") source = ImageDataDispatcher.Eye.Left;
        else if (eye == "right") source = ImageDataDispatcher.Eye.Right;
        else { context.Response.StatusCode = 404; return; }
        if (_devices == null || !_devices.GetImageDataDispatcher(device, out var dispatcher) || dispatcher.Disposed)
        { context.Response.StatusCode = 404; return; }
        JpegBroadcast.Client viewer;
        try
        {
            var stream = _streams.GetOrAdd(new StreamKey(dispatcher, source), key =>
                new Lazy<JpegBroadcast>(() => new JpegBroadcast(key.Dispatcher, key.Eye, _options.JpegQuality, _options.MaxFps)));
            viewer = stream.Value.Subscribe();
        }
        catch (ObjectDisposedException) { context.Response.StatusCode = 503; return; }
        catch (InvalidOperationException) { context.Response.StatusCode = 429; return; }
        using (viewer)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, _app.Lifetime.ApplicationStopping))
        {
            context.Response.ContentType = "multipart/x-mixed-replace; boundary=frame-boundary";
            try
            {
                while (!timeout.IsCancellationRequested)
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    byte[] jpeg = await viewer.Frames.ReadAsync(timeout.Token);
                    byte[] header = Encoding.ASCII.GetBytes($"--frame-boundary\r\nContent-Type: image/jpeg\r\nContent-Length: {jpeg.Length}\r\n\r\n");
                    await context.Response.Body.WriteAsync(header, timeout.Token);
                    await context.Response.Body.WriteAsync(jpeg, timeout.Token);
                    await context.Response.Body.WriteAsync(NewLine, timeout.Token);
                    await context.Response.Body.FlushAsync(timeout.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (ChannelClosedException) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        foreach (var stream in _streams.Values) if (stream.IsValueCreated) await stream.Value.DisposeAsync();
    }

    private static class Assets
    {
        private static readonly ConcurrentDictionary<string, string> Cache = new();
        internal static string Read(string name) => Cache.GetOrAdd(name, static key =>
        {
            using var stream = typeof(MJPEGStreamServer).Assembly.GetManifestResourceStream("ReVision.Streamer.Web." + key)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }
}
