using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Wires M5 (crafting) into an already-built M1+M2+M4 scene: assigns real resource
    /// costs to the existing build-piece definitions and connects BuildModeController to
    /// the Player's Inventory. Run after M1/M2/M4 setup tools.
    /// </summary>
    public static class M5CraftingSetup
    {
        [MenuItem("Tools/SheltreSim/Setup M5 Crafting")]
        public static void SetupCrafting()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("No 'Player' GameObject found — run Tools > SheltreSim > Build M1 Bunker Scene first.");
                return;
            }

            if (player.GetComponent<FirstPersonController>() == null)
            {
                Debug.LogError("'Player' GameObject has no FirstPersonController — run Tools > SheltreSim > Build M1 Bunker Scene first.");
                return;
            }

            Inventory inventory = player.GetComponent<Inventory>();
            if (inventory == null)
            {
                Debug.LogError("'Player' GameObject has no Inventory — run Tools > SheltreSim > Setup M4 Outdoor Area first.");
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

            ResourceDefinition wood = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/Wood.asset");
            ResourceDefinition scrapMetal = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/ScrapMetal.asset");
            if (wood == null || scrapMetal == null)
            {
                Debug.LogError("Resource definitions not found at Assets/Data/Resources/ — run Tools > SheltreSim > Setup M4 Outdoor Area first.");
                return;
            }

            BuildPieceDefinition wallSegment = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>("Assets/Data/BuildPieces/WallSegment.asset");
            BuildPieceDefinition floorTile = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>("Assets/Data/BuildPieces/FloorTile.asset");
            if (wallSegment == null || floorTile == null)
            {
                Debug.LogError("Build piece definitions not found at Assets/Data/BuildPieces/ — run Tools > SheltreSim > Setup M2 Build System first.");
                return;
            }

            wallSegment.craftingCost = new System.Collections.Generic.List<CraftingCost>
            {
                new CraftingCost { resource = wood, amount = 2 },
            };
            floorTile.craftingCost = new System.Collections.Generic.List<CraftingCost>
            {
                new CraftingCost { resource = scrapMetal, amount = 1 },
            };

            EditorUtility.SetDirty(wallSegment);
            EditorUtility.SetDirty(floorTile);

            BuildPieceDefinition workbench = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>("Assets/Data/BuildPieces/Workbench.asset");
            BuildPieceDefinition bed = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>("Assets/Data/BuildPieces/Bed.asset");
            BuildPieceDefinition storageCrate = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>("Assets/Data/BuildPieces/StorageCrate.asset");
            BuildPieceDefinition waterCollector = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>("Assets/Data/BuildPieces/WaterCollector.asset");
            BuildPieceDefinition generator = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>("Assets/Data/BuildPieces/Generator.asset");
            if (workbench == null || bed == null || storageCrate == null || waterCollector == null || generator == null)
            {
                Debug.LogError("Build piece definitions not found at Assets/Data/BuildPieces/ — run Tools > SheltreSim > Setup M2 Build System first.");
                return;
            }

            workbench.craftingCost = new System.Collections.Generic.List<CraftingCost>
            {
                new CraftingCost { resource = wood, amount = 1 },
                new CraftingCost { resource = scrapMetal, amount = 1 },
            };
            bed.craftingCost = new System.Collections.Generic.List<CraftingCost>
            {
                new CraftingCost { resource = wood, amount = 2 },
            };
            storageCrate.craftingCost = new System.Collections.Generic.List<CraftingCost>
            {
                new CraftingCost { resource = wood, amount = 1 },
            };
            waterCollector.craftingCost = new System.Collections.Generic.List<CraftingCost>
            {
                new CraftingCost { resource = scrapMetal, amount = 2 },
            };
            generator.craftingCost = new System.Collections.Generic.List<CraftingCost>
            {
                new CraftingCost { resource = scrapMetal, amount = 3 },
                new CraftingCost { resource = wood, amount = 1 },
            };

            EditorUtility.SetDirty(workbench);
            EditorUtility.SetDirty(bed);
            EditorUtility.SetDirty(storageCrate);
            EditorUtility.SetDirty(waterCollector);
            EditorUtility.SetDirty(generator);

            buildController.inventory = inventory;

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("M5 crafting ready. Wall Segment: 2 Wood. Floor Tile: 1 Scrap Metal. Workbench: 1 Wood + 1 Scrap Metal. Bed: 2 Wood. Storage Crate: 1 Wood. Water Collector: 2 Scrap Metal. Generator: 3 Scrap Metal + 1 Wood.");
        }
    }
}
