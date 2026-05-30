using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Marble
{
    public class ResultsUI : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas parentCanvas;

        [Header("UI Elements")]
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI resultsText;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button backToLobbyButton;

        [Header("References")]
        [SerializeField] private MarbleRaceController raceController;
        [SerializeField] private LobbyUI lobbyUI;

        private StringBuilder _sb;

        private readonly string[] _teamNames = new string[]
        {
            "Red", "Blue", "Green", "Yellow", "Magenta",
            "Cyan", "Orange", "Purple", "Forest", "Silver"
        };

        private void Awake()
        {
            _sb = new StringBuilder(1024);
        }

        private void Start()
        {
            FindReferences();

            if (playAgainButton != null)
            {
                playAgainButton.onClick.AddListener(OnPlayAgainClicked);
            }
            if (backToLobbyButton != null)
            {
                backToLobbyButton.onClick.AddListener(OnBackToLobbyClicked);
            }

            Hide();
        }

        private void FindReferences()
        {
            if (raceController == null)
            {
                raceController = FindAnyObjectByType<MarbleRaceController>();
            }
            if (lobbyUI == null)
            {
                lobbyUI = FindAnyObjectByType<LobbyUI>();
            }
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

            resultsPanel = new GameObject("ResultsPanel");
            resultsPanel.transform.SetParent(canvasParent, false);

            var panelRect = resultsPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(500, 500);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = resultsPanel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.9f);

            titleText = CreateText(resultsPanel.transform, "Title", "RACE RESULTS",
                new Vector2(0, 200), 36);

            resultsText = CreateText(resultsPanel.transform, "Results", "",
                new Vector2(0, 20), 20, TextAlignmentOptions.Left);
            resultsText.GetComponent<RectTransform>().sizeDelta = new Vector2(450, 300);

            playAgainButton = CreateButton(resultsPanel.transform, "PlayAgain", "Play Again",
                new Vector2(-100, -200));

            backToLobbyButton = CreateButton(resultsPanel.transform, "BackToLobby", "Back to Lobby",
                new Vector2(100, -200));

            Debug.Log("ResultsUI created successfully!");
        }

        public void ClearUI()
        {
            if (resultsPanel != null)
            {
                DestroyImmediate(resultsPanel);
                resultsPanel = null;
            }
            titleText = null;
            resultsText = null;
            playAgainButton = null;
            backToLobbyButton = null;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string text,
            Vector2 position, int fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(450, 50);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;

            return tmp;
        }

        private Button CreateButton(Transform parent, string name, string text, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(160, 50);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.5f, 0.2f, 1f);

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
            tmp.fontSize = 20;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return button;
        }

        public void SetReferences(MarbleRaceController controller, LobbyUI lobby)
        {
            raceController = controller;
            lobbyUI = lobby;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            resultsPanel?.SetActive(true);

            UpdateResults();
        }

        public void Hide()
        {
            resultsPanel?.SetActive(false);
        }

        private void UpdateResults()
        {
            if (_sb == null) _sb = new StringBuilder(1024);
            if (resultsText == null || raceController == null) return;

            var rankings = raceController.GetRankingData();
            if (rankings == null || rankings.Length == 0)
            {
                resultsText.text = "No results available.";
                return;
            }

            _sb.Clear();
            _sb.AppendLine("<b>FINAL STANDINGS</b>");
            _sb.AppendLine();

            for (int i = 0; i < rankings.Length; i++)
            {
                var data = rankings[i];
                string posStr = RacePositionTracker.GetPositionString(data.Position);
                string teamName = GetTeamName(data.MarbleId);

                string medal = data.Position switch
                {
                    0 => "<color=#FFD700>",
                    1 => "<color=#C0C0C0>",
                    2 => "<color=#CD7F32>",
                    _ => "<color=#FFFFFF>"
                };

                if (data.IsFinished)
                {
                    string timeStr = FormatTime(data.FinishTime);
                    _sb.AppendLine($"{medal}{posStr}</color> {teamName} - {timeStr}");
                }
                else
                {
                    _sb.AppendLine($"{medal}{posStr}</color> {teamName} - DNF");
                }
            }

            resultsText.text = _sb.ToString();
        }

        private string GetTeamName(byte marbleId)
        {
            if (marbleId < _teamNames.Length)
            {
                return _teamNames[marbleId];
            }
            return $"Team {marbleId}";
        }

        private string FormatTime(float time)
        {
            int minutes = (int)(time / 60f);
            float seconds = time % 60f;
            return $"{minutes:D2}:{seconds:05.2f}";
        }

        private void OnPlayAgainClicked()
        {
            Hide();

            if (raceController != null)
            {
                raceController.ResetRace();
                raceController.StartCountdown();
            }
        }

        private void OnBackToLobbyClicked()
        {
            Hide();

            if (raceController != null)
            {
                raceController.ResetRace();
            }

            if (lobbyUI != null)
            {
                lobbyUI.Show();
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ResultsUI))]
    public class ResultsUIEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var resultsUI = (ResultsUI)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("UI Generation", EditorStyles.boldLabel);

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Generate UI", GUILayout.Height(30)))
            {
                resultsUI.GenerateUI();
                EditorUtility.SetDirty(resultsUI);
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear UI"))
            {
                resultsUI.ClearUI();
                EditorUtility.SetDirty(resultsUI);
            }
            GUI.backgroundColor = Color.white;
        }
    }
#endif
}
