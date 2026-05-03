using System.Buffers.Binary;
using System.Runtime.Serialization;

namespace ReVision.EyeDevice.Platform;

public class BasePacket
{
    internal Stream _stream;
    
    private MessageDirection _direction;
    private uint _packetLength;
    private uint _messageType;
    private uint _messageId;
    private OpCode _opCode;
    private uint _statusCode;
    private uint _bodyLength;
    
    public uint MessageId => _messageId;
    public OpCode OpCode => _opCode;
    public uint StatusCode => _statusCode;
    
    internal BasePacket(uint messageId, OpCode opCode)
    {
        _stream = new MemoryStream();
        
        _messageId = messageId;
        _messageType = 81; // ???
        _opCode = opCode;
        
        WriteHeader();
    }
    
    public BasePacket(Stream stream)
    {
        _stream = stream;
        
        _direction = (MessageDirection)this.ReadUInt32LE();
        _packetLength = this.ReadUInt32LE();
        _messageType = this.ReadUInt32BE();
        _messageId = this.ReadUInt32BE();
        _ = this.ReadUInt32BE();
        _opCode = (OpCode)this.ReadUInt32BE();
        _statusCode = this.ReadUInt32BE();
        _bodyLength = this.ReadUInt32BE();

        if (Available(2))
            _ = this.ReadUInt16BE();
    }

    public static bool IsFullPacket(byte[] packet, out uint packetLength)
    {
        packetLength = BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan()[4..]);
        return packetLength == packet.Length;
    }

    private bool Available(uint byteAmount)
    {
        return _stream.Length - _stream.Position >= byteAmount;
    }

    private void WriteHeader()
    {
        _stream.Seek(0, SeekOrigin.Begin);
        
        this.WriteUInt32LE((uint)_direction);
        this.WriteUInt32LE(_packetLength);
        this.WriteUInt32BE(_messageType);
        this.WriteUInt32BE(_messageId);
        this.WriteUInt32BE(0);
        this.WriteUInt32BE((uint)_opCode);
        this.WriteUInt32BE(_statusCode);
        this.WriteUInt32BE(_bodyLength);
        this.WriteUInt16BE(0);
    }

    public byte[] Encode()
    {
        var headerLength = (uint)_stream.Position;
        
        IBinaryOutput binaryOutput = (IBinaryOutput)this;
        if (!binaryOutput.Encode())
            throw new SerializationException("Can't encode packet");

        uint streamPosition = (uint)_stream.Position;

        _packetLength = streamPosition - 8;
        _bodyLength = (_packetLength - headerLength) + 10;
        
        WriteHeader();
        _stream.Seek(0, SeekOrigin.Begin);
        
        byte[] packet = new byte[streamPosition];
        _stream.ReadExactly(packet, 0, (int)streamPosition);

        return packet;
    }
}