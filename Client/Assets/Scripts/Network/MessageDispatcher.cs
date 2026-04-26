using System;
using System.Collections.Generic;
using Google.Protobuf;
using StateSync.Shared;
using UnityEngine;

namespace StateSync.Client.Network
{
	public class MessageDispatcher
	{
		private readonly Dictionary<MessageType, Action<ServerPacket>> _Handlers =
			new Dictionary<MessageType, Action<ServerPacket>>();

		public void Register<T>(MessageType type, Action<T, ErrorCode, int[]> handler)
			where T : IMessage<T>, new()
		{
			MessageParser<T> parser = new MessageParser<T>(() => new T());
			_Handlers[type] = (ServerPacket packet) =>
			{
				T message = packet.Data.Length > 0 ? parser.ParseFrom(packet.Data) : new T();
				handler(message, packet.Error, packet.ErrorParams);
			};
		}

		public void Unregister(MessageType type)
		{
			_Handlers.Remove(type);
		}

		public void Dispatch(ServerPacket packet)
		{
			if (_Handlers.TryGetValue(packet.Type, out Action<ServerPacket> handler))
			{
				try
				{
					handler(packet);
				}
				catch (Exception exception)
				{
					Debug.LogError(
						"[MessageDispatcher] Handler error for " + packet.Type + ": " + exception);
				}
			}
			else
			{
				Debug.LogWarning("[MessageDispatcher] No handler registered for " + packet.Type);
			}
		}

		public void Clear()
		{
			_Handlers.Clear();
		}
	}
}
