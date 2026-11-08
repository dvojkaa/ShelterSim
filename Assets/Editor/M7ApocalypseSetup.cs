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
                EditorMaterialUtility.Apply(trigger, new Color(0.9f, 0.1f, 0.1f));

                BuildButtonDetailing(trigger.transform);
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

        /// <summary>
        /// Purely decorative mounting detail built around the fixed button at (2.3, 1, 2.6):
        /// a backing panel (bigger than the button, so its edges read as a raised rim/frame once
        /// the button sits in front of it) plus a hazard-stripe accent bar. All built as
        /// standalone world-space objects — NOT parented under the button — specifically to
        /// avoid compounding with the button's own non-uniform localScale (0.4, 0.4, 0.2); a
        /// child's localScale would otherwise be multiplied by that scale and come out warped.
        /// Deliberately does not touch <paramref name="buttonTransform"/> itself: its position
        /// and BoxCollider are exactly what PlayerInteractor's raycast and the trigger's own
        /// prompt depend on.
        /// </summary>
        private static void BuildButtonDetailing(Transform buttonTransform)
        {
            Vector3 buttonPos = buttonTransform.position;

            // Backing panel sits in the gap between the button's back face (buttonPos.z + 0.1)
            // and the north wall's inner face (z = 2.9), centered so it never touches either.
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "ApocalypseButton_Panel";
            panel.transform.position = new Vector3(buttonPos.x, buttonPos.y, buttonPos.z + 0.08f);
            panel.transform.localScale = new Vector3(0.6f, 0.6f, 0.04f);
            EditorMaterialUtility.Apply(panel, new Color(0.15f, 0.15f, 0.16f));
            Object.DestroyImmediate(panel.GetComponent<Collider>());

            // Thin yellow hazard-stripe accent along the panel's top edge.
            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "ApocalypseButton_HazardStripe";
            stripe.transform.position = new Vector3(buttonPos.x, buttonPos.y + 0.26f, buttonPos.z + 0.07f);
            stripe.transform.localScale = new Vector3(0.6f, 0.06f, 0.06f);
            EditorMaterialUtility.Apply(stripe, new Color(0.85f, 0.7f, 0.1f));
            Object.DestroyImmediate(stripe.GetComponent<Collider>());
        }
    }
}
