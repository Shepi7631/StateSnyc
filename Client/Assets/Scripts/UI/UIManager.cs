using System;
using StateSync.Client.Network;
using UnityEngine;

namespace StateSync.Client.UI
{
    public sealed class UIManager : IDisposable
    {
        private readonly NetworkManager _NetworkManager;
        private RoomUIReferences _UIReferences;
        private RoomUIController _RoomUIController;
        private bool _Initialized;

        public UIManager(NetworkManager networkManager)
        {
            _NetworkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
        }

        public void Initialize()
        {
            if (_Initialized)
            {
                return;
            }

            RoomUIBuilder builder = new RoomUIBuilder();
            _UIReferences = builder.Build();

            _RoomUIController = new RoomUIController();
            _RoomUIController.Initialize(_NetworkManager, _UIReferences);

            _Initialized = true;
        }

        public void Tick(float deltaTime)
        {
            if (!_Initialized)
            {
                return;
            }

            _RoomUIController.Tick(deltaTime);
        }

        public void Dispose()
        {
            if (!_Initialized)
            {
                return;
            }

            _RoomUIController?.Dispose();

            if (_UIReferences != null)
            {
                if (_UIReferences.RootObject != null)
                {
                    UnityEngine.Object.Destroy(_UIReferences.RootObject);
                }

                if (_UIReferences.CreatedEventSystem && _UIReferences.EventSystemObject != null)
                {
                    UnityEngine.Object.Destroy(_UIReferences.EventSystemObject);
                }
            }

            _RoomUIController = null;
            _UIReferences = null;
            _Initialized = false;
        }
    }
}
