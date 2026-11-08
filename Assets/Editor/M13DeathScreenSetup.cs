using SheltreSim.Core;
using SheltreSim.Survival;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// M13 — an Opus-model review flagged that SurvivalStats.Die() froze the player with no
    /// on-screen feedback at all (no "you died" message, only F9-to-an-old-save as a way out,
    /// which doesn't exist if the player never saved). Wires a GameOverScreen component into the
    /// scene and points SurvivalStats.gameOverScreen at it. Run after M1 (needs "Player") and M3
    /// (needs SurvivalStats on the Player) have already run.
    /// </summary>
    public static class M13DeathScreenSetup
    {
        [MenuItem("Tools/SheltreSim/Setup M13 Death Screen")]
        public static void SetupDeathScreen()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("No 'Player' GameObject found — run Tools > SheltreSim > Build M1 Bunker Scene first.");
                return;
            }

            SurvivalStats stats = player.GetComponent<SurvivalStats>();
            if (stats == null)
            {
                Debug.LogError("'Player' has no SurvivalStats — run Tools > SheltreSim > Setup M3 Survival & Save first.");
                return;
            }

            GameObject gameOverScreenObject = GameObject.Find("GameOverScreen");
            if (gameOverScreenObject == null)
            {
                gameOverScreenObject = new GameObject("GameOverScreen");
            }

            GameOverScreen gameOverScreen = gameOverScreenObject.GetComponent<GameOverScreen>();
            if (gameOverScreen == null)
            {
                gameOverScreen = gameOverScreenObject.AddComponent<GameOverScreen>();
            }

            stats.gameOverScreen = gameOverScreen;

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("M13 ready. Dying now shows a 'You Died' screen with a Restart button (reloads the active scene).");
        }
    }
}
