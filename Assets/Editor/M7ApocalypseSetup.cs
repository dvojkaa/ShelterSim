using SheltreSim.Core;
using SheltreSim.Player;
using SheltreSim.Survival;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Wires M7 (apocalypse trigger + full loop glue) into an already-built scene: a
    /// GameStateManager that starts in the Building phase (survival decay paused) and a
    /// physical trigger object inside the bunker room that, once interacted with, starts
    /// the apocalypse sequence and hands control to the Survival phase.
    /// </summary>
    public static class M7ApocalypseSetup
    {
        [MenuItem("Tools/SheltreSim/Setup M7 Apocalypse Trigger")]
        public static void SetupApocalypseTrigger()
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
                Debug.LogError("'Player' GameObject has no SurvivalStats — run Tools > SheltreSim > Setup M3 Survival & Save first.");
                return;
            }

            GameObject gameStateObject = GameObject.Find("GameStateManager");
            if (gameStateObject == null)
            {
                gameStateObject = new GameObject("GameStateManager");
            }

            GameStateManager gameStateManager = gameStateObject.GetComponent<GameStateManager>();
            if (gameStateManager == null)
            {
                gameStateManager = gameStateObject.AddComponent<GameStateManager>();
            }
            gameStateManager.stats = stats;

            // Inside the bunker room (interior spans roughly X/Z in [-3, 3]), against the
            // north wall, clear of the south doorway and of where the Player spawns.
            GameObject trigger = GameObject.Find("ApocalypseButton");
            if (trigger == null)
            {
                trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trigger.name = "ApocalypseButton";
                trigger.transform.position = new Vector3(2.3f, 1f, 2.6f);
                trigger.transform.localScale = new Vector3(0.4f, 0.4f, 0.2f);
            }

            ApocalypseTrigger apocalypseTrigger = trigger.GetComponent<ApocalypseTrigger>();
            if (apocalypseTrigger == null)
            {
                apocalypseTrigger = trigger.AddComponent<ApocalypseTrigger>();
            }
            apocalypseTrigger.gameStateManager = gameStateManager;

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("M7 apocalypse trigger ready. Survival stats are paused during the Building phase — find the button inside the bunker (near the north wall) and press E to begin the apocalypse. Point of no return.");
        }
    }
}
