using System.Buffers.Binary;
using ReVision.EyeDevice.Platform;
using ReVision.EyeDevice.Platform.Client;
using ReVision.EyeDevice.Platform.Server;
using ReVision.Vive;

namespace ReVision.Tests;

public sealed class ProtocolTests
{
    internal static byte[] Packet(byte[]? body = null)
    {
        body ??= [];
        byte[] packet = new byte[34 + body.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), (uint)packet.Length);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(28), (uint)body.Length + 2);
        body.CopyTo(packet, 34);
        return packet;
    }

    [Fact]
    public void FramerHandlesPartialHeadersAndBodies()
    {
        byte[] packet = Packet(new byte[40000]);
        for (int split = 0; split < packet.Length; split += 127)
        {
            var framer = new PacketFramer();
            var received = new List<byte[]>();
            framer.Append(packet.AsSpan(0, split), p => received.Add(p.ToArray()));
            Assert.Empty(received);
            framer.Append(packet.AsSpan(split), p => received.Add(p.ToArray()));
            Assert.Equal(packet, Assert.Single(received));
        }
        for (int split = 1; split <= 8; split++)
        {
            var framer = new PacketFramer();
            int count = 0;
            framer.Append(packet.AsSpan(0, split), _ => count++);
            framer.Append(packet.AsSpan(split), _ => count++);
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public void ReopenedEndpointSynchronizesOnUpgradeResponseAcrossReads()
    {
        byte[] response = Convert.FromHexString("010000002B000000000000520000000100000001000003E8000000000000000B0000020000000400010008");
        Assert.Equal(43, response.Length);
        var framer = new PacketFramer(synchronizeOnUpgrade: true);
        var received = new List<byte[]>();
        byte[] oldImage = Enumerable.Repeat((byte)0xff, 16000).ToArray();
        framer.Append(oldImage, p => received.Add(p.ToArray()));
        framer.Append(response.AsSpan(0, 21), p => received.Add(p.ToArray()));
        Assert.Empty(received);
        framer.Append(response.AsSpan(21), p => received.Add(p.ToArray()));
        Assert.Equal(response, Assert.Single(received));
        Assert.Equal(65544u, new SUpgradeProtocol(new MemoryStream(response)).ProtocolVersion);
    }

    [Fact]
    public void FramerHandlesCoalescedPacketsAndPartialNextHeader()
    {
        byte[] packet = Packet();
        byte[] combined = [.. packet, .. packet, .. packet];
        var framer = new PacketFramer();
        int count = 0;
        framer.Append(combined.AsSpan(0, packet.Length * 2 + 3), _ => count++);
        Assert.Equal(2, count);
        framer.Append(combined.AsSpan(packet.Length * 2 + 3), p => { Assert.Equal(packet.Length, p.Length); count++; });
        Assert.Equal(3, count);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(23u)]
    [InlineData(uint.MaxValue)]
    [InlineData((uint)PacketFramer.MaxPacketBytes + 1)]
    public void FramerRejectsInvalidAdvertisedLengths(uint length)
    {
        byte[] header = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), length);
        Assert.Throws<InvalidDataException>(() => new PacketFramer().Append(header, _ => { }));
    }

    [Fact]
    public void IncomingPacketRejectsLengthMismatchAndTruncatedBody()
    {
        byte[] packet = Packet();
        Assert.Throws<InvalidDataException>(() => new BasePacket(new MemoryStream(packet[..^1])));
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(28), 100);
        Assert.Throws<InvalidDataException>(() => new BasePacket(new MemoryStream(packet)));
    }

    [Fact]
    public void EmptyCommandHasKnownWireRepresentationAndCanEncodeTwice()
    {
        var command = new CUnitInformation(0x12345678);
        byte[] expected = Convert.FromHexString("000000001A0000000000005112345678000000000000058C00000000000000020000");
        Assert.Equal(expected, command.Encode());
        Assert.Equal(expected, command.Encode());
    }

    [Fact]
    public void BlobRoundTripsAndMalformedLengthIsRejectedBeforeAllocation()
    {
        byte[] packet = Packet([0x15, 0, 0, 0, 7, 0, 0, 0, 3, 1, 2, 3]);
        var decoded = new BasePacket(new MemoryStream(packet));
        Assert.Equal(new byte[] { 1, 2, 3 }, decoded.DecodeBlob());
        packet[42] = 0xff;
        Assert.Throws<InvalidDataException>(() => new BasePacket(new MemoryStream(packet)).DecodeBlob());
    }

    [Fact]
    public void StringRejectsInconsistentLength()
    {
        byte[] packet = Packet([0x14, 0, 0, 0, 5, 0, 0, 0, 2, 65, 66]);
        Assert.Throws<InvalidDataException>(() => new BasePacket(new MemoryStream(packet)).DecodeString());
    }

    [Fact]
    public void UnknownImageColumnFailsWithoutMisreadingNextValue()
    {
        byte[] packet = Packet([5, 0, 0, 0, 4, 0, 1, 0x0b, 0xb8,
            5, 0, 0, 0, 4, 0, 2, 0x0b, 0xb9, 2, 0, 0, 0, 4, 0, 0, 3, 0xe7]);
        Assert.Throws<InvalidDataException>(() => new SWearableLimitedImage(new MemoryStream(packet)));
    }

    [Fact]
    public void KeepAliveMatchesIndependentCrcFixture()
    {
        byte[] report = ViveDevice.CreateKeepAlive("TEST-SERIAL"u8, 0x1234);
        Assert.Equal(64, report.Length);
        Assert.Equal(new byte[] { 4, 0x78, 0x29, 0x38 }, report[..4]);
        Assert.Equal(8, report[10]);
        Assert.Equal(new byte[] { 195, 143, 107 }, report[47..50]);
        Assert.All(report[50..], b => Assert.Equal(0, b));
    }
}
