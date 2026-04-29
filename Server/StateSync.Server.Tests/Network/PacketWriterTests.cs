namespace StateSync.Server.Tests.Network;

using System.Buffers;
using Xunit;
using StateSync.Server.Network;
using StateSync.Shared;

public class PacketWriterTests
{
    [Fact]
    public void WriteServerPacket_Success_NoParams_CorrectLayout()
    {
        byte[] data = [10, 20, 30];
        var (buffer, length) = PacketWriter.WriteServerPacket(MessageType.JoinRoom, ErrorCode.Success, [], data);
        try
        {
            Assert.Equal(1, BitConverter.ToInt32(buffer, 0));   // type = JoinRoom
            Assert.Equal(3, BitConverter.ToInt32(buffer, 4));   // data length
            Assert.Equal(0, BitConverter.ToInt32(buffer, 8));   // error code = SUCCESS
            Assert.Equal(0, BitConverter.ToInt32(buffer, 12));  // param count = 0
            Assert.Equal(data, buffer[16..length]);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    [Fact]
    public void WriteServerPacket_RoomFull_OneParam_CorrectLayout()
    {
        var (buffer, length) = PacketWriter.WriteServerPacket(MessageType.JoinRoom, ErrorCode.RoomFull, [16], []);
        try
        {
            Assert.Equal(101, BitConverter.ToInt32(buffer, 8));  // ROOM_FULL = 101
            Assert.Equal(1, BitConverter.ToInt32(buffer, 12));   // param count = 1
            Assert.Equal(16, BitConverter.ToInt32(buffer, 16));  // param[0] = max players
            Assert.Equal(20, length);                            // 16-byte header + 1x4-byte param
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
}
