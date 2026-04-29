namespace StateSync.Server.Network;

using System.Buffers;
using StateSync.Shared;

public static class PacketReader
{
    private const int MaxPayloadSize = 1 * 1024 * 1024; // 1MB

    // headerBuf 由调用方提供并复用（每个连接一个 8 字节缓冲）。
    // 返回的 Data 若 DataLength > 0，则为 ArrayPool 租用的数组，调用方负责归还。
    public static async ValueTask<(MessageType Type, byte[] Data, int DataLength)> ReadClientPacketAsync(
        Stream stream, byte[] headerBuf)
    {
        // 第一步：精确读取 8 字节固定包头
        // 包头格式：[4B MessageType][4B DataLength]
        await ReadExactAsync(stream, headerBuf, 8);
        var type = (MessageType)BitConverter.ToInt32(headerBuf, 0);
        int length = BitConverter.ToInt32(headerBuf, 4);

        // 防御恶意客户端发送异常长度（负数或超大包），避免服务器 OOM
        if (length < 0 || length > MaxPayloadSize)
            throw new InvalidDataException($"Invalid payload length: {length}");
        if (length == 0) return (type, [], 0);

        // 第二步：按包头声明的长度精确读取数据体，不多不少
        byte[] data = ArrayPool<byte>.Shared.Rent(length);
        await ReadExactAsync(stream, data, length);
        return (type, data, length);
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buf, int count)
    {
        // ★ 粘包/拆包处理核心：
        // TCP 是字节流协议，发送方的一次 Write 不保证接收方一次 Read 能收完。
        // 例如发送方发 100 字节，接收方可能分 3 次收到：40 + 35 + 25。
        // 循环累加已读字节数，每次从上次结束位置继续读，直到凑满 count 字节。
        int read = 0;
        while (read < count)
        {
            int n = await stream.ReadAsync(buf.AsMemory(read, count - read));
            if (n == 0) throw new EndOfStreamException("Client disconnected");
            read += n;
        }
    }
}
