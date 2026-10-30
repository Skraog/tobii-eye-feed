using System.Buffers.Binary;

namespace ReVision.EyeDevice.Platform;

public class BasePacket
{
    internal readonly Stream _stream;
    private readonly bool _outgoing;
    public uint MessageId { get; }
    public OpCode OpCode { get; }
    public uint StatusCode { get; }
    public uint MessageType { get; }

    internal BasePacket(uint messageId, OpCode opCode)
    {
        _stream = new MemoryStream();
        _outgoing = true;
        MessageId = messageId;
        OpCode = opCode;
    }

    public BasePacket(Stream stream)
    {
        _stream = stream;
        if (stream.Length is < 32 or > PacketFramer.MaxPacketBytes)
            throw new InvalidDataException("Invalid packet size.");
        _ = this.ReadUInt32LE(); // Direction.
        uint length = this.ReadUInt32LE();
        if (length != stream.Length)
            throw new InvalidDataException("Packet length does not match its transport header.");
        MessageType = this.ReadUInt32BE();
        MessageId = this.ReadUInt32BE();
        _ = this.ReadUInt32BE();
        OpCode = (OpCode)this.ReadUInt32BE();
        StatusCode = this.ReadUInt32BE();
        uint bodyLength = this.ReadUInt32BE();
        if (bodyLength > stream.Length - stream.Position)
            throw new InvalidDataException("Packet body extends beyond the transport frame.");
        if (bodyLength >= 2) _ = this.ReadUInt16BE();
    }

    public byte[] Encode()
    {
        if (!_outgoing || this is not IBinaryOutput body)
            throw new InvalidOperationException("Only command packets can be encoded.");
        _stream.SetLength(34);
        _stream.Position = 34;
        body.WriteBody();
        int length = checked((int)_stream.Position);
        byte[] packet = ((MemoryStream)_stream).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(packet, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), (uint)length - 8);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(8), 81);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(12), MessageId);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(20), (uint)OpCode);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(28), (uint)length - 32);
        return packet;
    }
}
