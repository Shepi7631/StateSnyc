using StateSync.Client.Movement;
using StateSync.Client.Network;
using StateSync.Client.UI;
using UnityEngine;

namespace StateSync.Client.World
{
	public sealed class WorldManager : MonoBehaviour
	{
		private static WorldManager _Instance;

		private NetworkManager _NetworkManager;
		private UIManager _UIManager;
		private MovementManager _MovementManager;

		public NetworkManager Network { get { return _NetworkManager; } }
		public UIManager UI { get { return _UIManager; } }
		public MovementManager Movement { get { return _MovementManager; } }

		private void Awake()
		{
			if (_Instance != null && _Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			_Instance = this;
			DontDestroyOnLoad(gameObject);

			_NetworkManager = new NetworkManager();
			_NetworkManager.Initialize();

			_UIManager = new UIManager(_NetworkManager);
			_UIManager.Initialize();
		}

		public void InitializeMovement(Pathfinding.Data.NavMesh2D mesh, string localPlayerId, Pathfinding.Data.Vec2 startPos, float speed)
		{
			_MovementManager = new MovementManager(_NetworkManager, mesh);
			_MovementManager.Initialize(localPlayerId, startPos, speed);
		}

		private void Update()
		{
			if (_NetworkManager != null)
				_NetworkManager.Tick();
			if (_UIManager != null)
				_UIManager.Tick(Time.deltaTime);
			if (_MovementManager != null)
				_MovementManager.Tick(Time.deltaTime);
		}

		private void OnDestroy()
		{
			if (_Instance == this)
			{
				_Instance = null;
			}

			if (_MovementManager != null)
			{
				_MovementManager.Dispose();
				_MovementManager = null;
			}

			if (_UIManager != null)
			{
				_UIManager.Dispose();
				_UIManager = null;
			}

			if (_NetworkManager != null)
			{
				_NetworkManager.Dispose();
				_NetworkManager = null;
			}
		}
	}
}
