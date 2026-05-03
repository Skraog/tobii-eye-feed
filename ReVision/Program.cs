using System.Security.Cryptography;
using System.Text.Json;
using ReVision.EyeDevice;
using ReVision.Streamer;
using ReVision.Vive;

namespace ReVision;

class Program
{
    private static CancellationTokenSource _cancellationTokenSource;
    private static CancellationToken _cancellationToken;
    
    private static ViveDeviceManager _viveDeviceManager;
    private static EyeDeviceManager  _eyeDeviceManager;
    private static StreamServerManager _streamServerManager;

    static async Task Main(string[] args)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;
        
        _viveDeviceManager = new ViveDeviceManager(_cancellationToken);
        _eyeDeviceManager = new EyeDeviceManager(_cancellationToken);
        _streamServerManager = new StreamServerManager(_eyeDeviceManager, _cancellationToken);
        
        await Task.Delay(Timeout.Infinite, _cancellationToken);
    }
}