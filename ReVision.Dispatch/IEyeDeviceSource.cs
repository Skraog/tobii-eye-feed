namespace ReVision.Dispatch;

public interface IEyeDeviceSource
{
    DeviceSnapshot[] GetDeviceSnapshots(int port = 4442);
    bool GetImageDataDispatcher(string serialNumber, out ImageDataDispatcher dispatcher);
}

public sealed record DeviceSnapshot(string DeviceId, string SerialNumber, string DeviceModel, string Stage,
    string? Error, long LeftFrames, long RightFrames, double? LeftFrameAgeSeconds, double? RightFrameAgeSeconds,
    bool IsStreaming, string? LeftUrl, string? RightUrl);
