using System.Buffers.Binary;

namespace ReVision.EyeDevice.Platform;

internal static class WriteExtensionLE
{
    internal static void WriteUInt32LE(this BasePacket packet, uint value)
    {
        Stream _stream = packet._stream;
        
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        
        _stream.Write(buffer);
    }
}