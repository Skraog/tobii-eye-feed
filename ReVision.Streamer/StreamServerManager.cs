using ReVision.EyeDevice;

namespace ReVision.Streamer;

public class StreamServerManager
{
    private static StreamServerManager? _streamServerManager;
    
    private CancellationTokenSource _cancellationTokenSource;
    private CancellationToken _cancellationToken;
    
    private EyeDeviceManager _eyeDeviceManager;
    private MJPEGStreamServer _mjpegStreamServer;

    public StreamServerManager(EyeDeviceManager eyeDeviceManager, CancellationToken linkedCancellationToken)
    {
        if (_streamServerManager != null)
            throw new InvalidOperationException("StreamServerManager already exists");

        _streamServerManager = this;
        
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(linkedCancellationToken);
        _cancellationToken = _cancellationTokenSource.Token;
        
        _eyeDeviceManager = eyeDeviceManager;
        _mjpegStreamServer = new MJPEGStreamServer();
    }

    ~StreamServerManager()
    {
        _streamServerManager = null;
    }

    public static StreamServerManager Get()
    {
        return _streamServerManager!;
    }

    public EyeDeviceManager GetEyeDeviceManager()
    {
        return _eyeDeviceManager;
    }
}