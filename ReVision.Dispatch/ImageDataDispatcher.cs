namespace ReVision.Dispatch;

public class ImageDataDispatcher : IDisposable
{
    public bool Disposed { get; private set; }
    
    private List<dFunc> _funcs = new();
    public delegate void dFunc(Eye eye, byte[] rawImageData);
    
    public void Register(dFunc func) => _funcs.Add(func);
    public void Unregister(dFunc func) => _funcs.Remove(func);
    
    public void Dispatch(Eye eye, byte[] rawImageData)
    {
        foreach (var func in _funcs)
            func(eye, rawImageData);
    }

    public enum Eye
    {
        Left = 0,
        Right = 1
    }

    public void Dispose()
    {
        Disposed = true;
    }
}