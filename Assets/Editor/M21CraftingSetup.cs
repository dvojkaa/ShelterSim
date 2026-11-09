using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Saving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// M21 — "more items, more crafting". Writes one ResourceDefinition asset per ItemCatalog entry (existing assets
    /// keep their GUIDs), rewrites build costs from RecipeBook.PieceCosts, builds the new station/utility prefabs,
    /// and wires CraftingUI + PlayerGear into the scene. Idempotent. Run after M2, M5, M8, M10, M11 (it edits what they made)
    /// and after M16 (art pass); it only needs the scene to be the built M1 scene for the wiring half.
    /// </summary>
    public static class M21CraftingSetup
    {
        private const string ResourcesPath = "Assets/Data/Resources/";
        private const string PiecesPath = "Assets/Data/BuildPieces/";
        private const string PrefabsPath = "Assets/Prefabs/Building/";
        private const string ModelsPath = "Assets/Art/Models/";

        internal sealed class PieceSpec
        {
            public string id, name, prefabName, assetName;
            public Vector3 size;
            public Color color;
            public Action<GameObject> behaviours;
        }

        [MenuItem("Tools/SheltreSim/Setup M21 Crafting")]
        public static void Setup()
        {
            Directory.CreateDirectory(ResourcesPath.TrimEnd('/'));
            Directory.CreateDirectory(PiecesPath.TrimEnd('/'));
            Directory.CreateDirectory(PrefabsPath.TrimEnd('/'));

            WriteItems();
            ItemCatalog.InvalidateCache();

            List<BuildPieceDefinition> allDefs = new List<BuildPieceDefinition>();
            UpdateExistingPieces(allDefs);
            List<BuildPieceDefinition> added = CreateNewPieces(allDefs);
            SetStationKindOnWorkbench();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WireScene(added);

            List<string> errors = RecipeBook.Validate(out string summary);
            foreach (string e in errors) Debug.LogError("[M21] validation: " + e);
            Debug.Log("[M21] " + summary);
            Debug.Log($"[M21] crafting setup complete: {ItemCatalog.All.Count} items, {RecipeBook.All.Count} recipes, {added.Count} new pieces.");
        }

        // ---------- Items ----------

        private static string Pascal(string id) => string.Concat(id.Split('_').Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));

        private static void WriteItems()
        {
            Dictionary<string, ResourceDefinition> existing = new Dictionary<string, ResourceDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:ResourceDefinition"))
            {
                ResourceDefinition d = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (d != null && !string.IsNullOrEmpty(d.id) && !existing.ContainsKey(d.id)) existing[d.id] = d;
            }

            foreach (ItemSpec spec in ItemCatalog.All)
            {
                if (!existing.TryGetValue(spec.id, out ResourceDefinition def))
                {
                    def = ScriptableObject.CreateInstance<ResourceDefinition>();
                    AssetDatabase.CreateAsset(def, ResourcesPath + (spec.assetName ?? Pascal(spec.id)) + ".asset");
                }
                def.id = spec.id;
                def.displayName = spec.name;
                def.description = spec.description;
                def.category = spec.category;
                def.isFood = spec.food; def.hungerRestored = spec.hunger;
                def.isDrink = spec.drink; def.thirstRestored = spec.thirst;
                def.isMedicine = spec.medicine; def.healAmount = spec.heal;
                def.isWeapon = spec.weapon; def.weaponDamage = spec.damage; def.damageBonus = 0f;
                def.isBackpackUpgrade = spec.backpackUpgrade;
                def.toolKind = spec.tool; def.toolTier = spec.tier; def.toolBonus = spec.bonus;
                def.gearSlot = spec.slot; def.damageReduction = spec.reduction; def.warmth = spec.warmth;
                EditorUtility.SetDirty(def);
            }
            AssetDatabase.SaveAssets();
        }

        // ---------- Pieces ----------

        private static ResourceDefinition Def(string id)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ResourceDefinition"))
            {
                ResourceDefinition d = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (d != null && d.id == id) return d;
            }
            throw new InvalidOperationException("M21: no ResourceDefinition for " + id);
        }

        internal static List<CraftingCost> CostFor(string pieceId)
        {
            List<CraftingCost> cost = new List<CraftingCost>();
            foreach (Ingredient i in RecipeBook.PieceCosts[pieceId]) cost.Add(new CraftingCost { resource = Def(i.id), amount = i.amount });
            return cost;
        }

        private static void UpdateExistingPieces(List<BuildPieceDefinition> all)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:BuildPieceDefinition"))
            {
                BuildPieceDefinition def = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null) continue;
                all.Add(def);
                if (RecipeBook.PieceCosts.ContainsKey(def.id))
                {
                    def.craftingCost = CostFor(def.id);
                    EditorUtility.SetDirty(def);
                }
            }
        }

        private static List<PieceSpec> PieceSpecs() => new List<PieceSpec>
        {
            new PieceSpec { id = "piece_stove", name = "Stove", prefabName = "StovePiece", assetName = "Stove", size = new Vector3(1f, 1f, 0.8f), color = new Color(0.25f, 0.25f, 0.27f),
                behaviours = g => g.AddComponent<WorkbenchStation>().kind = StationKind.Stove },
            new PieceSpec { id = "piece_forge", name = "Forge", prefabName = "ForgePiece", assetName = "Forge", size = new Vector3(1.2f, 1.2f, 1f), color = new Color(0.45f, 0.2f, 0.12f),
                behaviours = g => g.AddComponent<WorkbenchStation>().kind = StationKind.Forge },
            new PieceSpec { id = "piece_chemlab", name = "Chem Lab", prefabName = "ChemLabPiece", assetName = "ChemLab", size = new Vector3(1.6f, 1f, 0.8f), color = new Color(0.3f, 0.5f, 0.45f),
                behaviours = g => g.AddComponent<WorkbenchStation>().kind = StationKind.ChemLab },
            new PieceSpec { id = "piece_electronics", name = "Electronics Bench", prefabName = "ElectronicsBenchPiece", assetName = "ElectronicsBench", size = new Vector3(1.6f, 1f, 0.8f), color = new Color(0.2f, 0.3f, 0.55f),
                behaviours = g => g.AddComponent<WorkbenchStation>().kind = StationKind.Electronics },
            new PieceSpec { id = "piece_farm", name = "Farm Plot", prefabName = "FarmPlotPiece", assetName = "FarmPlot", size = new Vector3(2f, 0.3f, 2f), color = new Color(0.32f, 0.22f, 0.12f),
                behaviours = g => g.AddComponent<FarmPlot>() },
            new PieceSpec { id = "piece_wall_steel", name = "Steel Wall", prefabName = "SteelWallPiece", assetName = "SteelWall", size = new Vector3(2f, 3f, 0.2f), color = new Color(0.5f, 0.52f, 0.56f) },
            new PieceSpec { id = "piece_lamp", name = "Lamp", prefabName = "LampPiece", assetName = "Lamp", size = new Vector3(0.4f, 0.6f, 0.4f), color = new Color(0.95f, 0.85f, 0.5f),
                behaviours = g =>
                {
                    GameObject lightObject = new GameObject("PointLight");
                    lightObject.transform.SetParent(g.transform, false);
                    lightObject.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                    Light light = lightObject.AddComponent<Light>();
                    light.type = LightType.Point; light.range = 7f; light.intensity = 1.3f;
                    light.color = new Color(1f, 0.86f, 0.62f); light.shadows = LightShadows.None;
                } },
            new PieceSpec { id = "piece_shelf", name = "Shelf", prefabName = "ShelfPiece", assetName = "Shelf", size = new Vector3(1.6f, 1.8f, 0.5f), color = new Color(0.45f, 0.33f, 0.2f) },
            new PieceSpec { id = "piece_filter", name = "Water Filter", prefabName = "WaterFilterPiece", assetName = "WaterFilter", size = new Vector3(0.8f, 1.2f, 0.8f), color = new Color(0.3f, 0.5f, 0.65f),
                behaviours = g => g.AddComponent<WaterFilterPiece>() },
            new PieceSpec { id = "piece_radio", name = "Radio Beacon", prefabName = "RadioBeaconPiece", assetName = "RadioBeacon", size = new Vector3(0.8f, 1.6f, 0.8f), color = new Color(0.7f, 0.2f, 0.2f),
                behaviours = g => g.AddComponent<RadioBeaconPiece>() },
        };

        private static List<BuildPieceDefinition> CreateNewPieces(List<BuildPieceDefinition> all)
        {
            List<BuildPieceDefinition> result = new List<BuildPieceDefinition>();
            Material atlas = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/SS_Atlas.mat");
            foreach (PieceSpec spec in PieceSpecs())
            {
                GameObject prefab = BuildPrefab(spec, atlas);
                BuildPieceDefinition def = all.FirstOrDefault(d => d != null && d.id == spec.id);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<BuildPieceDefinition>();
                    AssetDatabase.CreateAsset(def, PiecesPath + spec.assetName + ".asset");
                    all.Add(def);
                }
                def.id = spec.id; def.displayName = spec.name; def.prefab = prefab; def.footprintSize = spec.size;
                def.craftingCost = CostFor(spec.id);
                EditorUtility.SetDirty(def);
                result.Add(def);
            }
            return result;
        }

        /// <summary>Root = footprint box collider + PlacedPiece (like M2's pieces); the model (or a coloured box if it is missing) is a collider-free child standing on the footprint floor.</summary>
        internal static GameObject BuildPrefab(PieceSpec spec, Material atlas)
        {
            GameObject root = new GameObject(spec.prefabName);
            try
            {
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.size = spec.size;
                root.AddComponent<PlacedPiece>().pieceId = spec.id;

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsPath + spec.id + ".fbx");
                GameObject visual;
                if (model != null)
                {
                    visual = UnityEngine.Object.Instantiate(model);
                    foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true)) if (atlas != null) r.sharedMaterial = atlas;
                    foreach (Collider c in visual.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
                    visual.transform.SetParent(root.transform, false);
                    visual.transform.localPosition = new Vector3(0f, -spec.size.y / 2f, 0f);
                }
                else
                {
                    Debug.LogWarning($"[M21] no model at {ModelsPath}{spec.id}.fbx, using a coloured box for {spec.name}");
                    visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                    visual.transform.SetParent(root.transform, false);
                    visual.transform.localScale = spec.size;
                    EditorMaterialUtility.Apply(visual, spec.color);
                }
                visual.name = "Visual";

                spec.behaviours?.Invoke(root);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabsPath + spec.prefabName + ".prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void SetStationKindOnWorkbench()
        {
            const string path = PrefabsPath + "WorkbenchPiece.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                WorkbenchStation station = root.GetComponent<WorkbenchStation>() ?? root.AddComponent<WorkbenchStation>();
                station.kind = StationKind.Workbench;
                station.craftingCost = new List<CraftingCost>();
                station.output = null;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ---------- Scene wiring ----------

        private static void WireScene(List<BuildPieceDefinition> added)
        {
            BuildModeController build = UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
            FirstPersonController player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            if (build == null || player == null)
            {
                Debug.LogWarning("[M21] no built scene (BuildModeController/Player missing): assets written, scene wiring skipped.");
                return;
            }
            foreach (BuildPieceDefinition def in added)
                if (!build.availablePieces.Contains(def)) build.availablePieces.Add(def);
            EditorUtility.SetDirty(build);

            PlayerGear.Ensure(player.gameObject);
            Inventory inventory = player.GetComponent<Inventory>();
            SheltreSim.Survival.SurvivalStats stats = player.GetComponent<SheltreSim.Survival.SurvivalStats>();

            GameObject uiObject = GameObject.Find("CraftingUI");
            if (uiObject == null) uiObject = new GameObject("CraftingUI");
            CraftingUI crafting = uiObject.GetComponent<CraftingUI>() ?? uiObject.AddComponent<CraftingUI>();
            crafting.inventory = inventory; crafting.stats = stats; crafting.buildModeController = build;
            EditorUtility.SetDirty(crafting);

            InventoryUI inventoryUi = UnityEngine.Object.FindFirstObjectByType<InventoryUI>();
            if (inventoryUi != null)
            {
                inventoryUi.craftingUi = crafting;
                foreach (BuildPieceDefinition piece in build.availablePieces)
                    if (piece != null && !inventoryUi.knownRecipes.Contains(piece)) inventoryUi.knownRecipes.Add(piece);
                EditorUtility.SetDirty(inventoryUi);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
    }
}
