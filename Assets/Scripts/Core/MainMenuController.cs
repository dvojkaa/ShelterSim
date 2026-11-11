using SheltreSim.Saving;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SheltreSim.Core
{
    /// <summary>
    /// Code-built single-scene front door. The game world is already bootstrapped underneath
    /// the overlay; it is held by ModalUI until Continue or New Game is selected.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private static MainMenuController instance;
        private GameObject panel;
        private Button continueButton;
        private Button quitButton;
        private float quitConfirmUntil;
        private Text status;
        private Text difficultyLabel;
        private Text difficultyDescription;
        private SaveManager saves;
        private bool isOpen;
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        private static void Ensure()
        {
            if (instance != null) return;
            new GameObject("MainMenuController").AddComponent<MainMenuController>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            BuildUI();
        }

        private void Start()
        {
            Refresh();
            // New Game reloads the scene; the fresh controller must not open the menu again (otherwise New Game just returns to the menu).
            if (startNewGameImmediately) { startNewGameImmediately = false; return; }
            // Automated probes and screenshot runs must not start behind a modal menu (the new-game probe opts back in).
            foreach (string argument in System.Environment.GetCommandLineArgs())
                if (argument.StartsWith("--") && !argument.Contains("newgame") && (argument.Contains("probe") || argument.Contains("smoke") || argument.Contains("shots"))) return;
            Show();
        }

        private void Update()
        {
            if (quitConfirmUntil > 0f && Time.unscaledTime > quitConfirmUntil)
            {
                quitConfirmUntil = 0f;
                if (quitButton != null) quitButton.GetComponentInChildren<Text>().text = "Quit";
                if (status != null) status.text = "Choose Continue or start a new run.";
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void ReturnToMenu()
        {
            Ensure();
            if (instance != null) instance.Show();
        }

        private void Show()
        {
            if (isOpen) return;
            ModalUI.Replace(this, Hide);
            isOpen = true;
            panel.SetActive(true);
            Refresh();
        }

        private void Hide()
        {
            if (!isOpen) return;
            isOpen = false;
            panel.SetActive(false);
            ModalUI.Close(this);
        }

        private void Refresh()
        {
            if (saves == null) saves = FindFirstObjectByType<SaveManager>();
            bool canContinue = saves != null && saves.HasContinueSave;
            continueButton.interactable = canContinue;
            status.text = canContinue
                ? "Continue loads the newest valid manual save or autosave."
                : "No saved run yet. Start a new game, then F5 saves a manual copy.";
            difficultyLabel.text = "NEW GAME DIFFICULTY: " + GameDifficultySettings.Name(GameDifficultySettings.Current);
            difficultyDescription.text = GameDifficultySettings.Description(GameDifficultySettings.Current);
        }

        private void CycleDifficulty()
        {
            GameDifficultySettings.Select(GameDifficultySettings.Next(GameDifficultySettings.Current));
            Refresh();
        }

        private void Continue()
        {
            if (saves == null) saves = FindFirstObjectByType<SaveManager>();
            Hide();
            if (saves == null || !saves.TryLoadContinue())
            {
                if (saves == null) status.text = "Save/load is unavailable in this scene.";
                Show();
            }
        }

        private static bool startNewGameImmediately;

        public static void PrepareImmediateNewGame() => startNewGameImmediately = true;

        /// <summary>Public so the new-game probe can press the button.</summary>
        public void StartNewGame() => NewGame();
        public bool IsMenuOpen => isOpen;

        private void NewGame()
        {
            Hide();
            startNewGameImmediately = true;
            Time.timeScale = 1f;
            Scene active = SceneManager.GetActiveScene();
            if (active.buildIndex >= 0)
            {
                SceneManager.LoadScene(active.buildIndex);
                return;
            }
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(active.path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            Show();
            status.text = "This scene is missing from the build settings.";
#endif
        }

        private void BuildUI()
        {
            Canvas canvas = GameUI.CreateCanvas(transform, "MainMenuCanvas", 300, true);
            panel = GameUI.Backdrop(canvas.transform, "MainMenuPanel");
            Transform card = GameUI.Panel(panel.transform, "Card", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 560f), GameUI.PanelColor, true).transform;
            GameUI.Label(card, "Title", "SHELTER SIM", TopLeft, TopLeft,
                new Vector2(48f, -46f), new Vector2(624f, 72f), 48, GameUI.Accent, bold: true);
            GameUI.Label(card, "Subtitle", "A life underground begins with one decision.", TopLeft, TopLeft,
                new Vector2(50f, -116f), new Vector2(620f, 34f), 21, GameUI.Muted);
            GameUI.Button(card, "DifficultyButton", "Change New Game Difficulty", TopLeft, TopLeft,
                new Vector2(50f, -166f), new Vector2(620f, 56f), CycleDifficulty);
            difficultyLabel = GameUI.Label(card, "DifficultyLabel", "NEW GAME DIFFICULTY: NORMAL", TopLeft, TopLeft,
                new Vector2(50f, -232f), new Vector2(620f, 30f), 20, GameUI.Accent, bold: true);
            difficultyDescription = GameUI.Label(card, "DifficultyDescription", "", TopLeft, TopLeft,
                new Vector2(50f, -263f), new Vector2(620f, 32f), 15, GameUI.Muted);
            continueButton = GameUI.Button(card, "ContinueButton", "Continue", TopLeft, TopLeft,
                new Vector2(50f, -318f), new Vector2(620f, 58f), Continue, true);
            GameUI.Button(card, "NewGameButton", "New Game", TopLeft, TopLeft,
                new Vector2(50f, -386f), new Vector2(620f, 58f), NewGame);
            quitButton = GameUI.Button(card, "QuitButton", "Quit", TopLeft, TopLeft,
                new Vector2(50f, -454f), new Vector2(620f, 52f), RequestQuit);
            status = GameUI.Label(card, "SaveStatus", string.Empty, TopLeft, TopLeft,
                new Vector2(50f, -514f), new Vector2(620f, 48f), 16, GameUI.Muted);
            panel.SetActive(false);
        }

        private void RequestQuit()
        {
            if (quitConfirmUntil > Time.unscaledTime)
            {
                quitConfirmUntil = 0f;
                QuitGame();
                return;
            }
            quitConfirmUntil = Time.unscaledTime + 5f;
            quitButton.GetComponentInChildren<Text>().text = "Confirm Quit";
            status.text = "Quit the game? Press again within 5 seconds.";
        }

        private static void QuitGame()
        {
            ModalUI.DismissCurrent();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
