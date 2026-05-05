using System;
using Google.Protobuf;
using StateSync.Shared;

namespace StateSync.Client.Network
{
    public sealed class NetworkManager : IDisposable
    {
        private NetworkClient _Client;
        private MessageDispatcher _Dispatcher;
        private bool _NotifyDisconnectOnMainThread;
        private bool _Initialized;

        public RttTracker Rtt { get; private set; }

        public bool IsConnected
        {
            get { return _Client != null && _Client.IsConnected; }
        }

        public event Action OnDisconnected;

        public void Initialize()
        {
            if (_Initialized)
            {
                return;
            }
            _Client = new NetworkClient();
            _Dispatcher = new MessageDispatcher();
            _Client.OnDisconnected += HandleDisconnected;
            _Initialized = true;
            Rtt = new RttTracker();
            _Dispatcher.Register<Ping>(MessageType.Ping, HandlePing);
        }

        public void Tick()
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

        public void Dispose()
        {
            if (!_Initialized)
            {
                return;
            }

            Disconnect();

            if (_Client != null)
            {
                _Client.OnDisconnected -= HandleDisconnected;
            }

            _Dispatcher?.Clear();
            _Client = null;
            _Dispatcher = null;
            Rtt = null;
            _NotifyDisconnectOnMainThread = false;
            _Initialized = false;
        }

        public void Connect(string host, int port)
        {
            EnsureInitialized();
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
            EnsureInitialized();
            _Client.Send(type, message);
        }

        public void RegisterHandler<T>(MessageType type, Action<T, ErrorCode, int[]> handler)
            where T : IMessage<T>, new()
        {
            EnsureInitialized();
            _Dispatcher.Register(type, handler);
        }

        public void UnregisterHandler(MessageType type)
        {
            EnsureInitialized();
            _Dispatcher.Unregister(type);
        }

        private void HandleDisconnected()
        {
            _NotifyDisconnectOnMainThread = true;
        }

        private void HandlePing(Ping ping, ErrorCode error, int[] errorParams)
        {
            Send(MessageType.Pong, new Pong
            {
                ServerTimestamp = ping.ServerTimestamp,
                Sequence = ping.Sequence
            });

            Rtt.UpdateRtt((int)ping.YourRttMs);
        }

        private void EnsureInitialized()
        {
            if (!_Initialized || _Client == null || _Dispatcher == null)
            {
                throw new InvalidOperationException(
                    "NetworkManager is not initialized. Call Initialize() before use.");
            }
        }
    }
}
