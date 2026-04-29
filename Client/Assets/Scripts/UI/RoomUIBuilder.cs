using UnityEngine;
using UnityEngine.UI;

namespace StateSync.Client.UI
{
    public sealed class RoomUIReferences
    {
        public GameObject RootObject { get; set; }

        public GameObject LoginPanel { get; set; }
        public InputField NicknameInput { get; set; }
        public InputField HostInput { get; set; }
        public InputField PortInput { get; set; }
        public Button ConnectButton { get; set; }
        public Text LoginErrorText { get; set; }

        public GameObject LobbyPanel { get; set; }
        public InputField RoomIdInput { get; set; }
        public Button JoinButton { get; set; }
        public Button CreateButton { get; set; }
        public Text LobbyErrorText { get; set; }

        public GameObject RoomPanel { get; set; }
        public Text RoomIdText { get; set; }
        public Text PlayerCountText { get; set; }
        public Button LeaveButton { get; set; }

        public GameObject EventSystemObject { get; set; }
        public bool CreatedEventSystem { get; set; }
    }

    public sealed class RoomUIBuilder
    {
        private readonly Font _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        public RoomUIReferences Build()
        {
            bool createdEventSystem;
            GameObject eventSystemObject = EnsureEventSystem(out createdEventSystem);
            Canvas canvas = CreateCanvas();

            GameObject loginPanel = CreatePanel(canvas.transform, "LoginPanel",
                new Color(0.08f, 0.08f, 0.16f, 0.97f));
            GameObject lobbyPanel = CreatePanel(canvas.transform, "LobbyPanel",
                new Color(0.08f, 0.10f, 0.14f, 0.97f));
            GameObject roomPanel = CreatePanel(canvas.transform, "RoomPanel",
                new Color(0.06f, 0.10f, 0.16f, 0.97f));

            // LoginPanel contents
            CreateText(loginPanel.transform, "TitleText", "StateSync",
                40, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.72f), new Vector2(1f, 0.88f));
            InputField nicknameInput = CreateInputField(loginPanel.transform,
                "NicknameInput", "输入昵称",
                new Vector2(0.20f, 0.58f), new Vector2(0.80f, 0.68f));
            InputField hostInput = CreateInputField(loginPanel.transform,
                "HostInput", "服务器地址",
                new Vector2(0.20f, 0.44f), new Vector2(0.80f, 0.54f));
            hostInput.text = "127.0.0.1";
            InputField portInput = CreateInputField(loginPanel.transform,
                "PortInput", "端口",
                new Vector2(0.20f, 0.30f), new Vector2(0.80f, 0.40f));
            portInput.text = "8080";
            Button connectButton = CreateButton(loginPanel.transform,
                "ConnectButton", "连接",
                new Vector2(0.30f, 0.16f), new Vector2(0.70f, 0.26f));
            Text loginErrorText = CreateText(loginPanel.transform,
                "LoginErrorText", "",
                18, TextAnchor.MiddleCenter,
                new Vector2(0.10f, 0.04f), new Vector2(0.90f, 0.13f));
            loginErrorText.color = new Color(1f, 0.35f, 0.35f);
            loginErrorText.gameObject.SetActive(false);

            // LobbyPanel contents
            CreateText(lobbyPanel.transform, "TitleText", "大厅",
                40, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.72f), new Vector2(1f, 0.88f));
            InputField roomIdInput = CreateInputField(lobbyPanel.transform,
                "RoomIdInput", "房间编号",
                new Vector2(0.10f, 0.54f), new Vector2(0.62f, 0.64f));
            Button joinButton = CreateButton(lobbyPanel.transform,
                "JoinButton", "加入",
                new Vector2(0.64f, 0.54f), new Vector2(0.90f, 0.64f));
            Button createButton = CreateButton(lobbyPanel.transform,
                "CreateButton", "创建房间",
                new Vector2(0.20f, 0.38f), new Vector2(0.80f, 0.48f));
            Text lobbyErrorText = CreateText(lobbyPanel.transform,
                "ErrorText", "",
                18, TextAnchor.MiddleCenter,
                new Vector2(0.10f, 0.24f), new Vector2(0.90f, 0.34f));
            lobbyErrorText.color = new Color(1f, 0.35f, 0.35f);
            lobbyErrorText.gameObject.SetActive(false);

            // RoomPanel contents
            Text roomIdText = CreateText(roomPanel.transform,
                "RoomIdText", "房间-----",
                26, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.65f), new Vector2(1f, 0.75f));
            Text playerCountText = CreateText(roomPanel.transform,
                "PlayerCountText", "玩家数量... 0 人",
                22, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.52f), new Vector2(1f, 0.62f));
            CreateText(roomPanel.transform, "WaitingHint",
                "等待其他玩家加入...",
                18, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.40f), new Vector2(1f, 0.50f));
            Button leaveButton = CreateButton(roomPanel.transform,
                "LeaveButton", "离开房间",
                new Vector2(0.30f, 0.20f), new Vector2(0.70f, 0.30f));

            loginPanel.SetActive(true);
            lobbyPanel.SetActive(false);
            roomPanel.SetActive(false);

            return new RoomUIReferences
            {
                RootObject = canvas.gameObject,
                LoginPanel = loginPanel,
                NicknameInput = nicknameInput,
                HostInput = hostInput,
                PortInput = portInput,
                ConnectButton = connectButton,
                LoginErrorText = loginErrorText,
                LobbyPanel = lobbyPanel,
                RoomIdInput = roomIdInput,
                JoinButton = joinButton,
                CreateButton = createButton,
                LobbyErrorText = lobbyErrorText,
                RoomPanel = roomPanel,
                RoomIdText = roomIdText,
                PlayerCountText = playerCountText,
                LeaveButton = leaveButton,
                EventSystemObject = eventSystemObject,
                CreatedEventSystem = createdEventSystem
            };
        }

        private static GameObject EnsureEventSystem(out bool created)
        {
            UnityEngine.EventSystems.EventSystem existing =
                Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (existing != null)
            {
                created = false;
                return existing.gameObject;
            }

            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            created = true;
            return es;
        }

        private Canvas CreateCanvas()
        {
            GameObject go = new GameObject("RoomUI");
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private GameObject CreatePanel(Transform parent, string name, Color bgColor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            go.AddComponent<Image>().color = bgColor;
            return go;
        }

        private Text CreateText(Transform parent, string name, string content,
            int fontSize, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Text t = go.AddComponent<Text>();
            t.font = _font;
            t.text = content;
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = Color.white;

            return t;
        }

        private Text CreateChildText(Transform parent, string name, string content,
            int fontSize, Color color, FontStyle fontStyle = FontStyle.Normal)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SetFullStretch(go.AddComponent<RectTransform>(),
                new Vector2(8, 4), new Vector2(-8, -4));
            Text t = go.AddComponent<Text>();
            t.font = _font;
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.fontStyle = fontStyle;

            return t;
        }

        private InputField CreateInputField(Transform parent, string name,
            string placeholder, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.28f);

            Text pText = CreateChildText(go.transform, "Placeholder", placeholder,
                18, new Color(0.65f, 0.65f, 0.65f, 0.9f), FontStyle.Italic);

            Text iText = CreateChildText(go.transform, "Text", "",
                18, Color.white);

            InputField field = go.AddComponent<InputField>();
            field.textComponent = iText;
            field.placeholder = pText;
            return field;
        }

        private Button CreateButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            go.AddComponent<Image>().color = new Color(0.18f, 0.47f, 0.87f);

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.28f, 0.57f, 0.97f);
            cb.pressedColor = new Color(0.10f, 0.30f, 0.65f);
            cb.disabledColor = new Color(0.40f, 0.40f, 0.40f);
            btn.colors = cb;

            GameObject tGo = new GameObject("Text");
            tGo.transform.SetParent(go.transform, false);
            SetFullStretch(tGo.AddComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            Text t = tGo.AddComponent<Text>();
            t.font = _font;
            t.text = label;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.fontSize = 20;

            return btn;
        }

        private static void SetFullStretch(RectTransform rt,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
