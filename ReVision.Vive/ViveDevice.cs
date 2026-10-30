using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;
using HidApi;

namespace ReVision.Vive;

public sealed class ViveDevice : IAsyncDisposable
{
    private readonly Device _device;
    private readonly CancellationTokenSource _stop;
    private readonly Task _worker;
    private int _failed;
    public bool HasFailed => Volatile.Read(ref _failed) != 0;

    public ViveDevice(Device device, string serialNumber, CancellationToken token)
    {
        _device = device;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        byte[] serial = Encoding.UTF8.GetBytes(serialNumber);
        _worker = Task.Factory.StartNew(() => KeepAlive(serial), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    // Offsets and CRC transformation are inherited from the upstream Vive control protocol.
    public static byte[] CreateKeepAlive(ReadOnlySpan<byte> serial, ushort authorizationId)
    {
        if (serial.Length is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(serial));
        Span<byte> challenge = stackalloc byte[serial.Length + 2];
        serial.CopyTo(challenge);
        BinaryPrimitives.WriteUInt16LittleEndian(challenge[serial.Length..], authorizationId);
        uint crc = Crc32.HashToUInt32(challenge);
        byte[] report = new byte[64];
        report[0] = 0x04;
        BinaryPrimitives.WriteUInt16LittleEndian(report.AsSpan(1), 0x2978);
        report[3] = 0x38;
        report[10] = 0x08;
        report[47] = (byte)((crc >> 25) | 0x80);
        report[48] = (byte)(crc >> 17);
        report[49] = (byte)(crc >> 9);
        return report;
    }

    private void KeepAlive(byte[] serial)
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                ReadOnlySpan<byte> report = _device.ReadTimeout(64, 500);
                ushort? latestChallenge = null;
                for (int i = 0; report.Length > 0 && i < 128; i++)
                {
                    if (report.Length == 64 && report[0] == 0x03 && BinaryPrimitives.ReadUInt16LittleEndian(report[1..]) == 0x2cd0)
                        latestChallenge = BinaryPrimitives.ReadUInt16LittleEndian(report[62..]);
                    report = _device.ReadTimeout(64, 1);
                }
                if (latestChallenge.HasValue) _device.SendFeatureReport(CreateKeepAlive(serial, latestChallenge.Value));
                if (_stop.Token.WaitHandle.WaitOne(500)) break;
            }
        }
        catch (Exception error)
        {
            if (!_stop.IsCancellationRequested)
            {
                Interlocked.Exchange(ref _failed, 1);
                Console.Error.WriteLine($"Vive keep-alive failed: {error.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        await _worker;
        _device.Dispose();
        _stop.Dispose();
    }
}
