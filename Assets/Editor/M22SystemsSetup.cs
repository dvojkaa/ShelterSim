using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SheltreSim.Building;
using SheltreSim.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// M22: prefabs and definitions for power, automation, radio and defence pieces, working components on the
    /// generator and storage crate that were decoration until now, StructureHealth on every piece, and weapon
    /// viewmodel prefabs. Runtime systems create themselves, so nothing else in the scene changes. Idempotent.
    /// Models come from ArtSource: missing ones stay coloured boxes until the FBX exists (M21 BuildPrefab).
    /// </summary>
    public static class M22SystemsSetup
    {
        private const string PiecesPath = "Assets/Data/BuildPieces/";
        private const string PrefabsPath = "Assets/Prefabs/Building/";
        private const string WeaponModels = "Assets/Art/Models/";
        private const string WeaponPrefabs = "Assets/Art/Resources/Weapons/";

        private sealed class Row
        {
            public string id, name, asset, type; public Vector3 size; public Color color; public float hp; public bool trigger;
            public Row(string id, string name, string asset, string type, float w, float h, float d, Color color, float hp, bool trigger = false)
            { this.id = id; this.name = name; this.asset = asset; this.type = type; size = new Vector3(w, h, d); this.color = color; this.hp = hp; this.trigger = trigger; }
        }

        private static readonly Color Machine = new Color(.35f, .4f, .42f), Power = new Color(.75f, .6f, .15f), Danger = new Color(.6f, .2f, .15f), Sand = new Color(.6f, .52f, .36f);

        private static readonly Row[] Rows =
        {
            new Row("piece_armory", "Armory", "Armory", "SheltreSim.Building.WorkbenchStation", 1.6f, 1.1f, .8f, new Color(.4f, .3f, .2f), 150f),
            new Row("piece_console", "Power Console", "PowerConsole", "SheltreSim.Power.PowerConsolePiece", .8f, 1.4f, .4f, Power, 80f),
            new Row("piece_solar", "Solar Panel", "SolarPanel", "SheltreSim.Power.SolarPanelPiece", 2f, 1.2f, 2f, new Color(.15f, .2f, .4f), 80f),
            new Row("piece_battery", "Battery Bank", "BatteryBank", "SheltreSim.Power.BatteryPiece", 1f, 1.3f, .6f, Power, 120f),
            new Row("piece_cooler", "Cooling Unit", "CoolingUnit", "SheltreSim.Power.CoolingUnitPiece", 1.2f, 1.6f, .8f, new Color(.4f, .65f, .75f), 120f),
            new Row("piece_charger", "Cell Charger", "CellCharger", "SheltreSim.Power.CellChargerPiece", .8f, 1.1f, .6f, new Color(.3f, .7f, .6f), 100f),
            new Row("piece_pump", "Water Pump", "WaterPump", "SheltreSim.Power.WaterPumpPiece", .8f, 1.2f, .8f, new Color(.25f, .45f, .7f), 120f),
            new Row("piece_irrigator", "Irrigator", "Irrigator", "SheltreSim.Power.IrrigatorPiece", .6f, 1.5f, .6f, new Color(.3f, .55f, .4f), 60f),
            new Row("piece_fabricator", "Fabricator", "Fabricator", "SheltreSim.Power.FabricatorPiece", 1.6f, 1.6f, 1f, Machine, 200f),
            new Row("piece_repair", "Repair Station", "RepairStation", "SheltreSim.Power.RepairStationPiece", 1f, 1.8f, 1f, new Color(.8f, .5f, .15f), 150f),
            new Row("piece_radio_console", "Radio Console", "RadioConsole", "SheltreSim.Objectives.RadioConsolePiece", 1.2f, 1.4f, .7f, new Color(.3f, .35f, .25f), 80f),
            new Row("piece_sandbag", "Sandbag Wall", "SandbagWall", null, 2f, 1.1f, .6f, Sand, 260f),
            new Row("piece_spike_trap", "Spike Trap", "SpikeTrap", "SheltreSim.Raids.SpikeTrapPiece", 2f, .25f, 2f, Danger, 300f, true),
            new Row("piece_shock_trap", "Shock Trap", "ShockTrap", "SheltreSim.Raids.ShockTrapPiece", 2f, .2f, 2f, new Color(.7f, .7f, .2f), 200f, true),
            new Row("piece_turret_mg", "Machine Gun Turret", "TurretMG", "SheltreSim.Raids.MachineGunTurretPiece", 1f, 1.5f, 1f, Danger, 220f),
            new Row("piece_turret_laser", "Laser Turret", "TurretLaser", "SheltreSim.Raids.LaserTurretPiece", 1f, 1.6f, 1f, new Color(.3f, .5f, .8f), 180f),
            new Row("piece_alarm", "Raid Alarm", "RaidAlarm", "SheltreSim.Raids.AlarmPiece", .5f, 1.8f, .5f, Danger, 60f),
        };

        // Pieces made by older milestones: hit points only, plus the behaviour that was missing.
        private static readonly Dictionary<string, float> OlderPieceHealth = new Dictionary<string, float>
        {
            { "wall_segment", 180f }, { "floor_tile", 150f }, { "workbench", 150f }, { "bed", 80f }, { "storage_crate", 120f },
            { "water_collector", 80f }, { "generator", 150f }, { "piece_stove", 150f }, { "piece_forge", 220f }, { "piece_chemlab", 120f },
            { "piece_electronics", 120f }, { "piece_farm", 60f }, { "piece_wall_steel", 500f }, { "piece_lamp", 30f }, { "piece_shelf", 60f },
            { "piece_filter", 80f }, { "piece_radio", 150f },
        };

        [MenuItem("Tools/SheltreSim/Setup M22 Systems")]
        public static void Setup()
        {
            Material atlas = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/SS_Atlas.mat");
            var added = new List<BuildPieceDefinition>();
            foreach (Row row in Rows) added.Add(BuildPiece(row, atlas));
            UpgradeOlderPieces();
            BuildWeaponPrefabs(atlas);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WireScene(added);
            Debug.Log($"[M22] systems setup complete: {added.Count} new pieces.");
        }

        private static Type ResolveType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(name);
                if (type != null) return type;
            }
            throw new InvalidOperationException("[M22] missing component type " + name);
        }

        private static BuildPieceDefinition BuildPiece(Row row, Material atlas)
        {
            var spec = new M21CraftingSetup.PieceSpec
            {
                id = row.id, name = row.name, prefabName = row.asset + "Piece", assetName = row.asset, size = row.size, color = row.color,
                behaviours = root =>
                {
                    if (row.type != null)
                    {
                        Component component = root.AddComponent(ResolveType(row.type));
                        if (component is WorkbenchStation station) station.kind = SheltreSim.Crafting.StationKind.Armory;
                    }
                    if (row.trigger) root.GetComponent<BoxCollider>().isTrigger = true;
                    root.AddComponent<StructureHealth>().maxHealth = row.hp;
                }
            };
            GameObject prefab = M21CraftingSetup.BuildPrefab(spec, atlas);
            string path = PiecesPath + row.asset + ".asset";
            BuildPieceDefinition def = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<BuildPieceDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            def.id = row.id; def.displayName = row.name; def.prefab = prefab; def.footprintSize = row.size;
            def.craftingCost = M21CraftingSetup.CostFor(row.id);
            EditorUtility.SetDirty(def);
            return def;
        }

        private static void UpgradeOlderPieces()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:BuildPieceDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.prefab == null || !OlderPieceHealth.TryGetValue(def.id, out float hp)) continue;
                string path = AssetDatabase.GetAssetPath(def.prefab);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    StructureHealth health = root.GetComponent<StructureHealth>() ?? root.AddComponent<StructureHealth>();
                    health.maxHealth = hp;
                    if (def.id == "generator" && root.GetComponent(ResolveType("SheltreSim.Power.GeneratorPiece")) == null)
                        root.AddComponent(ResolveType("SheltreSim.Power.GeneratorPiece"));
                    if (def.id == "storage_crate" && root.GetComponent(ResolveType("SheltreSim.Storage.StorageCratePiece")) == null)
                        root.AddComponent(ResolveType("SheltreSim.Storage.StorageCratePiece"));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        // Weapon models live outside Resources with the rest of the art, so the runtime loads a prefab of each.
        private static void BuildWeaponPrefabs(Material atlas)
        {
            Directory.CreateDirectory(WeaponPrefabs.TrimEnd('/'));
            foreach (var spec in SheltreSim.Crafting.ItemCatalog.All.Where(s => s.ranged))
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponModels + "weapon_" + spec.id + ".fbx");
                string path = WeaponPrefabs + spec.id + ".prefab";
                if (model == null)
                {
                    if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                    continue;
                }
                GameObject instance = UnityEngine.Object.Instantiate(model);
                try
                {
                    instance.name = spec.id;
                    foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true)) if (atlas != null) r.sharedMaterial = atlas;
                    foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
                    PrefabUtility.SaveAsPrefabAsset(instance, path);
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
        }

        private static void WireScene(List<BuildPieceDefinition> added)
        {
            var build = UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
            if (build == null) { Debug.LogWarning("[M22] no built scene: assets written, wiring skipped."); return; }
            foreach (BuildPieceDefinition def in added) if (!build.availablePieces.Contains(def)) build.availablePieces.Add(def);
            EditorUtility.SetDirty(build);
            var inventory = UnityEngine.Object.FindFirstObjectByType<InventoryUI>();
            if (inventory != null)
            {
                foreach (BuildPieceDefinition piece in build.availablePieces)
                    if (piece != null && !inventory.knownRecipes.Contains(piece)) inventory.knownRecipes.Add(piece);
                EditorUtility.SetDirty(inventory);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
    }
}
