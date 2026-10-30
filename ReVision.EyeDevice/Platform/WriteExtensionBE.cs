using System.Buffers.Binary;

namespace ReVision.EyeDevice.Platform;

internal static class WriteExtensionBE
{
    internal static void WriteUInt32BE(this BasePacket packet, uint value)
    {
        Stream _stream = packet._stream;

        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);

        _stream.Write(buffer);
    }

    internal static void WriteUInt16BE(this BasePacket packet, ushort value)
    {
        Stream _stream = packet._stream;

        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);

        _stream.Write(buffer);
    }
}
