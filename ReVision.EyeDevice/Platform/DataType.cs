namespace ReVision.EyeDevice.Platform;

public enum DataType : byte
{
    UINT32 = 0x02,
    F32 = 0x03,
    F64 = 0x04,
    PROLOGUE = 0x05,
    I64 = 0x06,
    STRING = 0x14,
    BLOB = 0x15,
    UINT32_VECTOR = 0x17
}
