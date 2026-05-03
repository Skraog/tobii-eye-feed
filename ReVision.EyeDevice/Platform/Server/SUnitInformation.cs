namespace ReVision.EyeDevice.Platform.Server;

public class SUnitInformation : BasePacket
{
    public string SerialNumber { get; private init; }
    public string DeviceModel { get; private init; }
    public string DeviceLine { get; private init; }
    public string FirmwareVersion { get; private init; }
    
    public SUnitInformation(Stream stream) : base(stream)
    {
        SerialNumber = this.DecodeString();
        DeviceModel = this.DecodeString();
        DeviceLine = this.DecodeString();
        FirmwareVersion = this.DecodeString();
    }
    
    public override string ToString()
    {
        return
            $"{nameof(SUnitInformation)} {{\n" +
            $"  {nameof(SerialNumber)} = \"{SerialNumber}\",\n" +
            $"  {nameof(DeviceModel)} = \"{DeviceModel}\",\n" +
            $"  {nameof(DeviceLine)} = \"{DeviceLine}\",\n" +
            $"  {nameof(FirmwareVersion)} = \"{FirmwareVersion}\"\n" +
            $"}}";
    }
}