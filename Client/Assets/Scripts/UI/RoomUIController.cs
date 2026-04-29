using System;
using System.Net.Sockets;
using StateSync.Client.Network;
using StateSync.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace StateSync.Client.UI
{
    public sealed class RoomUIController
    {
        private NetworkManager _NetworkManager;

        private GameObject _LoginPanel;
        private InputField _NicknameInput;
        private InputField _HostInput;
        private InputField _PortInput;
        private Button     _ConnectButton;
        private Text       _LoginErrorText;

        private GameObject _LobbyPanel;
        private InputField _RoomIdInput;
        private Button     _JoinButton;
        private Button     _CreateButton;
        private Text       _LobbyErrorText;

        private GameObject _RoomPanel;
        private Text       _RoomIdText;
        private Text       _PlayerCountText;
        private Button     _LeaveButton;

        private string _Nickname;
        private string _PlayerId;
        private string _CurrentRoomId;
        private int    _PlayerCount;
        private float _LobbyErrorRemainingSeconds;
        private float _LoginErrorRemainingSeconds;

        public void Initialize(NetworkManager networkManager, RoomUIReferences ui)
        {
            if (networkManager == null) throw new ArgumentNullException(nameof(networkManager));
            if (ui == null) throw new ArgumentNullException(nameof(ui));

            _NetworkManager = networkManager;
            _LoginPanel = ui.LoginPanel;
            _NicknameInput = ui.NicknameInput;
            _HostInput = ui.HostInput;
            _PortInput = ui.PortInput;
            _ConnectButton = ui.ConnectButton;
            _LoginErrorText = ui.LoginErrorText;

            _LobbyPanel = ui.LobbyPanel;
            _RoomIdInput = ui.RoomIdInput;
            _JoinButton = ui.JoinButton;
            _CreateButton = ui.CreateButton;
            _LobbyErrorText = ui.LobbyErrorText;

            _RoomPanel = ui.RoomPanel;
            _RoomIdText = ui.RoomIdText;
            _PlayerCountText = ui.PlayerCountText;
            _LeaveButton = ui.LeaveButton;

            _ConnectButton.onClick.AddListener(OnConnectClicked);
            _JoinButton.onClick.AddListener(OnJoinClicked);
            _CreateButton.onClick.AddListener(OnCreateClicked);
            _LeaveButton.onClick.AddListener(OnLeaveClicked);

            _NetworkManager.RegisterHandler<JoinRoom>(
                MessageType.JoinRoom, OnJoinRoomResponse);
            _NetworkManager.RegisterHandler<CreateRoom>(
                MessageType.CreateRoom, OnCreateRoomResponse);
            _NetworkManager.RegisterHandler<PlayerJoined>(
                MessageType.PlayerJoined, OnPlayerJoinedResponse);
            _NetworkManager.OnDisconnected += OnDisconnected;

            ShowPanel(_LoginPanel);
            _LobbyErrorText.gameObject.SetActive(false);
            _LoginErrorText.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            _ConnectButton.onClick.RemoveListener(OnConnectClicked);
            _JoinButton.onClick.RemoveListener(OnJoinClicked);
            _CreateButton.onClick.RemoveListener(OnCreateClicked);
            _LeaveButton.onClick.RemoveListener(OnLeaveClicked);

            if (_NetworkManager != null)
            {
                _NetworkManager.UnregisterHandler(MessageType.JoinRoom);
                _NetworkManager.UnregisterHandler(MessageType.CreateRoom);
                _NetworkManager.UnregisterHandler(MessageType.PlayerJoined);
                _NetworkManager.OnDisconnected -= OnDisconnected;
            }
        }

        public void Tick(float deltaTime)
        {
            if (_LobbyErrorRemainingSeconds > 0f)
            {
                _LobbyErrorRemainingSeconds -= deltaTime;
                if (_LobbyErrorRemainingSeconds <= 0f)
                {
                    _LobbyErrorRemainingSeconds = 0f;
                    _LobbyErrorText.gameObject.SetActive(false);
                }
            }

            if (_LoginErrorRemainingSeconds > 0f)
            {
                _LoginErrorRemainingSeconds -= deltaTime;
                if (_LoginErrorRemainingSeconds <= 0f)
                {
                    _LoginErrorRemainingSeconds = 0f;
                    _LoginErrorText.gameObject.SetActive(false);
                }
            }
        }

        private void ShowPanel(GameObject panel)
        {
            _LoginPanel.SetActive(panel == _LoginPanel);
            _LobbyPanel.SetActive(panel == _LobbyPanel);
            _RoomPanel.SetActive(panel == _RoomPanel);
        }

        private void SetLobbyButtonsInteractable(bool interactable)
        {
            _JoinButton.interactable   = interactable;
            _CreateButton.interactable = interactable;
        }

        private void UpdateRoomPanel()
        {
            _RoomIdText.text      = $"房间：{_CurrentRoomId}";
            _PlayerCountText.text = $"等待玩家... {_PlayerCount} 人";
        }

        private void ShowLobbyError(string message)
        {
            _LobbyErrorText.text = message;
            _LobbyErrorText.gameObject.SetActive(true);
            _LobbyErrorRemainingSeconds = 2f;
        }

        private void ShowLoginError(string message)
        {
            _LoginErrorText.text = message;
            _LoginErrorText.gameObject.SetActive(true);
            _LoginErrorRemainingSeconds = 3f;
        }

        private void OnConnectClicked()
        {
            string nickname = _NicknameInput.text.Trim();
            string host     = _HostInput.text.Trim();
            if (string.IsNullOrEmpty(nickname) || string.IsNullOrEmpty(host) ||
                !int.TryParse(_PortInput.text.Trim(), out int port))
                return;

            _Nickname = nickname;
            try
            {
                _NetworkManager.Connect(host, port);
            }
            catch (SocketException)
            {
                ShowLoginError("无法连接到服务器，请检查地址和端口");
                return;
            }
            catch (Exception)
            {
                ShowLoginError("连接失败，请重试");
                return;
            }
            ShowPanel(_LobbyPanel);
        }

        private void OnCreateClicked()
        {
            SetLobbyButtonsInteractable(false);
            _NetworkManager.Send(MessageType.CreateRoom, new CreateRoom { MaxPlayers = 4 });
        }

        private void OnJoinClicked()
        {
            string roomId = _RoomIdInput.text.Trim();
            if (string.IsNullOrEmpty(roomId)) return;

            SetLobbyButtonsInteractable(false);
            _NetworkManager.Send(MessageType.JoinRoom, new JoinRoom { RoomId = roomId });
        }

        private void OnLeaveClicked()
        {
            // LeaveRoom has no proto message; JoinRoom empty payload is harmless — server returns InvalidInput which is ignored
            _NetworkManager.Send(MessageType.LeaveRoom, new JoinRoom());
            _CurrentRoomId = null;
            _PlayerId      = null;
            _PlayerCount   = 0;
            ShowPanel(_LobbyPanel);
        }

        private void OnCreateRoomResponse(CreateRoom msg, ErrorCode err, int[] errorParams)
        {
            if (err != ErrorCode.Success)
            {
                SetLobbyButtonsInteractable(true);
                ShowLobbyError("创建失败，请重试");
                return;
            }
            _NetworkManager.Send(MessageType.JoinRoom, new JoinRoom { RoomId = msg.RoomId });
        }

        private void OnJoinRoomResponse(JoinRoom msg, ErrorCode err, int[] errorParams)
        {
            SetLobbyButtonsInteractable(true);
            if (err != ErrorCode.Success)
            {
                ShowLobbyError(err switch
                {
                    ErrorCode.RoomNotFound      => "房间不存在",
                    ErrorCode.RoomFull          => $"房间已满（最多 {(errorParams.Length > 0 ? errorParams[0] : 0)} 人）",
                    ErrorCode.RoomAlreadyJoined => "已在房间中",
                    _                           => "操作失败，请重试"
                });
                return;
            }

            _PlayerId      = msg.PlayerId;
            _CurrentRoomId = msg.RoomId;
            _PlayerCount   = msg.PlayerIds.Count;
            UpdateRoomPanel();
            ShowPanel(_RoomPanel);
        }

        private void OnPlayerJoinedResponse(PlayerJoined msg, ErrorCode err, int[] errorParams)
        {
            if (msg.RoomId != _CurrentRoomId) return;
            _PlayerCount++;
            UpdateRoomPanel();
        }

        private void OnDisconnected()
        {
            _CurrentRoomId = null;
            _PlayerId      = null;
            _PlayerCount   = 0;
            ShowPanel(_LoginPanel);
        }
    }
}
