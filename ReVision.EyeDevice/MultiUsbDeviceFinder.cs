using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace ReVision.EyeDevice;

public class MultiUsbDeviceFinder : UsbDeviceFinder
{
    private UsbDeviceFinder[] _finders;
    
    public MultiUsbDeviceFinder(UsbDeviceFinder[] finders) 
    {
        _finders = finders;
    }

    public override bool Check(IUsbDevice usbDevice)
    {
        foreach (var finder in _finders)
        {
            if (finder.Check(usbDevice))
                return true;
        }

        return false;
    }
}