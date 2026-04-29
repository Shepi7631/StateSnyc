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

        public NetworkManager Network => _NetworkManager;
        public UIManager UI => _UIManager;

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

        private void Update()
        {
            _NetworkManager?.Tick();
            _UIManager?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (_Instance == this)
            {
                _Instance = null;
            }

            _UIManager?.Dispose();
            _UIManager = null;

            _NetworkManager?.Dispose();
            _NetworkManager = null;
        }
    }
}
