namespace ReVision.EyeDevice.Platform.Client;

public class COpenRealmWithSalt : BasePacket, IBinaryOutput
{
    private uint _realmId;
    private byte[] _challenge;
    private byte[] _salt;
    
    internal COpenRealmWithSalt(uint messageId, uint realmId, byte[] challenge, byte[] salt) : base(messageId, OpCode.OPEN_REALM_WITH_SALT)
    {
        _realmId = realmId;
        _challenge = challenge;
        _salt = salt;
    }

    public bool Encode()
    {
        this.EncodeUInt32(_realmId);
        this.EncodeUInt32(0); // ???
        this.EncodeBlob(_challenge);
        this.EncodeBlob(_salt);
        
        return true;
    }
}