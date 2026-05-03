namespace ReVision.EyeDevice.Platform.Client;

public class CExtendedDataStreamEnumerate : BasePacket, IBinaryOutput
{
    internal CExtendedDataStreamEnumerate(uint messageId) : base(messageId, OpCode.EXTENDED_DATA_STREAM_ENUMERATE)
    {
    }

    public bool Encode()
    {
        return true;
    }
}