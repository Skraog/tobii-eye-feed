namespace ReVision.EyeDevice.Platform.Client;

public class CUpgradeProtocol : BasePacket, IBinaryOutput
{
    private readonly uint[] _targetProtocolVersions;

    internal CUpgradeProtocol(uint messageId, uint[] targetProtocolVersions) : base(messageId, OpCode.PROTOCOL_UPGRADE)
    {
        _targetProtocolVersions = targetProtocolVersions;
    }

    void IBinaryOutput.WriteBody()
    {
        this.EncodeUInt32Vector(_targetProtocolVersions);
    }
}
