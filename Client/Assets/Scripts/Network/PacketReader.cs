using System;
using System.IO;
using System.Net.Sockets;
using StateSync.Shared;

namespace StateSync.Client.Network
{
	public static class PacketReader
	{
		private const int MaxPayloadSize = 1 * 1024 * 1024;

		public static ServerPacket ReadServerPacket(NetworkStream stream)
		{
			byte[] header = ReadExact(stream, 16);

			MessageType type = (MessageType)BitConverter.ToInt32(header, 0);
			int dataLength = BitConverter.ToInt32(header, 4);
			ErrorCode errorCode = (ErrorCode)BitConverter.ToInt32(header, 8);
			int errorParamCount = BitConverter.ToInt32(header, 12);

			if (dataLength < 0 || dataLength > MaxPayloadSize)
				throw new InvalidDataException("Invalid payload length: " + dataLength);
			if (errorParamCount < 0 || errorParamCount > 64)
				throw new InvalidDataException("Invalid error param count: " + errorParamCount);

			int[] errorParams = new int[errorParamCount];
			if (errorParamCount > 0)
			{
				byte[] paramBytes = ReadExact(stream, errorParamCount * 4);
				for (int i = 0; i < errorParamCount; i++)
				{
					errorParams[i] = BitConverter.ToInt32(paramBytes, i * 4);
				}
			}

			byte[] data = dataLength > 0 ? ReadExact(stream, dataLength) : Array.Empty<byte>();

			return new ServerPacket
			{
				Type = type,
				Error = errorCode,
				ErrorParams = errorParams,
				Data = data
			};
		}

		private static byte[] ReadExact(NetworkStream stream, int count)
		{
			byte[] buffer = new byte[count];
			int offset = 0;
			while (offset < count)
			{
				int bytesRead = stream.Read(buffer, offset, count - offset);
				if (bytesRead == 0)
					throw new EndOfStreamException("Server disconnected");
				offset += bytesRead;
			}
			return buffer;
		}
	}
}
