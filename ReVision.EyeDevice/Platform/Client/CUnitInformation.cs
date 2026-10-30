namespace ReVision.EyeDevice.Platform.Client;

public class CUnitInformation : BasePacket, IBinaryOutput
{
    internal CUnitInformation(uint messageId) : base(messageId, OpCode.UNIT_INFORMATION)
    {
    }

    void IBinaryOutput.WriteBody()
    {
    }
}
