using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using ReVision.KeyWarning.Views;

namespace ReVision.KeyWarning;

public class KeyWarning
{
    private TaskCompletionSource<bool> _tcs;
    private Thread _staThread;

    private FieldInfo _setupWasAlreadyCalled;

    public KeyWarning()
    {
        _setupWasAlreadyCalled = typeof(AppBuilder)
            .GetField("s_setupWasAlreadyCalled", BindingFlags.Static | BindingFlags.NonPublic)!;
        
        _tcs = new TaskCompletionSource<bool>();
        _staThread = new Thread(Initialize);
        _staThread.IsBackground = true;
        _staThread.Start();
    }
    
    [STAThread]
    private void Initialize()
    {
        if (!(bool)_setupWasAlreadyCalled.GetValue(null))
        {
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .StartWithClassicDesktopLifetime([]);
        }
        else
        {
            MainWindow mainWindow;
            
            Dispatcher.UIThread.Invoke(() =>
            {
                mainWindow = new MainWindow();
                mainWindow.Show();
            });
        }
        
        _tcs.SetResult(true);
    }

    public async Task WaitForApplicationExit()
    {
        await _tcs.Task;
    }
}