using System.Buffers.Binary;

namespace ReVision.EyeDevice.Platform;

internal static class ReadExtensionLE
{
    internal static uint ReadUInt32LE(this BasePacket packet)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        packet._stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    internal static ushort ReadUInt16LE(this BasePacket packet)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        packet._stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
    }
}