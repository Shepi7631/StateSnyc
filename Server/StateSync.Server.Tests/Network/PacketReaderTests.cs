namespace StateSync.Server.Tests.Network;

using System.Buffers;
using Xunit;
using StateSync.Server.Network;
using StateSync.Shared;

public class PacketReaderTests
{
    [Fact]
    public async Task ReadClientPacketAsync_ValidPacket_ReturnsTypeAndData()
    {
        byte[] payload = [1, 2, 3];
        byte[] packet = BuildClientPacket(MessageType.JoinRoom, payload);
        using var stream = new MemoryStream(packet);

        var (type, data, dataLength) = await PacketReader.ReadClientPacketAsync(stream, new byte[8]);
        try
        {
            Assert.Equal(MessageType.JoinRoom, type);
            Assert.Equal(payload, data[..dataLength]);
        }
        finally
        {
            if (dataLength > 0) ArrayPool<byte>.Shared.Return(data);
        }
    }

    [Fact]
    public async Task ReadClientPacketAsync_EmptyData_ReturnsEmptyArray()
    {
        byte[] packet = BuildClientPacket(MessageType.JoinRoom, []);
        using var stream = new MemoryStream(packet);

        var (_, data, dataLength) = await PacketReader.ReadClientPacketAsync(stream, new byte[8]);

        Assert.Equal(0, dataLength);
        Assert.Empty(data);
    }

    [Fact]
    public async Task ReadClientPacketAsync_StreamClosed_ThrowsEndOfStreamException()
    {
        using var stream = new MemoryStream([]);

        await Assert.ThrowsAsync<EndOfStreamException>(
            () => PacketReader.ReadClientPacketAsync(stream, new byte[8]).AsTask());
    }

    private static byte[] BuildClientPacket(MessageType type, byte[] data)
    {
        byte[] packet = new byte[8 + data.Length];
        BitConverter.TryWriteBytes(packet.AsSpan(0), (int)type);
        BitConverter.TryWriteBytes(packet.AsSpan(4), data.Length);
        data.CopyTo(packet, 8);
        return packet;
    }
}
