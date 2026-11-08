using System.Collections.Generic;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// M11 — playtester feedback (verbatim, translated): "the workbench doesn't do anything,
    /// and the water collector should collect water." The Workbench/Water Collector build
    /// pieces (created by M2BuildSystemSetup as plain decoration prefabs) get a real
    /// component wired onto their prefab assets here, so every placed instance — including
    /// the starting Workbench M2 already furnishes the bunker with — gets the behavior for
    /// free. Run after M2 (prefabs must exist) and M10 (the weapon-upgrade/water resource
    /// assets the new components reference must exist) and before M8 (not a hard dependency,
    /// just keeps the setup-tool chain's "later tools can assume earlier content exists"
    /// convention consistent).
    /// </summary>
    public static class M11WorkbenchAndWaterSetup
    {
        [MenuItem("Tools/SheltreSim/Setup M11 Workbench And Water")]
        public static void SetupWorkbenchAndWater()
        {
            ResourceDefinition scrapMetal = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/ScrapMetal.asset");
            ResourceDefinition wood = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/Wood.asset");
            ResourceDefinition weaponUpgrade = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/WeaponUpgrade.asset");
            ResourceDefinition water = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/Water.asset");

            if (scrapMetal == null || wood == null || weaponUpgrade == null || water == null)
            {
                Debug.LogError("M11: missing resource definitions under Assets/Data/Resources/ — run Tools > SheltreSim > Setup M4 Outdoor Area and Setup M10 Consumables And Loot first.");
                return;
            }

            SetupWorkbench(scrapMetal, wood, weaponUpgrade);
            SetupWaterCollector(water);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("M11 ready. Workbench: press E to craft a Makeshift Blade (2 Scrap Metal, 2 Wood) — permanently boosts melee damage once Equipped from the inventory. Water Collector: accumulates 1 Water every 30s (caps at 5) — press E to collect.");
        }

        /// <summary>
        /// Attached directly to the prefab asset (not to scene instances) so every placed
        /// copy — the one M2 furnishes the bunker with at the start, and every one the
        /// player later builds — gets the component via the normal prefab-instantiate path,
        /// the same way PlacedPiece already does.
        /// </summary>
        private static void SetupWorkbench(ResourceDefinition scrapMetal, ResourceDefinition wood, ResourceDefinition weaponUpgrade)
        {
            const string path = "Assets/Prefabs/Building/WorkbenchPiece.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"M11: no prefab at {path} — run Tools > SheltreSim > Setup M2 Build System first.");
                return;
            }

            WorkbenchStation station = prefab.GetComponent<WorkbenchStation>();
            if (station == null)
            {
                station = prefab.AddComponent<WorkbenchStation>();
            }

            station.craftingCost = new List<CraftingCost>
            {
                new CraftingCost { resource = scrapMetal, amount = 2 },
                new CraftingCost { resource = wood, amount = 2 },
            };
            station.output = weaponUpgrade;
            station.outputAmount = 1;

            EditorUtility.SetDirty(prefab);
        }

        private static void SetupWaterCollector(ResourceDefinition water)
        {
            const string path = "Assets/Prefabs/Building/WaterCollectorPiece.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"M11: no prefab at {path} — run Tools > SheltreSim > Setup M2 Build System first.");
                return;
            }

            WaterCollectorPiece collector = prefab.GetComponent<WaterCollectorPiece>();
            if (collector == null)
            {
                collector = prefab.AddComponent<WaterCollectorPiece>();
            }

            collector.waterResource = water;

            EditorUtility.SetDirty(prefab);
        }
    }
}
