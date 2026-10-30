using System.Diagnostics;
using System.Threading.Channels;
using ReVision.Dispatch;
using StbImageWriteSharp;

namespace ReVision.Streamer;

// One encoder per eye, regardless of the number or speed of HTTP clients.
public sealed class JpegBroadcast : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly ImageDataDispatcher.Eye _eye;
    private readonly int _quality;
    private readonly int _maxFps;
    private readonly Channel<EyeFrame> _input = Channel.CreateBounded<EyeFrame>(new BoundedChannelOptions(1)
    { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly CancellationTokenSource _stop;
    private readonly IDisposable _subscription;
    private readonly Task _worker;
    private Channel<byte[]>[] _clients = [];
    private long _encodedFrames;
    private bool _disposed;

    public JpegBroadcast(ImageDataDispatcher dispatcher, ImageDataDispatcher.Eye eye, int quality = 85, int maxFps = 120)
    {
        if (quality is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(quality));
        if (maxFps is < 1 or > 240) throw new ArgumentOutOfRangeException(nameof(maxFps));
        _eye = eye;
        _quality = quality;
        _maxFps = maxFps;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(dispatcher.Stopped);
        _subscription = dispatcher.Register(frame =>
        {
            if (frame.Eye == _eye && Volatile.Read(ref _clients).Length > 0) _input.Writer.TryWrite(frame);
        });
        _worker = Task.Run(EncodeAsync);
    }

    public long EncodedFrames => Interlocked.Read(ref _encodedFrames);

    public Client Subscribe()
    {
        var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
        { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true });
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed || _stop.IsCancellationRequested, this);
            if (_clients.Length >= 8) throw new InvalidOperationException("This eye already has eight viewers.");
            _clients = [.. _clients, channel];
        }
        return new Client(this, channel);
    }

    private void Unsubscribe(Channel<byte[]> channel)
    {
        lock (_gate) _clients = _clients.Where(item => item != channel).ToArray();
        channel.Writer.TryComplete();
    }

    private async Task EncodeAsync()
    {
        var encoder = new ImageWriter();
        using var output = new MemoryStream(16 * 1024);
        long lastEncoded = 0;
        try
        {
            await foreach (var received in _input.Reader.ReadAllAsync(_stop.Token))
            {
                var frame = received;
                if (lastEncoded != 0)
                {
                    double wait = 1.0 / _maxFps - Stopwatch.GetElapsedTime(lastEncoded).TotalSeconds;
                    if (wait > 0) await Task.Delay(TimeSpan.FromSeconds(wait), _stop.Token);
                    while (_input.Reader.TryRead(out var newer)) frame = newer;
                }
                if (Volatile.Read(ref _clients).Length == 0) continue;
                lastEncoded = Stopwatch.GetTimestamp();
                output.SetLength(0);
                encoder.WriteJpg(frame.Pixels, frame.Width, frame.Height, ColorComponents.Grey, output, _quality);
                byte[] jpeg = output.ToArray();
                Interlocked.Increment(ref _encodedFrames);
                foreach (var client in Volatile.Read(ref _clients)) client.Writer.TryWrite(jpeg);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception error)
        {
            Console.Error.WriteLine($"JPEG encoder stopped: {error}");
            _stop.Cancel();
        }
        finally
        {
            foreach (var client in Volatile.Read(ref _clients)) client.Writer.TryComplete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; }
        _subscription.Dispose();
        _stop.Cancel();
        _input.Writer.TryComplete();
        await _worker;
        _stop.Dispose();
    }

    public sealed class Client : IDisposable
    {
        private JpegBroadcast? _owner;
        private readonly Channel<byte[]> _channel;
        internal Client(JpegBroadcast owner, Channel<byte[]> channel) { _owner = owner; _channel = channel; }
        public ChannelReader<byte[]> Frames => _channel.Reader;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Unsubscribe(_channel);
    }
}
