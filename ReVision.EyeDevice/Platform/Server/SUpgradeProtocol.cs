namespace ReVision.EyeDevice.Platform.Server;

public class SUpgradeProtocol : BasePacket
{
    public uint ProtocolVersion { get; private init; }

    public SUpgradeProtocol(Stream stream) : base(stream)
    {
        ProtocolVersion = this.DecodeUInt32();
    }

    public override string ToString()
    {
        return
            $"{nameof(SUpgradeProtocol)} {{\n" +
            $"  {nameof(ProtocolVersion)} = \"{ProtocolVersion}\"\n" +
            $"}}";
    }
}
