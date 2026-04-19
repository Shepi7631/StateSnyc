namespace StateSync.Server.Network;

using StateSync.Shared;

public static class PacketReader
{
    private const int MaxPayloadSize = 1 * 1024 * 1024; // 1MB

    public static async Task<(MessageType Type, byte[] Data)> ReadClientPacketAsync(Stream stream)
    {
        byte[] header = await ReadExactAsync(stream, 8);
        var type = (MessageType)BitConverter.ToInt32(header, 0);
        int length = BitConverter.ToInt32(header, 4);
        if (length < 0 || length > MaxPayloadSize)
            throw new InvalidDataException($"Invalid payload length: {length}");
        byte[] data = length > 0 ? await ReadExactAsync(stream, length) : [];
        return (type, data);
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int count)
    {
        byte[] buf = new byte[count];
        int read = 0;
        while (read < count)
        {
            int n = await stream.ReadAsync(buf.AsMemory(read, count - read));
            if (n == 0) throw new EndOfStreamException("Client disconnected");
            read += n;
        }
        return buf;
    }
}
