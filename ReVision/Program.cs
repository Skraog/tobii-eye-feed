using System.Runtime.InteropServices;
using ReVision.EyeDevice;
using ReVision.Streamer;
using ReVision.Vive;

namespace ReVision;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        CommandLineOptions options;
        try { options = CommandLineOptions.Parse(args); }
        catch (ArgumentException error) { Console.Error.WriteLine(error.Message); return 2; }
        if (options.Help)
        {
            Console.WriteLine("Tobii Eye Feed [--no-hardware] [--diagnose] [--duration SECONDS] [--port PORT]");
            Console.WriteLine("         [--jpeg-quality 1..100] [--max-fps 1..240] [--stop-file PATH]");
            Console.WriteLine("Defaults: port 4442, JPEG quality 85, maximum 120 fps per eye. Without --duration, runs until stopped.");
            Console.WriteLine("--no-hardware runs HTTP only. --diagnose checks native libraries and device access without capture.");
            Console.WriteLine("--stop-file exits when PATH exists, for service supervisors. Ctrl+C and Linux SIGTERM also stop cleanly.");
            return 0;
        }
        if (options.Diagnose) return NativeDiagnostics.Run();
        using var shutdown = new CancellationTokenSource();
        using var terminate = OperatingSystem.IsLinux()
            ? PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => { context.Cancel = true; shutdown.Cancel(); })
            : null;
        if (options.DurationSeconds is int seconds) shutdown.CancelAfter(TimeSpan.FromSeconds(seconds));
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; shutdown.Cancel(); };
        Console.CancelKeyPress += cancel;
        Task stopFile = WatchStopFileAsync(options.StopFile, shutdown);
        EyeDeviceManager? eyes = null;
        ViveDeviceManager? vive = null;
        try
        {
            Console.WriteLine($"Tobii Eye Feed 0.2.0 ({RuntimeInformation.OSDescription})");
            Console.WriteLine($"Dashboard: http://127.0.0.1:{options.Stream.Port}/");
            Console.WriteLine("Press Ctrl+C to stop. Camera passthrough requires an external inference application for eye tracking.");
            if (!options.NoHardware)
            {
                vive = new ViveDeviceManager(shutdown.Token);
                eyes = new EyeDeviceManager(shutdown.Token);
            }
            await using var server = new MJPEGStreamServer(eyes, options.Stream);
            Task serving = server.RunAsync(shutdown.Token);
            Task first = eyes == null ? serving : await Task.WhenAny(serving, eyes.Completion, vive!.Completion);
            if (first != serving)
            {
                bool requested = shutdown.IsCancellationRequested;
                shutdown.Cancel();
                try { await serving; }
                catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
                await first;
                if (!requested) throw new IOException("A camera monitor stopped unexpectedly.");
            }
            else await serving;
            if (!shutdown.IsCancellationRequested) throw new IOException("A camera worker or HTTP server stopped unexpectedly.");
            return 0;
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { return 0; }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine("Run --diagnose to check native libraries and USB permissions. See docs/troubleshooting.md.");
            return 1;
        }
        finally
        {
            shutdown.Cancel();
            await stopFile;
            if (eyes != null) await eyes.DisposeAsync();
            if (vive != null) await vive.DisposeAsync();
            Console.CancelKeyPress -= cancel;
            Console.WriteLine("Tobii Eye Feed stopped.");
        }
    }

    private static async Task WatchStopFileAsync(string? path, CancellationTokenSource stop)
    {
        if (path == null) return;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                if (File.Exists(path)) { stop.Cancel(); break; }
                await Task.Delay(250, stop.Token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }
}
