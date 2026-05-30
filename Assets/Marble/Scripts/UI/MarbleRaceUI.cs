using UnityEngine;

namespace Marble
{
    public class MarbleRaceUI : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private LobbyUI lobbyUI;
        [SerializeField] private RaceHUD raceHUD;
        [SerializeField] private ResultsUI resultsUI;

        [Header("References")]
        [SerializeField] private MarbleRaceController raceController;
        [SerializeField] private MarbleNetworkManager networkManager;
        [SerializeField] private MarbleNetworkSync networkSync;

        private bool _isInitialized;

        private void Awake()
        {
            if (lobbyUI == null)
            {
                var lobbyGO = new GameObject("LobbyUI");
                lobbyGO.transform.SetParent(transform);
                lobbyUI = lobbyGO.AddComponent<LobbyUI>();
            }

            if (raceHUD == null)
            {
                var hudGO = new GameObject("RaceHUD");
                hudGO.transform.SetParent(transform);
                raceHUD = hudGO.AddComponent<RaceHUD>();
            }

            if (resultsUI == null)
            {
                var resultsGO = new GameObject("ResultsUI");
                resultsGO.transform.SetParent(transform);
                resultsUI = resultsGO.AddComponent<ResultsUI>();
            }
        }

        public void Initialize(
            MarbleRaceController controller,
            MarbleNetworkManager netManager,
            MarbleNetworkSync netSync)
        {
            raceController = controller;
            networkManager = netManager;
            networkSync = netSync;

            lobbyUI?.SetReferences(netManager, controller);
            raceHUD?.SetReferences(controller);
            resultsUI?.SetReferences(controller, lobbyUI);

            if (raceController != null)
            {
                if (raceController.StateMachine != null)
                {
                    raceController.StateMachine.OnPhaseChanged += HandlePhaseChanged;
                }
                raceController.OnAllMarblesFinished += HandleRaceFinished;
            }

            if (networkSync != null)
            {
                networkSync.OnRacePhaseChanged += HandleNetworkPhaseChanged;
            }

            _isInitialized = true;

            ShowLobby();
        }

        private void OnDestroy()
        {
            if (raceController != null)
            {
                if (raceController.StateMachine != null)
                {
                    raceController.StateMachine.OnPhaseChanged -= HandlePhaseChanged;
                }
                raceController.OnAllMarblesFinished -= HandleRaceFinished;
            }

            if (networkSync != null)
            {
                networkSync.OnRacePhaseChanged -= HandleNetworkPhaseChanged;
            }
        }

        public void ShowLobby()
        {
            lobbyUI?.Show();
            raceHUD?.Hide();
            resultsUI?.Hide();
        }

        public void ShowRaceHUD()
        {
            lobbyUI?.Hide();
            raceHUD?.Show();
            resultsUI?.Hide();
        }

        public void ShowResults()
        {
            lobbyUI?.Hide();
            raceHUD?.Hide();
            resultsUI?.Show();
        }

        public void HideAll()
        {
            lobbyUI?.Hide();
            raceHUD?.Hide();
            resultsUI?.Hide();
        }

        private void HandlePhaseChanged(RacePhase phase)
        {
            switch (phase)
            {
                case RacePhase.Lobby:
                    ShowLobby();
                    break;
                case RacePhase.Countdown:
                case RacePhase.Racing:
                    ShowRaceHUD();
                    break;
                case RacePhase.Finished:
                    break;
            }
        }

        private void HandleNetworkPhaseChanged(RacePhase phase, float serverTime)
        {
            HandlePhaseChanged(phase);
        }

        private void HandleRaceFinished()
        {
            Invoke(nameof(DelayedShowResults), 2f);
        }

        private void DelayedShowResults()
        {
            ShowResults();
        }

        public LobbyUI GetLobbyUI() => lobbyUI;

        public RaceHUD GetRaceHUD() => raceHUD;

        public ResultsUI GetResultsUI() => resultsUI;
    }
}
