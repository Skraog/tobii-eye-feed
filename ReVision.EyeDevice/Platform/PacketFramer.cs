using System.Buffers.Binary;

namespace ReVision.EyeDevice.Platform;

// Tracker responses include the eight-byte transport prefix in their length.
// Host commands use a different convention; see BasePacket.Encode.
public sealed class PacketFramer
{
    public const int MaxPacketBytes = 2 * 1024 * 1024;
    private byte[] _buffer = new byte[64 * 1024];
    private int _length;
    private bool _synchronizing;

    public PacketFramer(bool synchronizeOnUpgrade = false) => _synchronizing = synchronizeOnUpgrade;

    public void Append(ReadOnlySpan<byte> bytes, Action<ReadOnlyMemory<byte>> receive)
    {
        ArgumentNullException.ThrowIfNull(receive);
        if (bytes.Length > 64 * 1024 || _length + bytes.Length > MaxPacketBytes + 64 * 1024)
            throw new InvalidDataException("USB receive buffer exceeds the packet size limit.");
        if (_buffer.Length < _length + bytes.Length)
            Array.Resize(ref _buffer, Math.Max(_length + bytes.Length, Math.Min(_buffer.Length * 2, MaxPacketBytes + 64 * 1024)));
        bytes.CopyTo(_buffer.AsSpan(_length));
        _length += bytes.Length;
        if (_synchronizing)
        {
            // A reopened endpoint can start in the middle of the previous session's image.
            // Ignore that stream until the response to this session's first command arrives.
            int start = 0;
            while (_length - start >= 32 && !IsUpgradeResponse(_buffer.AsSpan(start, 32))) start++;
            if (start > 0)
            {
                _length -= start;
                _buffer.AsSpan(start, _length).CopyTo(_buffer);
            }
            if (_length < 32) return;
            _synchronizing = false;
        }
        int offset = 0;
        while (_length - offset >= 8)
        {
            uint advertisedLength = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.AsSpan(offset + 4, 4));
            if (advertisedLength is < 32 or > MaxPacketBytes)
                throw new InvalidDataException($"Invalid USB packet length: {advertisedLength}.");
            int totalLength = (int)advertisedLength;
            if (_length - offset < totalLength) break;
            // The memory is valid only during this synchronous callback.
            receive(_buffer.AsMemory(offset, totalLength));
            offset += totalLength;
        }
        if (offset == 0) return;
        _length -= offset;
        _buffer.AsSpan(offset, _length).CopyTo(_buffer);
    }

    private static bool IsUpgradeResponse(ReadOnlySpan<byte> header)
    {
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
        return BinaryPrimitives.ReadUInt32LittleEndian(header) == 1
            && length is >= 34 and <= MaxPacketBytes
            && BinaryPrimitives.ReadUInt32BigEndian(header[8..]) == 82
            && BinaryPrimitives.ReadUInt32BigEndian(header[12..]) == 1
            && BinaryPrimitives.ReadUInt32BigEndian(header[20..]) == (uint)OpCode.PROTOCOL_UPGRADE
            && BinaryPrimitives.ReadUInt32BigEndian(header[28..]) == length - 32;
    }
}
