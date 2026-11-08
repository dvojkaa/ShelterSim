using SheltreSim.Building;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// M15 — playtester/Opus-review feedback: the Bed build piece has been pure decoration
    /// since M2, with no sleep/rest function at all. BedPiece is attached directly to the
    /// prefab asset here (not to scene instances), the same precedent M11 used for
    /// WorkbenchStation/WaterCollectorPiece, so every placed copy — including the one M2
    /// already furnishes the starting bunker with — gets the behavior via the normal
    /// prefab-instantiate path. No external resource references needed, unlike Workbench, so
    /// fields are left at their defaults.
    /// </summary>
    public static class M15BedSetup
    {
        [MenuItem("Tools/SheltreSim/Setup M15 Bed")]
        public static void SetupBed()
        {
            const string path = "Assets/Prefabs/Building/BedPiece.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"M15: no prefab at {path} — run Tools > SheltreSim > Setup M2 Build System first.");
                return;
            }

            BedPiece bed = prefab.GetComponent<BedPiece>();
            if (bed == null)
            {
                bed = prefab.AddComponent<BedPiece>();
            }

            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("M15 ready. Bed: press E to sleep — heals 40 health at the cost of 60s of simulated hunger/thirst decay; blocked (for free) while a raider is within 15m.");
        }
    }
}
