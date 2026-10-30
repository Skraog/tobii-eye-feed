namespace ReVision.EyeDevice.Platform.Client;

public class COpenRealmRequest : BasePacket, IBinaryOutput
{
    private readonly uint _realmId;

    internal COpenRealmRequest(uint messageId, uint realmId) : base(messageId, OpCode.OPEN_REALM_REQUEST)
    {
        _realmId = realmId;
    }

    void IBinaryOutput.WriteBody()
    {
        this.EncodeUInt32(_realmId);
        this.EncodeUInt32Vector([0]);
    }
}
