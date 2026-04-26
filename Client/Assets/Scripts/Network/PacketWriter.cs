using System;
using StateSync.Shared;

namespace StateSync.Client.Network
{
	public static class PacketWriter
	{
		public static byte[] WriteClientPacket(MessageType type, byte[] data)
		{
			byte[] packet = new byte[8 + data.Length];
			Buffer.BlockCopy(BitConverter.GetBytes((int)type), 0, packet, 0, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(data.Length), 0, packet, 4, 4);
			Buffer.BlockCopy(data, 0, packet, 8, data.Length);
			return packet;
		}
	}
}
