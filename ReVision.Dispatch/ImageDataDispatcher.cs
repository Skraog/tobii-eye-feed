using System.Diagnostics;

namespace ReVision.Dispatch;

public sealed class ImageDataDispatcher : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stopped = new();
    private Action<EyeFrame>[] _subscribers = [];
    private readonly long[] _frames = new long[2];
    private readonly long[] _lastFrame = new long[2];
    private int _disposed;

    public ImageDataDispatcher() => Stopped = _stopped.Token;
    public CancellationToken Stopped { get; }
    public bool Disposed => Volatile.Read(ref _disposed) != 0;
    public long LeftFrames => Interlocked.Read(ref _frames[0]);
    public long RightFrames => Interlocked.Read(ref _frames[1]);
    public int SubscriberCount => Volatile.Read(ref _subscribers).Length;

    public double? FrameAgeSeconds(Eye eye)
    {
        long timestamp = Interlocked.Read(ref _lastFrame[(int)eye]);
        return timestamp == 0 ? null : Stopwatch.GetElapsedTime(timestamp).TotalSeconds;
    }

    public IDisposable Register(Action<EyeFrame> subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            _subscribers = [.. _subscribers, subscriber];
        }
        return new Subscription(this, subscriber);
    }

    private void Unregister(Action<EyeFrame> subscriber)
    {
        lock (_gate) _subscribers = _subscribers.Where(item => item != subscriber).ToArray();
    }

    // Subscribers must not modify a producer-owned pixel array.
    public void Dispatch(EyeFrame frame)
    {
        if (Disposed) return;
        Interlocked.Increment(ref _frames[(int)frame.Eye]);
        Interlocked.Exchange(ref _lastFrame[(int)frame.Eye], Stopwatch.GetTimestamp());
        foreach (var subscriber in Volatile.Read(ref _subscribers))
        {
            try { subscriber(frame); }
            catch (Exception error)
            {
                Unregister(subscriber);
                Console.Error.WriteLine($"Frame subscriber removed after an error: {error.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        lock (_gate) _subscribers = [];
        _stopped.Cancel();
        _stopped.Dispose();
    }

    public enum Eye { Left, Right }

    private sealed class Subscription(ImageDataDispatcher owner, Action<EyeFrame> subscriber) : IDisposable
    {
        private ImageDataDispatcher? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Unregister(subscriber);
    }
}

public sealed record EyeFrame
{
    public EyeFrame(ImageDataDispatcher.Eye eye, int width, int height, byte[] pixels, long timestampUs = 0)
    {
        if (eye is not (ImageDataDispatcher.Eye.Left or ImageDataDispatcher.Eye.Right))
            throw new ArgumentOutOfRangeException(nameof(eye));
        if (width is < 1 or > 1024 || height is < 1 or > 1024)
            throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be between 1 and 1024 pixels.");
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != checked(width * height))
            throw new ArgumentException("An 8-bit grayscale image must contain one byte per pixel.", nameof(pixels));
        Eye = eye;
        Width = width;
        Height = height;
        Pixels = pixels;
        TimestampUs = timestampUs;
    }

    public ImageDataDispatcher.Eye Eye { get; }
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
    public long TimestampUs { get; }
}
