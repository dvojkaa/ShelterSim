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
        private const string PropPrefabs = "Assets/Art/Resources/Props/";
        private const string EnemyPrefabs = "Assets/Art/Resources/Enemies/";
        private const string DamageStagePrefabs = "Assets/Art/Resources/DamageStages/";
        /// <summary>Piece id to GPT's intact model id, whose _damaged and _critical versions StructureDamageVisual swaps in.</summary>
        private static readonly (string pieceId, string modelId)[] DamageStagePieces =
        {
            ("wall_segment", "piece_wall"), ("piece_wall_steel", "piece_wall_steel"),
            ("piece_wall_reinforced", "piece_wall_reinforced"),
            ("piece_wall_brick", "piece_wall_brick"),
            ("piece_wall_concrete", "piece_wall_concrete"),
            ("piece_door_wood", "piece_door_wood"), ("piece_door_steel", "piece_door_steel"),
            ("piece_fence", "piece_fence"), ("piece_gate", "piece_gate"),
            ("piece_barricade", "piece_barricade"), ("piece_palisade", "piece_palisade"),
        };
        private static readonly string[] PropIds = { "loot_bag", "loot_boss_cache", "item_fuel_can", "item_canned_food", "item_bottle", "item_medkit", "item_scrap_bundle", "ammo_box_pistol", "ammo_box_rifle", "ammo_box_shell", "energy_cell", "fx_acid_glob", "fx_chem_flask", "melee_makeshift_blade", "melee_spiked_club", "melee_machete", "melee_fire_axe", "melee_steel_sword", "melee_hammer", "melee_iron_axe", "melee_iron_pickaxe" };

        private sealed class Row
        {
            public string id, name, asset, type; public Vector3 size; public Color color; public float hp; public bool trigger;
            public Row(string id, string name, string asset, string type, float w, float h, float d, Color color, float hp, bool trigger = false)
            { this.id = id; this.name = name; this.asset = asset; this.type = type; size = new Vector3(w, h, d); this.color = color; this.hp = hp; this.trigger = trigger; }
        }

        private static readonly Color Machine = new Color(.35f, .4f, .42f), Power = new Color(.75f, .6f, .15f), Danger = new Color(.6f, .2f, .15f), Sand = new Color(.6f, .52f, .36f);

        private static readonly Row[] Rows =
        {
            new Row("piece_foundation_reinforced", "Reinforced Foundation", "ReinforcedFoundation", null, 2f, .5f, 2f, new Color(.42f, .44f, .43f), 1100f),
            new Row("piece_wall_reinforced", "Reinforced Wall", "ReinforcedWall", null, 2f, 3f, .2f, new Color(.48f, .49f, .46f), 1250f),
            new Row("piece_foundation_concrete", "Concrete Foundation", "ConcreteFoundation", null, 2f, .6f, 2f, new Color(.53f, .55f, .54f), 2200f),
            new Row("piece_wall_concrete", "Concrete Wall", "ConcreteWall", null, 2f, 3f, .2f, new Color(.58f, .59f, .57f), 2400f),
            new Row("piece_foundation_brick", "Brick Foundation", "BrickFoundation", null, 2f, .45f, 2f, new Color(.52f, .3f, .22f), 850f),
            new Row("piece_wall_brick", "Brick Wall", "BrickWall", null, 2f, 3f, .2f, new Color(.58f, .32f, .24f), 900f),
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
            new Row("piece_garage_bay", "Garage Workshop", "GarageBay", "SheltreSim.Power.GarageWorkshopPiece", 6f, 2.5f, 6f, new Color(.38f, .4f, .39f), 500f),
            new Row("piece_radio_console", "Radio Console", "RadioConsole", "SheltreSim.Objectives.RadioConsolePiece", 1.2f, 1.4f, .7f, new Color(.3f, .35f, .25f), 80f),
            new Row("piece_bicycle", "Scrap Bicycle", "ScrapBicycle", "SheltreSim.World.RideableBicycle", .7f, 1.3f, 1.8f, new Color(.25f, .3f, .28f), 100f),
            new Row("piece_scout_buggy", "Scout Buggy", "ScoutBuggy", "SheltreSim.World.RideableScoutBuggy", 3.8f, 2.1f, 4.6f, new Color(.36f, .38f, .30f), 420f),
            new Row("piece_armored_van", "Armored Van", "ArmoredVan", "SheltreSim.World.RideableArmoredVan", 5f, 2.5f, 6.5f, new Color(.28f, .32f, .30f), 1500f),
            new Row("piece_sandbag", "Sandbag Wall", "SandbagWall", null, 2f, 1.1f, .6f, Sand, 260f),
            new Row("piece_spike_trap", "Spike Trap", "SpikeTrap", "SheltreSim.Raids.SpikeTrapPiece", 2f, .25f, 2f, Danger, 300f, true),
            new Row("piece_shock_trap", "Shock Trap", "ShockTrap", "SheltreSim.Raids.ShockTrapPiece", 2f, .2f, 2f, new Color(.7f, .7f, .2f), 200f, true),
            new Row("piece_turret_mg", "Machine Gun Turret", "TurretMG", "SheltreSim.Raids.MachineGunTurretPiece", 1f, 1.5f, 1f, Danger, 220f),
            new Row("piece_turret_laser", "Laser Turret", "TurretLaser", "SheltreSim.Raids.LaserTurretPiece", 1f, 1.6f, 1f, new Color(.3f, .5f, .8f), 180f),
            new Row("piece_alarm", "Raid Alarm", "RaidAlarm", "SheltreSim.Raids.AlarmPiece", .5f, 1.8f, .5f, Danger, 60f),
            new Row("piece_light_ceiling", "Ceiling Light", "CeilingLight", "SheltreSim.Power.LightPiece", .4f, .5f, .4f, new Color(1f, .82f, .46f), 40f),
            new Row("piece_floodlight", "Floodlight", "Floodlight", "SheltreSim.Power.LightPiece", .5f, .5f, .4f, new Color(.7f, .82f, 1f), 60f),
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
            M16ArtPass.ApplyBicycleArt();
            UpgradeOlderPieces();
            BuildWeaponPrefabs(atlas);
            Directory.CreateDirectory(PropPrefabs.TrimEnd('/'));
            foreach (string id in PropIds) ModelPrefab(WeaponModels + id + ".fbx", PropPrefabs + id + ".prefab", id, atlas);
            ModelPrefab(WeaponModels + "bunker_lamp.fbx", PropPrefabs + "bunker_lamp.prefab", "bunker_lamp", atlas);
            // Furniture, building parts, landmark dressing, and item visuals get Resources prefabs by id.
            foreach (string fbx in Directory.GetFiles(WeaponModels.TrimEnd('/'), "*.fbx"))
            {
                string id = Path.GetFileNameWithoutExtension(fbx);
                if ((id.StartsWith("furn_") || id.StartsWith("bld_") || id.StartsWith("lmk_") || id.StartsWith("item_")) && !PropIds.Contains(id))
                    ModelPrefab(WeaponModels + id + ".fbx", PropPrefabs + id + ".prefab", id, atlas);
            }
            Directory.CreateDirectory(EnemyPrefabs.TrimEnd('/'));
            foreach (var species in SheltreSim.Bestiary.SpeciesSpec.All)
            {
                ModelPrefab(WeaponModels + species.ModelId + ".fbx", EnemyPrefabs + species.ModelId + ".prefab", species.ModelId, atlas);
                AddAnimator(WeaponModels + species.ModelId + ".fbx", EnemyPrefabs + species.ModelId);
            }
            BuildDamageStages(atlas);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WireScene(added);
            Debug.Log($"[M22] systems setup complete: {added.Count} new pieces.");
        }

        /// <summary>
        /// Incremental setup for M27-M28 additions. It only creates these new prefabs/definitions and appends them to the
        /// scene's build catalogue, preserving existing prefab and art work from other owners.
        /// </summary>
        [MenuItem("Tools/SheltreSim/Setup M27-M28 Backlog Content")]
        public static void SetupBacklogContent()
        {
            const string scenePath = "Assets/Scenes/M1_BunkerShell.unity";
            EditorSceneManager.OpenScene(scenePath);
            Material atlas = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/SS_Atlas.mat");
            Directory.CreateDirectory(PropPrefabs.TrimEnd('/'));
            ModelPrefab(WeaponModels + "bunker_lamp.fbx", PropPrefabs + "bunker_lamp.prefab", "bunker_lamp", atlas);
            var backlogIds = new HashSet<string>
            {
                "piece_foundation_brick", "piece_wall_brick",
                "piece_foundation_reinforced", "piece_wall_reinforced",
                "piece_foundation_concrete", "piece_wall_concrete",
                "piece_scout_buggy", "piece_armored_van", "piece_garage_bay",
            };
            var added = new List<BuildPieceDefinition>();
            foreach (Row row in Rows)
            {
                if (!backlogIds.Contains(row.id)) continue;
                string definitionPath = PiecesPath + row.asset + ".asset";
                BuildPieceDefinition definition = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(definitionPath);
                if (definition == null || definition.prefab == null)
                {
                    definition = BuildPiece(row, atlas);
                }
                else
                {
                    // Preserve an already-built prefab (including future visual-owner edits) on repeated setup runs.
                    definition.id = row.id;
                    definition.displayName = row.name;
                    definition.footprintSize = row.size;
                    definition.placementRule = HomesteadCatalog.RuleFor(row.id);
                    definition.craftingCost = M21CraftingSetup.CostFor(row.id);
                    EditorUtility.SetDirty(definition);
                }
                if (definition != null) added.Add(definition);
            }

            // The stage assets remain art-owned. New walls only read the existing damage stages at runtime;
            // if those resources have not been provided yet, the visual component falls back gracefully.
            foreach (var entry in DamageStagePieces)
            {
                if (!backlogIds.Contains(entry.pieceId)) continue;
                BuildPieceDefinition definition = added.Find(candidate => candidate.id == entry.pieceId);
                if (definition == null || definition.prefab == null) continue;
                string prefabPath = AssetDatabase.GetAssetPath(definition.prefab);
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    if (root.GetComponent<StructureHealth>() == null) continue;
                    StructureDamageVisual visual = root.GetComponent<StructureDamageVisual>();
                    if (visual == null) visual = root.AddComponent<StructureDamageVisual>();
                    visual.modelId = entry.modelId;
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            WireScene(added);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[M22] Incremental backlog setup wired {added.Count} M27-M28 build pieces without rebuilding existing prefabs.");
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
                        if (component is SheltreSim.Power.LightPiece light)
                            light.fixture = row.id == "piece_floodlight" ? SheltreSim.Power.LightFixture.Floodlight : SheltreSim.Power.LightFixture.Ceiling;
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
            def.placementRule = HomesteadCatalog.RuleFor(row.id);
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
                    StructureHealth health = root.GetComponent<StructureHealth>();
                    if (health == null) health = root.AddComponent<StructureHealth>(); // not ??: Unity objects fake null
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

        /// <summary>
        /// Worn and broken looks for walls, doors and outdoor defenses: a Resources prefab per stage model, and StructureDamageVisual on each piece
        /// prefab that has both, next to its StructureHealth. Runs after M22HomesteadSetup, which rebuilds the door prefabs.
        /// </summary>
        private static void BuildDamageStages(Material atlas)
        {
            Directory.CreateDirectory(DamageStagePrefabs.TrimEnd('/'));
            var defs = new Dictionary<string, BuildPieceDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:BuildPieceDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null && !string.IsNullOrEmpty(def.id)) defs[def.id] = def;
            }
            foreach (var (pieceId, modelId) in DamageStagePieces)
            {
                bool complete = true;
                foreach (string suffix in new[] { "_damaged", "_critical" })
                {
                    string id = modelId + suffix;
                    ModelPrefab(WeaponModels + id + ".fbx", DamageStagePrefabs + id + ".prefab", id, atlas);
                    complete &= File.Exists(DamageStagePrefabs + id + ".prefab");
                }
                if (!defs.TryGetValue(pieceId, out BuildPieceDefinition piece) || piece.prefab == null)
                {
                    Debug.LogWarning($"[M22] no piece {pieceId} for damage stages");
                    continue;
                }
                string path = AssetDatabase.GetAssetPath(piece.prefab);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    StructureDamageVisual visual = root.GetComponent<StructureDamageVisual>();
                    if (!complete || root.GetComponent<StructureHealth>() == null)
                    {
                        if (visual != null) { UnityEngine.Object.DestroyImmediate(visual); PrefabUtility.SaveAsPrefabAsset(root, path); }
                        Debug.LogWarning($"[M22] {pieceId}: damage stages skipped (models {(complete ? "present" : "missing")}, health {(root.GetComponent<StructureHealth>() != null)})");
                        continue;
                    }
                    if (visual == null) visual = root.AddComponent<StructureDamageVisual>();
                    visual.modelId = modelId;
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
                ModelPrefab(WeaponModels + "weapon_" + spec.id + ".fbx", WeaponPrefabs + spec.id + ".prefab", spec.id, atlas);
            }
        }

        // One controller per model from its own takes: Idle and Walk loop, Attack/Hit/Death once. Driven by EnemyAnimationDriver.
        private static void AddAnimator(string modelPath, string prefabBase)
        {
            string prefabPath = prefabBase + ".prefab";
            if (!File.Exists(prefabPath)) return;
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            AnimationClip Clip(string name) => clips.FirstOrDefault(c => c.name == name) ?? clips.FirstOrDefault(c => c.name.EndsWith(name));
            AnimationClip idle = Clip("Idle"), walk = Clip("Walk"), attack = Clip("Attack"), hit = Clip("Hit"), death = Clip("Death");
            if (idle == null || walk == null || attack == null)
            {
                Debug.LogWarning($"[M22] {modelPath}: no Idle/Walk/Attack takes ({clips.Count} clips), it keeps the procedural walk");
                return;
            }
            string controllerPath = prefabBase + "_anim.controller";
            AssetDatabase.DeleteAsset(controllerPath);
            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Trigger);
            var sm = controller.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sWalk = sm.AddState("Walk"); sWalk.motion = walk;
            var sAttack = sm.AddState("Attack"); sAttack.motion = attack;
            sm.defaultState = sIdle;
            var t = sIdle.AddTransition(sWalk); t.hasExitTime = false; t.duration = .15f; t.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Greater, .3f, "Speed");
            t = sWalk.AddTransition(sIdle); t.hasExitTime = false; t.duration = .2f; t.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Less, .2f, "Speed");
            t = sm.AddAnyStateTransition(sAttack); t.hasExitTime = false; t.duration = .05f; t.canTransitionToSelf = false; t.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Attack");
            t = sAttack.AddTransition(sIdle); t.hasExitTime = true; t.exitTime = 1f; t.duration = .1f;
            if (hit != null)
            {
                var sHit = sm.AddState("Hit"); sHit.motion = hit;
                t = sm.AddAnyStateTransition(sHit); t.hasExitTime = false; t.duration = .05f; t.canTransitionToSelf = false; t.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Hit");
                t = sHit.AddTransition(sIdle); t.hasExitTime = true; t.exitTime = 1f; t.duration = .1f;
            }
            if (death != null)
            {
                var sDeath = sm.AddState("Death"); sDeath.motion = death;
                t = sm.AddAnyStateTransition(sDeath); t.hasExitTime = false; t.duration = .05f; t.canTransitionToSelf = false; t.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Dead");
            }
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Animator animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>(); // not ??: Unity objects fake null
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // cull modes skip enemies the camera isn't rendering, a probe or a turret still needs them posed
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ModelPrefab(string modelPath, string path, string name, Material atlas)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogWarning("[M22] no model at " + modelPath + ", the game keeps its fallback");
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                return;
            }
            GameObject instance = UnityEngine.Object.Instantiate(model);
            try
            {
                instance.name = name;
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true)) if (atlas != null) r.sharedMaterial = atlas;
                foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
                PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
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
