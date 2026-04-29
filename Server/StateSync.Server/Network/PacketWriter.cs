namespace StateSync.Server.Network;

using System.Buffers;
using StateSync.Shared;

public static class PacketWriter
{
    // 返回 ArrayPool 租用的缓冲，调用方负责 ArrayPool<byte>.Shared.Return(buffer)
    // S→C 帧格式：[4B type][4B dataLen][4B errorCode][4B paramCount][M×4B params][N bytes protobuf]
    public static (byte[] Buffer, int Length) WriteServerPacket(
        MessageType type, ErrorCode errorCode, int[] errorParams, byte[] data)
    {
        // 16 = 固定包头 4 个 int；errorParams 为可变长度错误参数列表
        int length = 16 + errorParams.Length * 4 + data.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
        int offset = 0;

        Write(buffer, ref offset, (int)type);
        Write(buffer, ref offset, data.Length);
        Write(buffer, ref offset, (int)errorCode);
        Write(buffer, ref offset, errorParams.Length);
        foreach (var p in errorParams)
            Write(buffer, ref offset, p);
        // data 是变长 protobuf 字节，CopyTo 批量复制到包头之后
        data.CopyTo(buffer, offset);

        return (buffer, length);
    }

    // ref offset 游标：每次写完 4 字节自动推进，调用处无需手动计算偏移
    private static void Write(byte[] buf, ref int offset, int value)
    {
        BitConverter.TryWriteBytes(buf.AsSpan(offset), value);
        offset += 4;
    }
}
