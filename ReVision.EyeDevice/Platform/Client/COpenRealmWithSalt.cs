namespace ReVision.EyeDevice.Platform.Client;

public class COpenRealmWithSalt : BasePacket, IBinaryOutput
{
    private readonly uint _realmId;
    private readonly byte[] _challenge;
    private readonly byte[] _salt;

    internal COpenRealmWithSalt(uint messageId, uint realmId, byte[] challenge, byte[] salt) : base(messageId, OpCode.OPEN_REALM_WITH_SALT)
    {
        _realmId = realmId;
        _challenge = challenge;
        _salt = salt;
    }

    void IBinaryOutput.WriteBody()
    {
        this.EncodeUInt32(_realmId);
        this.EncodeUInt32(0); // Reserved field required by the realm command.
        this.EncodeBlob(_challenge);
        this.EncodeBlob(_salt);
    }
}
