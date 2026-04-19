namespace StateSync.Server.Network;

using StateSync.Shared;

public static class PacketWriter
{
    public static byte[] WriteServerPacket(MessageType type, ErrorCode errorCode, int[] errorParams, byte[] data)
    {
        int headerSize = 16 + errorParams.Length * 4;
        byte[] packet = new byte[headerSize + data.Length];
        int offset = 0;

        Write(packet, ref offset, (int)type);
        Write(packet, ref offset, data.Length);
        Write(packet, ref offset, (int)errorCode);
        Write(packet, ref offset, errorParams.Length);
        foreach (var p in errorParams)
            Write(packet, ref offset, p);
        data.CopyTo(packet, offset);

        return packet;
    }

    private static void Write(byte[] buf, ref int offset, int value)
    {
        BitConverter.TryWriteBytes(buf.AsSpan(offset), value);
        offset += 4;
    }
}
