using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Google.Protobuf;
using StateSync.Shared;
using UnityEngine;

namespace StateSync.Client.Network
{
	public class NetworkClient
	{
		private TcpClient _TcpClient;
		private NetworkStream _Stream;
		private Thread _ReceiveThread;
		private volatile bool _IsRunning;
		private readonly object _SendLock = new object();
		private readonly ConcurrentQueue<ServerPacket> _ReceiveQueue = new ConcurrentQueue<ServerPacket>();

		public bool IsConnected
		{
			get { return _TcpClient != null && _TcpClient.Connected; }
		}

		public event Action OnDisconnected;

		public void Connect(string host, int port)
		{
			if (IsConnected)
				throw new InvalidOperationException("Already connected");

			_TcpClient = new TcpClient();
			try
			{
				_TcpClient.Connect(host, port);
			}
			catch
			{
				_TcpClient.Close();
				_TcpClient = null;
				throw;
			}
			_Stream = _TcpClient.GetStream();
			_IsRunning = true;

			_ReceiveThread = new Thread(ReceiveLoop);
			_ReceiveThread.IsBackground = true;
			_ReceiveThread.Start();

			Debug.Log("[NetworkClient] Connected to " + host + ":" + port);
		}

		public void Disconnect()
		{
			_IsRunning = false;

			if (_Stream != null)
			{
				_Stream.Close();
				_Stream = null;
			}

			if (_TcpClient != null)
			{
				_TcpClient.Close();
				_TcpClient = null;
			}

			Debug.Log("[NetworkClient] Disconnected");
		}

		public void Send(MessageType type, IMessage message)
		{
			if (!IsConnected)
				throw new InvalidOperationException("Not connected");

			byte[] payload = message.ToByteArray();
			byte[] packet = PacketWriter.WriteClientPacket(type, payload);

			lock (_SendLock)
			{
				_Stream.Write(packet, 0, packet.Length);
				_Stream.Flush();
			}
		}

		public bool TryDequeue(out ServerPacket packet)
		{
			return _ReceiveQueue.TryDequeue(out packet);
		}

		private void ReceiveLoop()
		{
			try
			{
				while (_IsRunning)
				{
					ServerPacket packet = PacketReader.ReadServerPacket(_Stream);
					_ReceiveQueue.Enqueue(packet);
				}
			}
			catch (EndOfStreamException)
			{
				Debug.Log("[NetworkClient] Server closed connection");
			}
			catch (IOException)
			{
				if (_IsRunning)
					Debug.LogWarning("[NetworkClient] Connection lost");
			}
			catch (InvalidDataException ex)
			{
				if (_IsRunning)
					Debug.LogWarning("[NetworkClient] Invalid data from server: " + ex.Message);
			}
			catch (ObjectDisposedException)
			{
			}
			finally
			{
				_IsRunning = false;
				OnDisconnected?.Invoke();
			}
		}
	}
}
