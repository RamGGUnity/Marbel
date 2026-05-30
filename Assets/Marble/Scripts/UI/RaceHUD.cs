using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Marble
{
    public class RaceHUD : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas parentCanvas;

        [Header("Panels")]
        [SerializeField] private GameObject topPanel;
        [SerializeField] private GameObject timePanel;
        [SerializeField] private GameObject positionsPanel;

        [Header("Race Info")]
        [SerializeField] private TextMeshProUGUI raceTimeText;
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private TextMeshProUGUI phaseText;

        [Header("Position Display")]
        [SerializeField] private TextMeshProUGUI positionsText;
        [SerializeField] private int displayTopPositions = 5;

        [Header("References")]
        [SerializeField] private MarbleRaceController raceController;
        [SerializeField] private RaceStateMachine raceStateMachine;
        [SerializeField] private RacePositionTracker positionTracker;
        [SerializeField] private MarbleSpawnPoints spawnPoints;

        [Header("Configuration")]
        [SerializeField] private float positionUpdateInterval = 0.2f;

        private bool _isVisible;
        private StringBuilder _sb;
        private float _nextPositionUpdate;

        private void Awake()
        {
            _sb = new StringBuilder(512);
        }

        private void Start()
        {
            FindReferences();

            SubscribeToEvents();
            Hide();
        }

        private void FindReferences()
        {
            bool wasNull = raceController == null;

            if (raceController == null)
            {
                raceController = FindAnyObjectByType<MarbleRaceController>();
            }
            if (raceController != null)
            {
                raceStateMachine = raceController.StateMachine;
                positionTracker = raceController.PositionTracker;

                if (wasNull)
                {
                    SubscribeToEvents();
                }
            }

            if (spawnPoints == null)
            {
                spawnPoints = FindAnyObjectByType<MarbleSpawnPoints>();
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

            topPanel = CreatePanel(canvasParent, "TopPanel",
                new Vector2(300, 100),
                new Vector2(0, 1),
                new Vector2(0, 1),
                new Vector2(160, -60));

            countdownText = CreateText(topPanel.transform, "Countdown", "",
                new Vector2(0, 20), 48, TextAlignmentOptions.Center);

            phaseText = CreateText(topPanel.transform, "Phase", "",
                new Vector2(0, -20), 24, TextAlignmentOptions.Center);

            timePanel = CreatePanel(canvasParent, "TimePanel",
                new Vector2(200, 50),
                new Vector2(1, 1),
                new Vector2(1, 1),
                new Vector2(-110, -35));

            raceTimeText = CreateText(timePanel.transform, "Time", "00:00.00",
                Vector2.zero, 28, TextAlignmentOptions.Center);

            positionsPanel = CreatePanel(canvasParent, "PositionsPanel",
                new Vector2(200, 250),
                new Vector2(0, 0.5f),
                new Vector2(0, 0.5f),
                new Vector2(110, 0));

            positionsText = CreateText(positionsPanel.transform, "Positions", "",
                Vector2.zero, 18, TextAlignmentOptions.Left);

            Debug.Log("RaceHUD created successfully!");
        }

        public void ClearUI()
        {
            if (topPanel != null)
            {
                DestroyImmediate(topPanel);
                topPanel = null;
            }
            if (timePanel != null)
            {
                DestroyImmediate(timePanel);
                timePanel = null;
            }
            if (positionsPanel != null)
            {
                DestroyImmediate(positionsPanel);
                positionsPanel = null;
            }
            raceTimeText = null;
            countdownText = null;
            phaseText = null;
            positionsText = null;
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 size,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var image = panel.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0.6f);

            return panel;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string text,
            Vector2 position, int fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10, 5);
            rect.offsetMax = new Vector2(-10, -5);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;

            return tmp;
        }

        private void SubscribeToEvents()
        {
            if (raceStateMachine != null)
            {
                raceStateMachine.OnPhaseChanged += HandlePhaseChanged;
                raceStateMachine.OnCountdownTick += HandleCountdownTick;
            }

            if (positionTracker != null)
            {
                positionTracker.OnPositionsChanged += HandlePositionsChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (raceStateMachine != null)
            {
                raceStateMachine.OnPhaseChanged -= HandlePhaseChanged;
                raceStateMachine.OnCountdownTick -= HandleCountdownTick;
            }

            if (positionTracker != null)
            {
                positionTracker.OnPositionsChanged -= HandlePositionsChanged;
            }
        }

        public void SetReferences(MarbleRaceController controller)
        {
            raceController = controller;
            raceStateMachine = controller?.StateMachine;
            positionTracker = controller?.PositionTracker;

            UnsubscribeFromEvents();
            SubscribeToEvents();
        }

        public void Show()
        {
            _isVisible = true;
            gameObject.SetActive(true);
            topPanel?.SetActive(true);
            timePanel?.SetActive(true);
            positionsPanel?.SetActive(true);
        }

        public void Hide()
        {
            _isVisible = false;
            topPanel?.SetActive(false);
            timePanel?.SetActive(false);
            positionsPanel?.SetActive(false);
        }

        private void Update()
        {
            if (!_isVisible) return;

            UpdateRaceTime();

            if (Time.time >= _nextPositionUpdate)
            {
                UpdatePositions();
                _nextPositionUpdate = Time.time + positionUpdateInterval;
            }
        }

        private void UpdateRaceTime()
        {
            if (raceTimeText == null || raceController == null) return;

            if (raceStateMachine != null && raceStateMachine.IsRacing)
            {
                float time = raceController.GetRaceTime();
                int minutes = (int)(time / 60f);
                float seconds = time % 60f;
                raceTimeText.text = $"{minutes:D2}:{seconds:05.2f}";
            }
        }

        private void UpdatePositions()
        {
            if (_sb == null) _sb = new StringBuilder(512);
            if (positionsText == null || raceController == null) return;

            var rankings = raceController.GetRankingData();
            if (rankings == null || rankings.Length == 0) return;

            _sb.Clear();
            _sb.AppendLine("<b>STANDINGS</b>");
            _sb.AppendLine();

            int displayCount = Mathf.Min(displayTopPositions, rankings.Length);
            for (int i = 0; i < displayCount; i++)
            {
                var data = rankings[i];
                string posStr = RacePositionTracker.GetPositionString(data.Position);
                string teamName = GetTeamName(data.MarbleId);

                if (data.IsFinished)
                {
                    _sb.AppendLine($"{posStr} {teamName} <color=#00FF00>FINISHED</color>");
                }
                else
                {
                    string lapInfo = $"Lap {data.LapCount + 1}/{MarbleConstants.TotalLaps}";
                    _sb.AppendLine($"{posStr} {teamName} ({lapInfo})");
                }
            }

            positionsText.text = _sb.ToString();
        }

        private string GetTeamName(byte slotIndex)
        {
            byte countryId = slotIndex;
            if (raceController != null && raceController.SelectedCountryIds != null && slotIndex < raceController.SelectedCountryIds.Length)
            {
                countryId = raceController.SelectedCountryIds[slotIndex];
            }

            if (spawnPoints == null)
            {
                spawnPoints = FindAnyObjectByType<MarbleSpawnPoints>();
            }

            if (spawnPoints != null)
            {
                return spawnPoints.GetShortName(countryId);
            }
            return $"M{countryId}";
        }

        private void HandlePhaseChanged(RacePhase phase)
        {
            if (phaseText == null) return;

            switch (phase)
            {
                case RacePhase.Lobby:
                    phaseText.text = "WAITING";
                    Hide();
                    break;
                case RacePhase.Countdown:
                    phaseText.text = "GET READY";
                    Show();
                    break;
                case RacePhase.Racing:
                    phaseText.text = "RACING";
                    if (countdownText != null) countdownText.text = "";
                    break;
                case RacePhase.Finished:
                    phaseText.text = "FINISHED";
                    break;
            }
        }

        private void HandleCountdownTick(int seconds)
        {
            if (countdownText == null) return;

            if (seconds > 0)
            {
                countdownText.text = seconds.ToString();
                countdownText.color = Color.yellow;
            }
            else
            {
                countdownText.text = "GO!";
                countdownText.color = Color.green;

                Invoke(nameof(ClearCountdown), 1f);
            }
        }

        private void ClearCountdown()
        {
            if (countdownText != null)
            {
                countdownText.text = "";
            }
        }

        private void HandlePositionsChanged(byte[] positions)
        {
            UpdatePositions();
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(RaceHUD))]
    public class RaceHUDEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var raceHUD = (RaceHUD)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("UI Generation", EditorStyles.boldLabel);

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Generate UI", GUILayout.Height(30)))
            {
                raceHUD.GenerateUI();
                EditorUtility.SetDirty(raceHUD);
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear UI"))
            {
                raceHUD.ClearUI();
                EditorUtility.SetDirty(raceHUD);
            }
            GUI.backgroundColor = Color.white;
        }
    }
#endif
}
