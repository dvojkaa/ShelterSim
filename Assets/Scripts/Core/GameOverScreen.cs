using SheltreSim.Saving;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SheltreSim.Core
{
    /// <summary>Death owns the same modal lock as pause/inventory, including the world clock.</summary>
    public class GameOverScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        private GameObject panel;
        private SaveManager saveManager;
        private Button loadButton;
        private Text detailLabel;
        private Text statusLabel;
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        private void Awake() => BuildUI();
        private void OnEnable() => GameMessageBus.OnMessage += OnMessage;
        private void OnDisable()
        {
            GameMessageBus.OnMessage -= OnMessage;
            Hide();
        }

        public void Show()
        {
            if (IsOpen) return;
            ModalUI.Replace(this, Hide);
            IsOpen = true;
            panel.SetActive(true);
            if (saveManager == null) saveManager = Object.FindFirstObjectByType<SaveManager>();
            bool hasSave = saveManager != null && saveManager.HasSave;
            loadButton.interactable = hasSave;
            detailLabel.text = hasSave
                ? "Your shelter is still out there. Load your last save to continue,\nor restart from the preparation phase."
                : "Start again, prepare supplies and build a Water Collector.\nSave with F5 so your next run has a way back.";
            statusLabel.text = hasSave ? "F9 also loads your last saved game." : "No saved game available.";
        }

        /// <summary>RestoreVitals and validated loads use this to release death ownership.</summary>
        public void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            panel.SetActive(false);
            ModalUI.Close(this);
        }

        private void OnMessage(string message)
        {
            if (IsOpen) statusLabel.text = message;
        }

        private void BuildUI()
        {
            Canvas canvas = GameUI.CreateCanvas(transform, "GameOverCanvas", 200, true);
            panel = GameUI.Backdrop(canvas.transform, "GameOverPanel");
            Transform card = GameUI.Panel(panel.transform, "Card", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 568f), GameUI.PanelColor, true).transform;
            GameUI.Label(card, "Eyebrow", "RUN ENDED", TopLeft, TopLeft, new Vector2(36f, -28f),
                new Vector2(748f, 30f), 20, GameUI.Danger, bold: true);
            GameUI.Label(card, "Title", "You died", TopLeft, TopLeft, new Vector2(36f, -74f),
                new Vector2(748f, 66f), 50, bold: true);
            detailLabel = GameUI.Label(card, "Details", string.Empty, TopLeft, TopLeft,
                new Vector2(36f, -160f), new Vector2(748f, 86f), 21, GameUI.Muted);
            loadButton = GameUI.Button(card, "LoadButton", "Load saved game", TopLeft, TopLeft,
                new Vector2(36f, -276f), new Vector2(356f, 58f), () => { saveManager?.TryLoad(); }, true);
            GameUI.Button(card, "RestartButton", "Restart from beginning", TopLeft, TopLeft,
                new Vector2(428f, -276f), new Vector2(356f, 58f), RestartGame);
            statusLabel = GameUI.Label(card, "LoadStatus", string.Empty, TopLeft, TopLeft,
                new Vector2(36f, -364f), new Vector2(748f, 66f), 19, GameUI.Accent);
            GameUI.Label(card, "RestartHint", "Restart creates a fresh run. Your saved game is kept.", TopLeft, TopLeft,
                new Vector2(36f, -448f), new Vector2(748f, 30f), 17, GameUI.Muted);
            GameUI.Button(card, "QuitButton", "Quit", TopLeft, TopLeft,
                new Vector2(36f, -494f), new Vector2(160f, 44f), QuitGame);
            panel.SetActive(false);
        }

        private void RestartGame()
        {
            // Scene reload does not reset static timeScale. Release ownership before loading.
            Hide();
            Time.timeScale = 1f;
            Scene active = SceneManager.GetActiveScene();
            if (active.buildIndex >= 0)
            {
                SceneManager.LoadScene(active.buildIndex);
                return;
            }
#if UNITY_EDITOR
            // Direct Editor Play also works if the user has not added the scene to Build Settings.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(active.path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            Show();
            statusLabel.text = "This scene is missing from the build. Load a save or quit.";
#endif
        }

        private void QuitGame()
        {
            Hide();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
