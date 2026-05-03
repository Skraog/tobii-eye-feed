using System.Buffers.Binary;

namespace ReVision.EyeDevice.Platform;

internal static class ReadExtensionBE
{
    internal static int ReadI32BE(this BasePacket packet)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        packet._stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
    }
    
    internal static long ReadI64BE(this BasePacket packet)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        packet._stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt64BigEndian(buffer);
    }
    
    internal static uint ReadUInt32BE(this BasePacket packet)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        packet._stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }
    
    internal static ushort ReadUInt16BE(this BasePacket packet)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        packet._stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt16BigEndian(buffer);
    }
}