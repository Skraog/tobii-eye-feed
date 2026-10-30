using System.Buffers.Binary;

namespace ReVision.EyeDevice.Platform;

public static class WriteExtensionEncoding
{
    public static void EncodeUInt32(this BasePacket packet, uint value)
    {
        Stream _stream = packet._stream;

        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        buffer[0] = (byte)DataType.UINT32;
        _stream.Write(buffer[..1]);

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)DataLength.UINT32);
        _stream.Write(buffer);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public static void EncodeUInt32Vector(this BasePacket packet, uint[] vector)
    {
        Stream _stream = packet._stream;

        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        buffer[0] = (byte)DataType.UINT32_VECTOR;
        _stream.Write(buffer[..1]);

        var vectorLength = vector.Length;

        // Data Length in Bytes
        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)((vectorLength * (int)DataLength.UINT32) + (int)DataLength.UINT32));
        _stream.Write(buffer);

        // Data Length in Entries
        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)vectorLength);
        _stream.Write(buffer);

        foreach (var entry in vector)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer, entry);
            _stream.Write(buffer);
        }
    }

    public static void EncodeBlob(this BasePacket packet, byte[] blob)
    {
        Stream _stream = packet._stream;

        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        buffer[0] = (byte)DataType.BLOB;
        _stream.Write(buffer[..1]);

        var blobLength = (uint)blob.Length;

        packet.WriteUInt32BE(blobLength + sizeof(uint));
        packet.WriteUInt32BE(blobLength);

        _stream.Write(blob);
    }
}
