namespace ReVision.EyeDevice.Platform.Client;

public class CUpgradeProtocol : BasePacket, IBinaryOutput
{
    private uint[] _targetProtocolVersions;
    
    internal CUpgradeProtocol(uint messageId, uint[] targetProtocolVersions) : base(messageId, OpCode.PROTOCOL_UPGRADE)
    {
        _targetProtocolVersions = targetProtocolVersions;
    }

    public bool Encode()
    {
        this.EncodeUInt32Vector(_targetProtocolVersions);
        
        return true;
    }
}