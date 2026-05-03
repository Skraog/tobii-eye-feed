using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;
using HidApi;

namespace ReVision.Vive;

public class ViveDevice
{
    private CancellationTokenSource _cancellationTokenSource;
    private CancellationToken _cancellationToken;
    
    private Device _device;
    private Crc32 _crc;
    private byte[] _deviceSerialNumber;

    public ViveDevice(Device device, string deviceSn, CancellationToken linkedCancellationToken)
    {
        _device = device;
        _crc = new Crc32();
        _deviceSerialNumber = Encoding.UTF8.GetBytes(deviceSn);
        
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(linkedCancellationToken);
        _cancellationToken = _cancellationTokenSource.Token;

        Task.Run(EnableEyeChip);
    }

    private async Task EnableEyeChip()
    {
        while (!_cancellationToken.IsCancellationRequested)
        {
            try
            {
                bool hasValidStatus = false;
                ViveControl viveControl = default;

                for (int i = 0; i < 10; ++i)
                {
                    if (_cancellationToken.IsCancellationRequested)
                        return;

                    var hidReport = _device.Read(64);

                    if (hidReport.Length != 64)
                        throw new ArgumentOutOfRangeException();

                    var parsedViveControl = MemoryMarshal.Read<ViveControl>(hidReport);

                    if (parsedViveControl.ReportId != 0x03)
                        continue;

                    if (parsedViveControl.Command != 0x2CD0)
                        continue;
                
                    viveControl = parsedViveControl;
                    hasValidStatus = true;
                }

                if (!hasValidStatus)
                {
                    await Task.Delay(500, _cancellationToken);
                    continue;
                }

                _crc.Reset();
                _crc.Append(_deviceSerialNumber);
                _crc.Append(BitConverter.GetBytes(viveControl.AuthorizationId));

                uint crc = _crc.GetCurrentHashAsUInt32();

                viveControl = new ViveControl(0x04, 0x2978, 0x38, 0x08, crc);
                var viveData =
                    MemoryMarshal.Cast<ViveControl, byte>(MemoryMarshal.CreateSpan(ref viveControl, 64))[..64];
                _device.SendFeatureReport(viveData);
                
                await Task.Delay(500, _cancellationToken);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
    
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
    private unsafe struct ViveControl
    {
        [FieldOffset(0)]
        public byte ReportId;
        [FieldOffset(1)]
        public ushort Command;
        [FieldOffset(3)]
        public byte Length;
        
        [FieldOffset(10)]
        public byte Unknown2;
        
        [FieldOffset(47)]
        public fixed byte AuthorizationCode[3];

        [FieldOffset(62)]
        public ushort AuthorizationId;

        public ViveControl(byte reportId, ushort command, byte length, byte unknown2, uint authorizationCode)
        {
            ReportId = reportId;
            Command = command;
            Length = length;
            Unknown2 = unknown2;
            ToAuthorizationCode(authorizationCode);
        }
        
        private void ToAuthorizationCode(uint crc)
        {
            AuthorizationCode[0] = (byte)((crc >> 25) | 0x80);
            AuthorizationCode[1] = (byte)(crc >> 17);
            AuthorizationCode[2] = (byte)(crc >> 9);
        }
    }
}