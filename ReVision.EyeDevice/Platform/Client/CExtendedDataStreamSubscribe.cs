namespace ReVision.EyeDevice.Platform.Client;

public class CExtendedDataStreamSubscribe : BasePacket, IBinaryOutput
{
    private readonly uint _streamId;

    internal CExtendedDataStreamSubscribe(uint messageId, uint streamId) : base(messageId, OpCode.EXTENDED_DATA_STREAM_SUBSCRIBE)
    {
        _streamId = streamId;
    }

    void IBinaryOutput.WriteBody()
    {
        this.EncodeUInt32(_streamId);
        this.EncodeUInt32Vector([]);
    }
}
