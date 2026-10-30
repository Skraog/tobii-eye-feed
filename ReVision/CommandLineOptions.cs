using ReVision.Streamer;

namespace ReVision;

public sealed record CommandLineOptions(bool NoHardware, bool Diagnose, bool Help, int? DurationSeconds,
    string? StopFile, StreamOptions Stream)
{
    public static CommandLineOptions Parse(string[] args)
    {
        bool noHardware = false, diagnose = false, help = false;
        int? duration = null;
        int port = 4442, quality = 85, fps = 120;
        string? stopFile = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i];
            if (!seen.Add(option)) throw new ArgumentException($"Repeated option: {option}");
            string Value()
            {
                if (++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"{option} requires a value.");
                return args[i];
            }
            int Number(int min, int max)
            {
                if (!int.TryParse(Value(), out int number) || number < min || number > max)
                    throw new ArgumentException($"{option} requires a number from {min} to {max}.");
                return number;
            }
            switch (option)
            {
                case "--help": help = true; break;
                case "--no-hardware": noHardware = true; break;
                case "--diagnose": diagnose = true; break;
                case "--duration": duration = Number(1, 86400); break;
                case "--port": port = Number(1024, 65535); break;
                case "--jpeg-quality": quality = Number(1, 100); break;
                case "--max-fps": fps = Number(1, 240); break;
                case "--stop-file": stopFile = Path.GetFullPath(Value()); break;
                default: throw new ArgumentException($"Unknown option: {option}. Use --help.");
            }
        }
        return new(noHardware, diagnose, help, duration, stopFile, new(port, quality, fps));
    }
}
