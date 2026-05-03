using HidApi;
using LibUsbDotNet.Info;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace ReVision.Vive;

public class ViveDeviceManager : IDisposable
{
    private CancellationTokenSource _cancellationTokenSource;
    private CancellationToken _cancellationToken;

    private UsbContext _usbContext;
    private DeviceManager _deviceManager;
    private UsbDeviceFinder _usbDeviceFinder;
    
    private List<ViveDevice> _devices;
    
    public ViveDeviceManager(CancellationToken linkedCancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(linkedCancellationToken);
        _cancellationToken = _cancellationTokenSource.Token;

        _usbContext = new UsbContext();
        _deviceManager = new DeviceManager(_usbContext);
        _usbDeviceFinder = new UsbDeviceFinder
        {
            Vid = 0x0BB4,
            Pid = 0x0309
        };

        _devices = new List<ViveDevice>();
        
        _deviceManager.Start();
        Task.Run(DetectNewDevice, _cancellationToken);
    }

    private async Task DetectNewDevice()
    {
        var usbArrivalToken = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken).Token;
        UsbDevice? usbDevice = await _deviceManager.WaitForDeviceArrival(_usbDeviceFinder, TimeSpan.FromSeconds(30), usbArrivalToken, TimeSpan.FromMilliseconds(500));
        
        while (!_cancellationToken.IsCancellationRequested)
        {
            usbArrivalToken = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken).Token;
            usbDevice ??= await _deviceManager.WaitForNewDeviceArrival(_usbDeviceFinder, TimeSpan.FromSeconds(30), usbArrivalToken, TimeSpan.FromMilliseconds(500));

            if (usbDevice == null)
                continue;
            
            usbDevice.Open();
            var deviceSn = usbDevice.Info.SerialNumber;
            usbDevice.Close();
            
            var hidDevice = new Device(usbDevice.VendorId, usbDevice.ProductId, deviceSn);
            _devices.Add(new ViveDevice(hidDevice, deviceSn, _cancellationToken));
            
            usbDevice = null;
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        _deviceManager.Dispose();
        _usbContext.Dispose();
    }
}