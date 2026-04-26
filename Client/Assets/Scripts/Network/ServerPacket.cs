using StateSync.Shared;

namespace StateSync.Client.Network
{
	public struct ServerPacket
	{
		public MessageType Type;
		public ErrorCode Error;
		public int[] ErrorParams;
		public byte[] Data;
	}
}
