using System;
using Google.Protobuf;
using StateSync.Shared;
using UnityEngine;

namespace StateSync.Client.Network
{
	public class NetworkManager : MonoBehaviour
	{
		private static NetworkManager _Instance;

		public static NetworkManager Instance
		{
			get { return _Instance; }
		}

		private NetworkClient _Client;
		private MessageDispatcher _Dispatcher;
		private bool _NotifyDisconnectOnMainThread;

		public bool IsConnected
		{
			get { return _Client != null && _Client.IsConnected; }
		}

		public event Action OnDisconnected;

		private void Awake()
		{
			if (_Instance != null && _Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			_Instance = this;
			DontDestroyOnLoad(gameObject);

			_Client = new NetworkClient();
			_Dispatcher = new MessageDispatcher();
			_Client.OnDisconnected += HandleDisconnected;
		}

		private void Update()
		{
			if (_NotifyDisconnectOnMainThread)
			{
				_NotifyDisconnectOnMainThread = false;
				OnDisconnected?.Invoke();
			}

			ServerPacket packet;
			while (_Client != null && _Client.TryDequeue(out packet))
			{
				_Dispatcher.Dispatch(packet);
			}
		}

		private void OnDestroy()
		{
			if (_Instance == this)
			{
				_Instance = null;
			}
			Disconnect();
			if (_Client != null)
			{
				_Client.OnDisconnected -= HandleDisconnected;
			}
			_Dispatcher?.Clear();
		}

		public void Connect(string host, int port)
		{
			_Client.Connect(host, port);
		}

		public void Disconnect()
		{
			if (_Client != null && _Client.IsConnected)
			{
				_Client.Disconnect();
			}
		}

		public void Send<T>(MessageType type, T message) where T : IMessage<T>
		{
			_Client.Send(type, message);
		}

		public void RegisterHandler<T>(MessageType type, Action<T, ErrorCode, int[]> handler)
			where T : IMessage<T>, new()
		{
			_Dispatcher.Register(type, handler);
		}

		public void UnregisterHandler(MessageType type)
		{
			_Dispatcher.Unregister(type);
		}

		private void HandleDisconnected()
		{
			_NotifyDisconnectOnMainThread = true;
		}
	}
}
