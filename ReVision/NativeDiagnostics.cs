using System.Runtime.InteropServices;
using HidApi;
using LibUsbDotNet.LibUsb;

namespace ReVision;

internal static class NativeDiagnostics
{
    public static int Run()
    {
        Console.WriteLine($"OS: {RuntimeInformation.OSDescription}; architecture: {RuntimeInformation.ProcessArchitecture}");
        bool librariesOk = true;
        int found = 0;
        try
        {
            using var context = new UsbContext();
            Console.WriteLine($"libusb: {NativeLibraryVersion.FullVersion}");
            using var devices = context.List();
            foreach (var device in devices)
            {
                bool vive = device.VendorId == 0x0bb4 && device.ProductId == 0x0309;
                bool eye = device.VendorId == 0x2104 && device.ProductId == 0x020f;
                if (!vive && !eye) continue;
                found++;
                Console.Write($"USB {device.VendorId:x4}:{device.ProductId:x4} ({(vive ? "Vive HID parent" : "EyeChip")}): ");
                try { device.Open(); Console.WriteLine("open succeeded"); device.Close(); }
                catch (Exception error) { Console.WriteLine($"open failed: {error.GetBaseException().Message}"); librariesOk = false; }
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"libusb check failed: {error.GetBaseException().Message}");
            librariesOk = false;
        }
        try
        {
            Hid.Init();
            Console.WriteLine($"HIDAPI: {Hid.VersionString()}");
            foreach (var info in Hid.Enumerate(0x0bb4, 0x0309))
            {
                Console.Write($"HID {info.Path}: ");
                try { using var device = info.ConnectToDevice(); Console.WriteLine("open succeeded"); }
                catch (Exception error) { Console.WriteLine($"open failed: {error.GetBaseException().Message}"); librariesOk = false; }
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"HIDAPI check failed: {error.GetBaseException().Message}");
            librariesOk = false;
        }
        finally
        {
            try { Hid.Exit(); } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
        }
        if (found == 0) Console.WriteLine("No supported headset USB devices found. Library checks do not establish hardware capture.");
        if (OperatingSystem.IsLinux())
            Console.WriteLine("Linux: install libusb and HIDAPI's hidraw backend, then linux/70-revision-vive-pro-eye.rules. Reconnect the headset from an active desktop login. Use linux/doctor.sh for details.");
        return librariesOk ? 0 : 1;
    }
}
