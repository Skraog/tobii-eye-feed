namespace ReVision.EyeDevice.Platform.Client;

public class COpenRealmRequest : BasePacket, IBinaryOutput
{
    private uint _realmId;
    
    internal COpenRealmRequest(uint messageId, uint realmId) : base(messageId, OpCode.OPEN_REALM_REQUEST)
    {
        _realmId = realmId;
    }

    public bool Encode()
    {
        this.EncodeUInt32(_realmId);
        this.EncodeUInt32Vector([0]);
        
        return true;
    }
}