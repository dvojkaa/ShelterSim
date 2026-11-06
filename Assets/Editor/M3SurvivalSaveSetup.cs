using SheltreSim.Building;
using SheltreSim.Player;
using SheltreSim.Saving;
using SheltreSim.Survival;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Wires M3 (survival stats, HUD, save/load) into an already-built M1+M2 scene.
    /// Run after "Tools > SheltreSim > Build M1 Bunker Scene" and
    /// "Tools > SheltreSim > Setup M2 Build System" have already produced a scene with
    /// a "Player" GameObject and its "BuildSystem" child.
    /// </summary>
    public static class M3SurvivalSaveSetup
    {
        [MenuItem("Tools/SheltreSim/Setup M3 Survival & Save")]
        public static void SetupSurvivalAndSave()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("No 'Player' GameObject found — run Tools > SheltreSim > Build M1 Bunker Scene first.");
                return;
            }

            FirstPersonController firstPersonController = player.GetComponent<FirstPersonController>();
            if (firstPersonController == null)
            {
                Debug.LogError("'Player' GameObject has no FirstPersonController — run Tools > SheltreSim > Build M1 Bunker Scene first.");
                return;
            }

            Transform buildSystemTransform = player.transform.Find("BuildSystem");
            BuildModeController buildController = buildSystemTransform != null
                ? buildSystemTransform.GetComponent<BuildModeController>()
                : null;
            if (buildController == null)
            {
                Debug.LogError("No 'BuildSystem' child with a BuildModeController found — run Tools > SheltreSim > Setup M2 Build System first.");
                return;
            }

            SurvivalStats stats = player.GetComponent<SurvivalStats>();
            if (stats == null)
            {
                stats = player.AddComponent<SurvivalStats>();
            }

            GameObject hud = GameObject.Find("HUD");
            if (hud == null)
            {
                hud = new GameObject("HUD");
            }

            SurvivalHUD survivalHud = hud.GetComponent<SurvivalHUD>();
            if (survivalHud == null)
            {
                survivalHud = hud.AddComponent<SurvivalHUD>();
            }
            survivalHud.stats = stats;

            GameObject saveManagerObject = GameObject.Find("SaveManager");
            if (saveManagerObject == null)
            {
                saveManagerObject = new GameObject("SaveManager");
            }

            SaveManager saveManager = saveManagerObject.GetComponent<SaveManager>();
            if (saveManager == null)
            {
                saveManager = saveManagerObject.AddComponent<SaveManager>();
            }
            saveManager.player = firstPersonController;
            saveManager.stats = stats;
            saveManager.buildController = buildController;

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("M3 survival & save ready. F5 saves, F9 loads. Hunger/thirst drain over time; health drains if either hits 0; HUD bars are top-left of the screen.");
        }
    }
}
