using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;
using ReVision.Dispatch;

namespace ReVision.EyeDevice;

public class EyeDeviceManager : IDisposable
{
    private CancellationTokenSource _cancellationTokenSource;
    private CancellationToken _cancellationToken;

    private UsbContext _usbContext;
    private DeviceManager _deviceManager;
    private MultiUsbDeviceFinder _multiUsbDeviceFinder;
    
    private List<EyeDevice> _devices;
    
    public EyeDeviceManager(CancellationToken linkedCancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(linkedCancellationToken);
        _cancellationToken = _cancellationTokenSource.Token;

        _usbContext = new UsbContext();
        _deviceManager = new DeviceManager(_usbContext);
        _multiUsbDeviceFinder = new MultiUsbDeviceFinder([
            new UsbDeviceFinder // VR4U2P2
            {
                Vid = 0x2104,
                Pid = 0x020F
            }, new UsbDeviceFinder // XR5EYECHIP
            {
                Vid = 0x2104,
                Pid = 0x0561
            }, new UsbDeviceFinder // VR4YODAP1
            {
                Vid = 0x2104,
                Pid = 0x0210
            }, new UsbDeviceFinder // IS5_Large_Eyetracker_5
            {
                Vid = 0x2104,
                Pid = 0x0313
            }
        ]);

        _devices = new List<EyeDevice>();
        
        _deviceManager.Start();
        Task.Run(DetectNewDevice, _cancellationToken);
    }

    private async Task DetectNewDevice()
    {
        var usbArrivalToken = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken).Token;
        UsbDevice? usbDevice = await _deviceManager.WaitForDeviceArrival(_multiUsbDeviceFinder, TimeSpan.FromSeconds(30), usbArrivalToken, TimeSpan.FromMilliseconds(500));
        
        while (!_cancellationToken.IsCancellationRequested)
        {
            usbArrivalToken = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken).Token;
            usbDevice ??= await _deviceManager.WaitForNewDeviceArrival(_multiUsbDeviceFinder, TimeSpan.FromSeconds(30), usbArrivalToken, TimeSpan.FromMilliseconds(500));

            if (usbDevice == null)
                continue;
            
            _devices.Add(new EyeDevice(usbDevice, _cancellationToken));
            
            usbDevice = null;
        }
    }

    public bool GetImageDataDispatcher(string serialNumber, out ImageDataDispatcher imageDataDispatcher)
    {
        imageDataDispatcher = default;
        
        foreach (var device in _devices)
        {
            if (device.SerialNumber != serialNumber)
                continue;

            imageDataDispatcher = device.GetImageDataDispatcher();
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        _deviceManager.Dispose();
        _usbContext.Dispose();
    }
}