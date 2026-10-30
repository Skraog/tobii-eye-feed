using HidApi;
using LibUsbDotNet.LibUsb;

namespace ReVision.Vive;

public sealed class ViveDeviceManager : IAsyncDisposable
{
    private readonly UsbContext _context;
    private readonly CancellationTokenSource _stop;
    private readonly Dictionary<string, ViveDevice> _devices = new();
    private readonly Dictionary<string, DateTimeOffset> _retryAfter = new();
    private readonly Task _monitor;

    public ViveDeviceManager(CancellationToken token)
    {
        // Check the native dependency before allocating owned resources.
        _ = Hid.VersionString();
        _context = new UsbContext();
        _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        _monitor = Task.Run(MonitorAsync);
    }

    public Task Completion => _monitor;

    private async Task MonitorAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using var connected = _context.List();
                var present = new HashSet<string>();
                foreach (var usb in connected.OfType<UsbDevice>().Where(d => d.VendorId == 0x0bb4 && d.ProductId == 0x0309))
                {
                    string id = $"{usb.BusNumber}:{usb.Address}";
                    present.Add(id);
                    if (_devices.TryGetValue(id, out var failed) && failed.HasFailed)
                    {
                        _devices.Remove(id);
                        await failed.DisposeAsync();
                        _retryAfter[id] = DateTimeOffset.UtcNow.AddSeconds(5);
                    }
                    if (_devices.ContainsKey(id) || (_retryAfter.TryGetValue(id, out var retry) && retry > DateTimeOffset.UtcNow)) continue;
                    try
                    {
                        usb.Open();
                        string serial;
                        try { serial = usb.Info.SerialNumber; }
                        finally { usb.Close(); }
                        if (string.IsNullOrEmpty(serial)) throw new IOException("Vive USB serial descriptor is empty.");
                        var hid = new Device(usb.VendorId, usb.ProductId, serial);
                        _devices[id] = new ViveDevice(hid, serial, _stop.Token);
                        _retryAfter.Remove(id);
                        Console.WriteLine($"Vive {id}: HID keep-alive started.");
                    }
                    catch (Exception error)
                    {
                        _retryAfter[id] = DateTimeOffset.UtcNow.AddSeconds(5);
                        Console.Error.WriteLine($"Vive {id}: {error.Message}. Check USB/hidraw permissions; retrying in five seconds.");
                    }
                }
                foreach (string id in _devices.Keys.Where(id => !present.Contains(id)).ToArray())
                {
                    var device = _devices[id];
                    _devices.Remove(id);
                    _retryAfter.Remove(id);
                    await device.DisposeAsync();
                }
                await Task.Delay(1000, _stop.Token);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        try { await _monitor; }
        catch (Exception error) { Console.Error.WriteLine($"Vive monitor stopped: {error.Message}"); }
        finally
        {
            foreach (var device in _devices.Values) await device.DisposeAsync();
            _context.Dispose();
            _stop.Dispose();
        }
    }
}
