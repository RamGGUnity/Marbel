using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Marble
{
    public class LobbyUI : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas parentCanvas;

        [Header("Connection Panel")]
        [SerializeField] private GameObject connectionPanel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Room List")]
        [SerializeField] private Transform roomListContent;
        [SerializeField] private GameObject roomItemPrefab;
        [SerializeField] private TextMeshProUGUI noRoomsText;

        [Header("Lobby Panel")]
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private Button startRaceButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI connectionInfoText;
        [SerializeField] private TextMeshProUGUI joinCodeText;

        [Header("References")]
        [SerializeField] private MarbleNetworkManager networkManager;
        [SerializeField] private MarbleRaceController raceController;

        private List<GameObject> _roomItems = new List<GameObject>();

        private void Start()
        {
            FindReferences();

            SetupButtonListeners();
            SubscribeToEvents();
            ShowConnectionPanel();
        }

        private void FindReferences()
        {
            bool wasNull = networkManager == null;

            if (networkManager == null)
            {
                networkManager = FindAnyObjectByType<MarbleNetworkManager>();
            }
            if (raceController == null)
            {
                raceController = FindAnyObjectByType<MarbleRaceController>();
            }

            if (wasNull && networkManager != null)
            {
                SubscribeToEvents();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        public void GenerateUI()
        {
            if (parentCanvas == null)
            {
                Debug.LogError("Please assign a Parent Canvas first!");
                return;
            }

            ClearUI();

            Transform canvasParent = parentCanvas.transform;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.AddComponent<EventSystem>();
                eventSystemGO.AddComponent<StandaloneInputModule>();
            }

            connectionPanel = CreatePanel(canvasParent, "ConnectionPanel", new Vector2(450, 400));

            CreateText(connectionPanel.transform, "Title", "Marble Race", new Vector2(0, 170), 32);

            hostButton = CreateButton(connectionPanel.transform, "HostButton", "Create Room", new Vector2(-80, 110));
            refreshButton = CreateButton(connectionPanel.transform, "RefreshButton", "Refresh", new Vector2(80, 110));

            CreateText(connectionPanel.transform, "RoomListLabel", "Available Rooms:", new Vector2(0, 60), 18);

            var scrollArea = CreateScrollArea(connectionPanel.transform, "RoomScrollArea", new Vector2(0, -30), new Vector2(380, 180));
            roomListContent = scrollArea.transform;

            noRoomsText = CreateText(connectionPanel.transform, "NoRoomsText", "Click Refresh to find rooms", new Vector2(0, -30), 16);
            noRoomsText.color = Color.gray;

            statusText = CreateText(connectionPanel.transform, "Status", "", new Vector2(0, -160), 16);

            lobbyPanel = CreatePanel(canvasParent, "LobbyPanel", new Vector2(400, 350));

            connectionInfoText = CreateText(lobbyPanel.transform, "ConnectionInfo", "Connected", new Vector2(0, 130), 24);

            joinCodeText = CreateText(lobbyPanel.transform, "JoinCode", "Join Code: ------", new Vector2(0, 70), 28);

            playerCountText = CreateText(lobbyPanel.transform, "PlayerCount", "Players: 0", new Vector2(0, 20), 20);

            startRaceButton = CreateButton(lobbyPanel.transform, "StartButton", "Start Race", new Vector2(-80, -60));
            disconnectButton = CreateButton(lobbyPanel.transform, "DisconnectButton", "Disconnect", new Vector2(80, -60));

            lobbyPanel.SetActive(false);

            Debug.Log("LobbyUI created successfully!");
        }

        private GameObject CreateScrollArea(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var scrollGO = new GameObject(name);
            scrollGO.transform.SetParent(parent, false);

            var scrollRect = scrollGO.AddComponent<RectTransform>();
            scrollRect.anchoredPosition = position;
            scrollRect.sizeDelta = size;

            var scrollImage = scrollGO.AddComponent<Image>();
            scrollImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGO.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            var viewportMask = viewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            viewport.AddComponent<Image>();

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 5;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            return content;
        }

        public void ClearUI()
        {
            ClearRoomList();

            if (connectionPanel != null)
            {
                DestroyImmediate(connectionPanel);
                connectionPanel = null;
            }
            if (lobbyPanel != null)
            {
                DestroyImmediate(lobbyPanel);
                lobbyPanel = null;
            }
            hostButton = null;
            refreshButton = null;
            statusText = null;
            roomListContent = null;
            noRoomsText = null;
            startRaceButton = null;
            disconnectButton = null;
            playerCountText = null;
            connectionInfoText = null;
            joinCodeText = null;
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            var rect = panel.AddComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var image = panel.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0.8f);

            return panel;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 position, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(350, 50);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return tmp;
        }

        private TMP_InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(250, 40);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var input = go.AddComponent<TMP_InputField>();

            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(go.transform, false);
            var textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 5);
            textAreaRect.offsetMax = new Vector2(-10, -5);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(textArea.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 18;
            tmp.color = Color.white;

            input.textComponent = tmp;
            input.textViewport = textAreaRect;
            input.text = placeholder;

            return input;
        }

        private Button CreateButton(Transform parent, string name, string text, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(140, 40);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.8f, 1f);

            var button = go.AddComponent<Button>();

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return button;
        }

        private void SetupButtonListeners()
        {
            hostButton?.onClick.AddListener(OnHostClicked);
            refreshButton?.onClick.AddListener(OnRefreshClicked);
            startRaceButton?.onClick.AddListener(OnStartRaceClicked);
            disconnectButton?.onClick.AddListener(OnDisconnectClicked);
        }

        private void SubscribeToEvents()
        {
            if (networkManager == null) return;

            networkManager.OnHostStarted += HandleHostStarted;
            networkManager.OnClientConnected += HandleClientConnected;
            networkManager.OnClientDisconnected += HandleClientDisconnected;
            networkManager.OnConnectionFailed += HandleConnectionFailed;
            networkManager.OnPlayerJoined += HandlePlayerJoined;
            networkManager.OnPlayerLeft += HandlePlayerLeft;
            networkManager.OnRoomsUpdated += HandleRoomsUpdated;
        }

        private void UnsubscribeFromEvents()
        {
            if (networkManager == null) return;

            networkManager.OnHostStarted -= HandleHostStarted;
            networkManager.OnClientConnected -= HandleClientConnected;
            networkManager.OnClientDisconnected -= HandleClientDisconnected;
            networkManager.OnConnectionFailed -= HandleConnectionFailed;
            networkManager.OnPlayerJoined -= HandlePlayerJoined;
            networkManager.OnPlayerLeft -= HandlePlayerLeft;
            networkManager.OnRoomsUpdated -= HandleRoomsUpdated;
        }

        public void SetReferences(MarbleNetworkManager netManager, MarbleRaceController controller)
        {
            networkManager = netManager;
            raceController = controller;

            UnsubscribeFromEvents();
            SubscribeToEvents();
        }

        private void ShowConnectionPanel()
        {
            connectionPanel?.SetActive(true);
            lobbyPanel?.SetActive(false);
            UpdateStatus("");

            if (networkManager != null)
            {
                networkManager.RefreshRoomList();
            }
        }

        private void ShowLobbyPanel()
        {
            connectionPanel?.SetActive(false);
            lobbyPanel?.SetActive(true);
            UpdatePlayerCount();
        }

        public void Hide()
        {
            connectionPanel?.SetActive(false);
            lobbyPanel?.SetActive(false);
        }

        public void Show()
        {
            if (networkManager != null && networkManager.IsConnected)
            {
                ShowLobbyPanel();
            }
            else
            {
                ShowConnectionPanel();
            }
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private void UpdatePlayerCount()
        {
            if (playerCountText == null || networkManager == null) return;

            int count = networkManager.IsServer ? networkManager.GetConnectedClientCount() : 1;
            playerCountText.text = $"Players: {count}";
        }

        private void UpdateConnectionInfo(string info)
        {
            if (connectionInfoText != null)
            {
                connectionInfoText.text = info;
            }
        }

        private void UpdateJoinCode(string code)
        {
            if (joinCodeText != null)
            {
                if (string.IsNullOrEmpty(code))
                {
                    joinCodeText.gameObject.SetActive(false);
                }
                else
                {
                    joinCodeText.gameObject.SetActive(true);
                    joinCodeText.text = $"Join Code: {code}";
                }
            }
        }

        #region Button Handlers

        private void OnHostClicked()
        {
            if (networkManager == null)
            {
                FindReferences();
            }

            if (networkManager == null)
            {
                UpdateStatus("NetworkManager not found!");
                return;
            }

            UpdateStatus("Creating room...");
            networkManager.CreateRoom();
        }

        private void OnRefreshClicked()
        {
            if (networkManager == null)
            {
                FindReferences();
            }

            if (networkManager == null)
            {
                UpdateStatus("NetworkManager not found!");
                return;
            }

            UpdateStatus("Searching for rooms...");
            networkManager.RefreshRoomList();
        }

        private void OnRoomJoinClicked(string lobbyId)
        {
            if (networkManager == null) return;

            UpdateStatus("Joining room...");
            networkManager.JoinRoom(lobbyId);
        }

        private void OnStartRaceClicked()
        {
            Debug.Log($"[LobbyUI] OnStartRaceClicked - raceController: {raceController}, networkManager: {networkManager}");

            if (raceController == null)
            {
                Debug.LogWarning("RaceController not assigned!");
                return;
            }

            Debug.Log($"[LobbyUI] networkManager.IsServer: {(networkManager != null ? networkManager.IsServer : false)}");

            if (!networkManager.IsServer)
            {
                Debug.LogWarning("Only host can start the race!");
                return;
            }

            raceController.StartCountdown();
            Hide();
        }

        private void OnDisconnectClicked()
        {
            networkManager?.Disconnect();
            ShowConnectionPanel();
        }

        #endregion

        #region Event Handlers

        private void HandleHostStarted()
        {
            UpdateConnectionInfo("Hosting Game");
            UpdateJoinCode(networkManager?.JoinCode ?? "------");
            ShowLobbyPanel();

            if (startRaceButton != null)
            {
                startRaceButton.interactable = true;
            }
        }

        private void HandleClientConnected()
        {
            UpdateConnectionInfo("Connected to Host");
            UpdateJoinCode("");
            ShowLobbyPanel();

            if (startRaceButton != null)
            {
                startRaceButton.interactable = networkManager.IsHost;
            }
        }

        private void HandleClientDisconnected()
        {
            ShowConnectionPanel();
            UpdateStatus("Disconnected");
        }

        private void HandleConnectionFailed(string reason)
        {
            UpdateStatus($"Failed: {reason}");
        }

        private void HandlePlayerJoined(ulong clientId)
        {
            UpdatePlayerCount();
        }

        private void HandlePlayerLeft(ulong clientId)
        {
            UpdatePlayerCount();
        }

        private void HandleRoomsUpdated(List<RoomInfo> rooms)
        {
            ClearRoomList();

            if (rooms == null || rooms.Count == 0)
            {
                if (noRoomsText != null)
                {
                    noRoomsText.gameObject.SetActive(true);
                    noRoomsText.text = "No rooms found. Create one or refresh.";
                }
                UpdateStatus("No rooms found");
                return;
            }

            if (noRoomsText != null)
            {
                noRoomsText.gameObject.SetActive(false);
            }

            UpdateStatus($"Found {rooms.Count} room(s)");

            foreach (var room in rooms)
            {
                CreateRoomItem(room);
            }
        }

        private void ClearRoomList()
        {
            foreach (var item in _roomItems)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }
            _roomItems.Clear();
        }

        private void CreateRoomItem(RoomInfo room)
        {
            if (roomListContent == null) return;

            GameObject item;
            if (roomItemPrefab != null)
            {
                item = Instantiate(roomItemPrefab, roomListContent);
            }
            else
            {
                item = CreateDefaultRoomItem(roomListContent, room);
            }

            var button = item.GetComponentInChildren<Button>();
            if (button != null)
            {
                string lobbyId = room.Id;
                button.onClick.AddListener(() => OnRoomJoinClicked(lobbyId));
            }

            var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var text in texts)
            {
                if (text.name.Contains("Name") || text.name == "RoomName")
                {
                    text.text = room.Name;
                }
                else if (text.name.Contains("Players") || text.name == "PlayerCount")
                {
                    text.text = $"{room.CurrentPlayers}/{room.MaxPlayers}";
                }
                else if (texts.Length == 1)
                {
                    text.text = $"{room.Name} ({room.CurrentPlayers}/{room.MaxPlayers})";
                }
            }

            _roomItems.Add(item);
        }

        private GameObject CreateDefaultRoomItem(Transform parent, RoomInfo room)
        {
            var item = new GameObject("RoomItem");
            item.transform.SetParent(parent, false);

            var rect = item.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(350, 50);

            var layout = item.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 10;
            layout.padding = new RectOffset(10, 10, 5, 5);

            var bg = item.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.3f, 1f);

            var nameGO = new GameObject("RoomName");
            nameGO.transform.SetParent(item.transform, false);
            var nameRect = nameGO.AddComponent<RectTransform>();
            var nameLayout = nameGO.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1;
            var nameText = nameGO.AddComponent<TextMeshProUGUI>();
            nameText.text = room.Name;
            nameText.fontSize = 16;
            nameText.color = Color.white;

            var countGO = new GameObject("PlayerCount");
            countGO.transform.SetParent(item.transform, false);
            var countRect = countGO.AddComponent<RectTransform>();
            var countLayout = countGO.AddComponent<LayoutElement>();
            countLayout.preferredWidth = 50;
            var countText = countGO.AddComponent<TextMeshProUGUI>();
            countText.text = $"{room.CurrentPlayers}/{room.MaxPlayers}";
            countText.fontSize = 14;
            countText.color = Color.gray;
            countText.alignment = TextAlignmentOptions.Right;

            var btnGO = new GameObject("JoinButton");
            btnGO.transform.SetParent(item.transform, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            var btnLayout = btnGO.AddComponent<LayoutElement>();
            btnLayout.preferredWidth = 60;
            btnLayout.preferredHeight = 30;
            var btnImage = btnGO.AddComponent<Image>();
            btnImage.color = new Color(0.3f, 0.6f, 0.3f, 1f);
            var btn = btnGO.AddComponent<Button>();

            var btnTextGO = new GameObject("Text");
            btnTextGO.transform.SetParent(btnGO.transform, false);
            var btnTextRect = btnTextGO.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;
            var btnText = btnTextGO.AddComponent<TextMeshProUGUI>();
            btnText.text = "Join";
            btnText.fontSize = 14;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            return item;
        }

        #endregion
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(LobbyUI))]
    public class LobbyUIEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var lobbyUI = (LobbyUI)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("UI Generation", EditorStyles.boldLabel);

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Generate UI", GUILayout.Height(30)))
            {
                lobbyUI.GenerateUI();
                EditorUtility.SetDirty(lobbyUI);
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear UI"))
            {
                lobbyUI.ClearUI();
                EditorUtility.SetDirty(lobbyUI);
            }
            GUI.backgroundColor = Color.white;
        }
    }
#endif
}
