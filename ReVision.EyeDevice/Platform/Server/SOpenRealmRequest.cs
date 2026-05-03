namespace ReVision.EyeDevice.Platform.Server;

public class SOpenRealmRequest : BasePacket
{
    public byte[] ChallengeData { get; private init; }
    
    public SOpenRealmRequest(Stream stream) : base(stream)
    {
        this.DecodeUInt32();
        this.DecodeUInt32();
        ChallengeData = this.DecodeBlob();
    }
}