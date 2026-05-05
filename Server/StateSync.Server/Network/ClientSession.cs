namespace StateSync.Server.Network;

using System.Buffers;
using System.Net.Sockets;
using StateSync.Server.Game;
using StateSync.Shared;

public class ClientSession : IClientSession
{
	private readonly NetworkStream _stream;
	private readonly object _writeLock = new();

	public string? PlayerId { get; set; }
	public int SmoothedRtt { get; set; }
	public Room? Room { get; set; }

	public ClientSession(NetworkStream stream)
	{
		_stream = stream;
	}

	public void Send(MessageType type, byte[] data)
	{
		var (buffer, length) = PacketWriter.WriteServerPacket(type, ErrorCode.Success, [], data);
		try
		{
			lock (_writeLock)
			{
				_stream.Write(buffer, 0, length);
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}
}
