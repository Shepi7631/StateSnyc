using System;
using System.Collections.Generic;
using Pathfinding.Data;
using StateSync.Client.Network;
using StateSync.Shared;

namespace StateSync.Client.Movement
{
	public class MovementManager : IDisposable
	{
		private readonly NetworkManager _Network;
		private readonly NavMesh2D _Mesh;

		private LocalPlayerController _LocalPlayer;
		private readonly Dictionary<string, RemotePlayerController> _RemotePlayers = new Dictionary<string, RemotePlayerController>();
		private string _LocalPlayerId;

		public LocalPlayerController LocalPlayer { get { return _LocalPlayer; } }
		public IReadOnlyDictionary<string, RemotePlayerController> RemotePlayers { get { return _RemotePlayers; } }

		public MovementManager(NetworkManager network, NavMesh2D mesh)
		{
			_Network = network;
			_Mesh = mesh;
		}

		public void Initialize(string localPlayerId, Vec2 startPos, float speed)
		{
			_LocalPlayerId = localPlayerId;
			_LocalPlayer = new LocalPlayerController(_Network, _Mesh, startPos, speed);

			_Network.RegisterHandler<MoveEvent>(MessageType.MoveEvent, OnMoveEvent);
			_Network.RegisterHandler<PositionSync>(MessageType.PositionSync, OnPositionSync);
		}

		public void AddRemotePlayer(string playerId, Vec2 startPos)
		{
			if (playerId == _LocalPlayerId)
				return;
			_RemotePlayers[playerId] = new RemotePlayerController(playerId, _Mesh, startPos, _Network.Rtt);
		}

		public void RemoveRemotePlayer(string playerId)
		{
			_RemotePlayers.Remove(playerId);
		}

		public void Tick(float deltaTime)
		{
			if (_LocalPlayer != null)
				_LocalPlayer.Tick(deltaTime);
			foreach (var remote in _RemotePlayers.Values)
				remote.Tick(deltaTime);
		}

		public void Dispose()
		{
			_Network.UnregisterHandler(MessageType.MoveEvent);
			_Network.UnregisterHandler(MessageType.PositionSync);
		}

		private void OnMoveEvent(MoveEvent evt, ErrorCode error, int[] errorParams)
		{
			if (error != ErrorCode.Success)
				return;

			if (evt.PlayerId == _LocalPlayerId)
			{
				_LocalPlayer.Speed = evt.Speed;
				return;
			}

			RemotePlayerController remote;
			if (_RemotePlayers.TryGetValue(evt.PlayerId, out remote))
				remote.OnMoveEvent(evt);
		}

		private void OnPositionSync(PositionSync sync, ErrorCode error, int[] errorParams)
		{
			if (error != ErrorCode.Success)
				return;

			if (sync.PlayerId == _LocalPlayerId)
			{
				if (_LocalPlayer != null)
					_LocalPlayer.OnPositionSync(sync);
				return;
			}

			RemotePlayerController remote;
			if (_RemotePlayers.TryGetValue(sync.PlayerId, out remote))
				remote.OnPositionSync(sync);
		}
	}
}
