namespace StateSync.Server.Tests.Network;

using Xunit;
using StateSync.Server.Network;
using StateSync.Shared;

public class PacketWriterTests
{
    [Fact]
    public void WriteServerPacket_Success_NoParams_CorrectLayout()
    {
        byte[] data = [10, 20, 30];
        byte[] packet = PacketWriter.WriteServerPacket(MessageType.JoinRoom, ErrorCode.Success, [], data);

        Assert.Equal(1, BitConverter.ToInt32(packet, 0));   // type = JoinRoom
        Assert.Equal(3, BitConverter.ToInt32(packet, 4));   // data length
        Assert.Equal(0, BitConverter.ToInt32(packet, 8));   // error code = SUCCESS
        Assert.Equal(0, BitConverter.ToInt32(packet, 12));  // param count = 0
        Assert.Equal(data, packet[16..]);
    }

    [Fact]
    public void WriteServerPacket_RoomFull_OneParam_CorrectLayout()
    {
        byte[] packet = PacketWriter.WriteServerPacket(MessageType.JoinRoom, ErrorCode.RoomFull, [16], []);

        Assert.Equal(101, BitConverter.ToInt32(packet, 8));  // ROOM_FULL = 101
        Assert.Equal(1, BitConverter.ToInt32(packet, 12));   // param count = 1
        Assert.Equal(16, BitConverter.ToInt32(packet, 16));  // param[0] = max players
        Assert.Equal(20, packet.Length);                     // 16-byte header + 1x4-byte param
    }
}
