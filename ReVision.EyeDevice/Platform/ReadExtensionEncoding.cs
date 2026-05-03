using System.Numerics;
using System.Text;

namespace ReVision.EyeDevice.Platform;

internal static class ReadExtensionEncoding
{
    private static void VerifyLengthAndType(BasePacket packet, DataType expectedType, DataLength expectedLength)
    {
        Stream _stream = packet._stream;

        byte type = (byte)_stream.ReadByte();
        if (type != (byte)expectedType)
            throw new InvalidDataException($"Expected type {type} but got {_stream.ReadByte()}");

        uint length = packet.ReadUInt32BE();
        if (length != (uint)expectedLength)
            throw new InvalidDataException($"Expected length of {expectedLength} but got {length}");
    }
    
    private static void VerifyType(BasePacket packet, DataType expectedType)
    {
        Stream _stream = packet._stream;

        byte type = (byte)_stream.ReadByte();
        if (type != (byte)expectedType)
            throw new InvalidDataException($"Expected type {type} but got {_stream.ReadByte()}");
    }
    
    internal static uint DecodeUInt32(this BasePacket packet)
    {
        VerifyLengthAndType(packet, DataType.UINT32, DataLength.UINT32);
        return packet.ReadUInt32BE();
    }

    internal static long DecodeI64(this BasePacket packet)
    {
        VerifyLengthAndType(packet, DataType.I64, DataLength.I64);
        return packet.ReadI64BE();
    }

    internal static float DecodeF32(this BasePacket packet)
    {
        VerifyLengthAndType(packet, DataType.F32, DataLength.F32);
        var value = packet.ReadI32BE();
        
        const float scale = (float)(1.0 / (1 << 16));
        return value * scale;
    }
    
    internal static double DecodeF64(this BasePacket packet)
    {
        VerifyLengthAndType(packet, DataType.F64, DataLength.F64);
        var value = packet.ReadI64BE();
        
        const double scale = 1.0 / ((long)1 << 42);
        return value * scale;
    }

    internal static string DecodeString(this BasePacket packet)
    {
        VerifyType(packet, DataType.STRING);
        
        packet.ReadUInt32BE();
        var _stream = packet._stream;
        
        var stringLength = packet.ReadUInt32BE();
        var remainingLength = (_stream.Length - _stream.Position);

        if (remainingLength < stringLength)
            throw new InvalidDataException($"String is {stringLength} but only have {remainingLength} bytes.");

        byte[] stringData = new byte[stringLength];
        _stream.ReadExactly(stringData, 0, (int)stringLength);
        return Encoding.UTF8.GetString(stringData);
    }

    internal static byte[] DecodeBlob(this BasePacket packet)
    {
        VerifyType(packet, DataType.BLOB);

        packet.ReadUInt32BE();
        var _stream = packet._stream;
        
        var blobLength = packet.ReadUInt32BE();
        var remainingLength = (_stream.Length - _stream.Position);
        
        if (remainingLength < blobLength)
            throw new InvalidDataException($"Blob is {blobLength} but only have {remainingLength} bytes.");
        
        byte[] blobData = new byte[blobLength];
        _stream.ReadExactly(blobData, 0, (int)blobLength);
        return blobData;
    }

    internal static uint DecodePrologue(this BasePacket packet)
    {
        VerifyLengthAndType(packet, DataType.PROLOGUE, DataLength.PROLOGUE);
        return packet.ReadUInt32BE();
    }

    internal static (uint, uint) DecodeTreeList(this BasePacket packet)
    {
        var prologue = packet.DecodePrologue();
        
        if ((prologue & 0xFFF) != (int)PrologueType.TREE_HEADER)
            throw new InvalidDataException($"Expected type {(int)PrologueType.TREE_HEADER} but got {prologue & 0xFFF}");

        var unknown = packet.DecodeUInt32();
        return ((prologue >> 16) - 1, unknown);
    }

    internal static uint DecodeTreeRow(this BasePacket packet, PrologueType expectedPrologue)
    {
        var prologue = packet.DecodePrologue();
        
        if ((prologue & 0xFFF) != (int)expectedPrologue)
            throw new InvalidDataException($"Expected type {(int)expectedPrologue} but got {prologue}");

        return prologue >> 16;
    }

    internal static uint DecodeTreeColumn(this BasePacket packet, PrologueType expectedPrologue)
    {
        var prologue = packet.DecodePrologue();
        
        if (prologue != (uint)expectedPrologue)
            throw new InvalidDataException($"Expected type {(int)expectedPrologue} but got {prologue}");

        return packet.DecodeUInt32();
    }
    
    internal static Vector3 DecodeVector3(this BasePacket packet)
    {
        var prologue = packet.DecodePrologue();
        
        if (prologue != (int)PrologueType.EXTENDED_DATA_STREAM_VECTOR3)
            throw new InvalidDataException($"Expected type {(int)PrologueType.EXTENDED_DATA_STREAM_VECTOR3} but got {prologue }");

        return new Vector3(packet.DecodeF32(), packet.DecodeF32(), packet.DecodeF32());
    }
    
    internal static Vector2d DecodeVector2d(this BasePacket packet)
    {
        var prologue = packet.DecodePrologue();
        
        if (prologue != (int)PrologueType.EXTENDED_DATA_STREAM_VECTOR2D)
            throw new InvalidDataException($"Expected type {(int)PrologueType.EXTENDED_DATA_STREAM_VECTOR2D} but got {prologue }");

        return new Vector2d(packet.DecodeF64(), packet.DecodeF64());
    }
    
    internal static Vector2 DecodeVector2f(this BasePacket packet)
    {
        var prologue = packet.DecodePrologue();
        
        if (prologue != (int)PrologueType.EXTENDED_DATA_STREAM_VECTOR2F)
            throw new InvalidDataException($"Expected type {(int)PrologueType.EXTENDED_DATA_STREAM_VECTOR2F} but got {prologue }");

        return new Vector2(packet.DecodeF32(), packet.DecodeF32());
    }
}