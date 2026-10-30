using LibUsbDotNet.LibUsb;
using ReVision.Dispatch;

namespace ReVision.EyeDevice;

public sealed class EyeDeviceManager : IEyeDeviceSource, IAsyncDisposable
{
    private sealed class Entry
    {
        public EyeDevice? Device;
        public string? Error;
        public DateTimeOffset RetryAfter;
    }

    private readonly UsbContext _context = new();
    private readonly CancellationTokenSource _stop;
    private readonly Task _monitor;
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _devices = new();

    public EyeDeviceManager(CancellationToken token)
    {
        _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        _monitor = Task.Run(MonitorAsync);
    }

    public static bool IsSupported(ushort vendor, ushort product) => vendor == 0x2104 && product == 0x020f;

    public Task Completion => _monitor;

    private async Task MonitorAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using var connected = _context.List();
                var present = new HashSet<string>();
                foreach (var usb in connected.OfType<UsbDevice>().Where(d => IsSupported(d.VendorId, d.ProductId)))
                {
                    string id = $"{usb.BusNumber}:{usb.Address}";
                    present.Add(id);
                    Entry entry;
                    lock (_gate)
                    {
                        if (!_devices.TryGetValue(id, out entry!)) _devices[id] = entry = new Entry();
                    }
                    if (entry.Device?.HasFailed == true)
                    {
                        var failed = entry.Device;
                        lock (_gate) { entry.Error = failed.InitializationError; entry.Device = null; entry.RetryAfter = DateTimeOffset.UtcNow.AddSeconds(5); }
                        await failed.DisposeAsync();
                    }
                    if (entry.Device != null || DateTimeOffset.UtcNow < entry.RetryAfter) continue;
                    try
                    {
                        var device = new EyeDevice(usb.Clone(), _stop.Token);
                        lock (_gate) { entry.Device = device; entry.Error = null; }
                    }
                    catch (Exception error)
                    {
                        lock (_gate) { entry.Error = error.GetBaseException().Message; entry.RetryAfter = DateTimeOffset.UtcNow.AddSeconds(5); }
                        Console.Error.WriteLine($"EyeChip {id}: {entry.Error}. Retrying in five seconds.");
                    }
                }
                KeyValuePair<string, Entry>[] removed;
                lock (_gate)
                {
                    removed = _devices.Where(item => !present.Contains(item.Key)).ToArray();
                    foreach (var item in removed) _devices.Remove(item.Key);
                }
                foreach (var item in removed) if (item.Value.Device != null) await item.Value.Device.DisposeAsync();
                await Task.Delay(1000, _stop.Token);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    public bool GetImageDataDispatcher(string serialNumber, out ImageDataDispatcher dispatcher)
    {
        lock (_gate)
        {
            var device = _devices.Values.Select(item => item.Device).FirstOrDefault(d => d?.SerialNumber == serialNumber && !d.HasFailed);
            dispatcher = device?.GetImageDataDispatcher()!;
            return device != null;
        }
    }

    public DeviceSnapshot[] GetDeviceSnapshots(int port = 4442)
    {
        lock (_gate)
            return _devices.Select(item =>
            {
                var device = item.Value.Device;
                var frames = device?.GetImageDataDispatcher();
                double? leftAge = frames?.FrameAgeSeconds(ImageDataDispatcher.Eye.Left);
                double? rightAge = frames?.FrameAgeSeconds(ImageDataDispatcher.Eye.Right);
                bool live = leftAge is <= 2 && rightAge is <= 2 && device?.InitializationError == null;
                string serial = device?.SerialNumber ?? "";
                string? prefix = string.IsNullOrEmpty(serial) ? null : $"http://127.0.0.1:{port}/{Uri.EscapeDataString(serial)}/image/";
                string stage = live ? "Streaming" : device?.InitializationStage ?? "Waiting to retry";
                if (!live && frames?.LeftFrames > 0 && device?.InitializationError == null) stage = "Waiting for fresh camera frames";
                return new DeviceSnapshot(item.Key, serial, device?.DeviceModel ?? "EyeChip", stage,
                    device?.InitializationError ?? item.Value.Error, frames?.LeftFrames ?? 0, frames?.RightFrames ?? 0,
                    leftAge, rightAge, live, prefix == null ? null : prefix + "left", prefix == null ? null : prefix + "right");
            }).ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        try { await _monitor; }
        catch (Exception error) { Console.Error.WriteLine($"EyeChip monitor stopped: {error.Message}"); }
        finally
        {
            foreach (var item in _devices.Values) if (item.Device != null) await item.Device.DisposeAsync();
            _context.Dispose();
            _stop.Dispose();
        }
    }
}
