using System.Collections.Generic;
using System.Linq;
using SheltreSim.Building;
using SheltreSim.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// M22: the structural kit. Prefabs with real openings and sloped colliders, definitions from HomesteadCatalog, and placement
    /// rules on existing pieces. Idempotent. Run after M21. Damage-stage looks (StructureDamageVisual) are added by M22SystemsSetup, which runs next. Colliders follow ArtSource/assets/_m22_build.py (window opening 1.08 x 1.2, sill at 1 m).
    /// </summary>
    public static class M22HomesteadSetup
    {
        private const string PiecesPath = "Assets/Data/BuildPieces/";
        private const string ModelsPath = "Assets/Art/Models/";
        private static readonly Color Wood = new Color(.45f, .33f, .2f), Steel = new Color(.5f, .52f, .56f);

        [MenuItem("Tools/SheltreSim/Setup M22 Homestead")]
        public static void Setup()
        {
            Material atlas = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/SS_Atlas.mat");
            Dictionary<string, BuildPieceDefinition> defs = new Dictionary<string, BuildPieceDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:BuildPieceDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<BuildPieceDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null && !string.IsNullOrEmpty(def.id)) defs[def.id] = def;
            }

            var added = new List<BuildPieceDefinition>();
            foreach (HomesteadEntry entry in HomesteadCatalog.Pieces)
            {
                // Older pieces keep their prefabs and only gain a rule and hit points.
                if (entry.id == "wall_segment" || entry.id == "floor_tile" || entry.id == "piece_wall_steel")
                {
                    if (defs.TryGetValue(entry.id, out BuildPieceDefinition older)) { AddHealth(older, entry.hitPoints); added.Add(older); }
                    continue;
                }
                added.Add(BuildPiece(entry, atlas, defs));
            }
            foreach (BuildPieceDefinition def in defs.Values.Concat(added).Distinct())
            {
                def.placementRule = HomesteadCatalog.RuleFor(def.id);
                EditorUtility.SetDirty(def);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WireScene(added);
            Debug.Log($"[M22] homestead setup complete: {added.Count} structural pieces.");
        }

        private static BuildPieceDefinition BuildPiece(HomesteadEntry entry, Material atlas, Dictionary<string, BuildPieceDefinition> defs)
        {
            string asset = string.Concat(entry.id.Replace("piece_", "").Split('_').Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
            Color color = entry.material == HomesteadMaterial.Metal ? Steel : Wood;
            bool hasModel = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsPath + entry.id + ".fbx") != null;
            var spec = new M21CraftingSetup.PieceSpec
            {
                id = entry.id, name = entry.displayName, prefabName = asset + "Piece", assetName = asset, size = entry.size, color = color,
                behaviours = root =>
                {
                    Shape(root, entry, hasModel, color);
                    root.AddComponent<StructureHealth>().maxHealth = entry.hitPoints;
                }
            };
            GameObject prefab = M21CraftingSetup.BuildPrefab(spec, atlas);
            if (!defs.TryGetValue(entry.id, out BuildPieceDefinition def))
            {
                def = ScriptableObject.CreateInstance<BuildPieceDefinition>();
                AssetDatabase.CreateAsset(def, PiecesPath + asset + ".asset");
                defs[entry.id] = def;
            }
            def.id = entry.id; def.displayName = entry.displayName; def.prefab = prefab; def.footprintSize = entry.size;
            def.craftingCost = M21CraftingSetup.CostFor(entry.id);
            def.placementRule = entry.rule;
            EditorUtility.SetDirty(def);
            return def;
        }

        private static void Shape(GameObject root, HomesteadEntry entry, bool hasModel, Color color)
        {
            Vector3 s = entry.size;
            float bottom = -s.y / 2f;
            switch (entry.id)
            {
                case "piece_door_frame":
                    Boxes(root, hasModel, color,
                        (new Vector3(-.9f, 0f, 0f), new Vector3(.2f, 3f, s.z)),
                        (new Vector3(.9f, 0f, 0f), new Vector3(.2f, 3f, s.z)),
                        (new Vector3(0f, bottom + 2.6f, 0f), new Vector3(1.6f, .8f, s.z)));
                    break;
                case "piece_wall_window":
                    Boxes(root, hasModel, color,
                        (new Vector3(0f, bottom + .5f, 0f), new Vector3(2f, 1f, s.z)),
                        (new Vector3(0f, bottom + 2.6f, 0f), new Vector3(2f, .8f, s.z)),
                        (new Vector3(-.77f, bottom + 1.6f, 0f), new Vector3(.46f, 1.2f, s.z)),
                        (new Vector3(.77f, bottom + 1.6f, 0f), new Vector3(.46f, 1.2f, s.z)));
                    break;
                case "piece_door_wood": case "piece_door_steel": case "piece_gate":
                    Hinged(root, entry, hasModel, color);
                    break;
                case "piece_ramp": Slope(root, hasModel, color, 2f, 2f, .4f, 0f, .1f); break;
                case "piece_stairs": Slope(root, hasModel, color, 2f, 4f, 3f, 0f, .2f); break;
                case "piece_roof_slope": Slope(root, hasModel, color, 2f, 2f, 1.02f, .09f, .18f); break;
                case "piece_foundation":
                    // Legs reach the ground on the low side of a slope (1 m limit) and vanish into it elsewhere.
                    for (int x = -1; x <= 1; x += 2)
                        for (int z = -1; z <= 1; z += 2)
                            Cube(root.transform, "Leg", new Vector3(x * .88f, bottom - .6f, z * .88f), new Vector3(.14f, 1.2f, .14f), new Color(.3f, .3f, .3f));
                    break;
            }
        }

        private static void Boxes(GameObject root, bool hasModel, Color color, params (Vector3 centre, Vector3 size)[] boxes)
        {
            Object.DestroyImmediate(root.GetComponent<BoxCollider>());
            foreach (var box in boxes)
            {
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = box.centre;
                collider.size = box.size;
                if (!hasModel) Cube(root.transform, "Part", box.centre, box.size, color);
            }
            if (!hasModel) Object.DestroyImmediate(root.transform.Find("Visual").gameObject);
        }

        private static void Hinged(GameObject root, HomesteadEntry entry, bool hasModel, Color color)
        {
            Object.DestroyImmediate(root.GetComponent<BoxCollider>());
            GameObject hinge = new GameObject("Hinge");
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition = new Vector3(-entry.size.x / 2f, 0f, 0f);
            BoxCollider leaf = hinge.AddComponent<BoxCollider>();
            leaf.center = new Vector3(entry.size.x / 2f, 0f, 0f);
            leaf.size = entry.size;
            root.transform.Find("Visual").SetParent(hinge.transform, true);
            HomesteadDoorPiece door = root.AddComponent<HomesteadDoorPiece>();
            door.hinge = hinge.transform;
            door.closedSize = entry.size;
            door.noun = entry.id == "piece_gate" ? "gate" : "door";
        }

        /// <summary>One box whose top face is the walking surface, rising towards +Z. Stairs are 36.9 degrees, under the CharacterController's 45.</summary>
        private static void Slope(GameObject root, bool hasModel, Color color, float width, float length, float rise, float centreY, float thickness)
        {
            Object.DestroyImmediate(root.GetComponent<BoxCollider>());
            float angle = Mathf.Atan2(rise, length) * Mathf.Rad2Deg;
            Quaternion tilt = Quaternion.Euler(-angle, 0f, 0f);
            GameObject slope = new GameObject("Slope");
            slope.transform.SetParent(root.transform, false);
            slope.transform.localRotation = tilt;
            slope.transform.localPosition = new Vector3(0f, centreY, 0f) + tilt * new Vector3(0f, -thickness / 2f, 0f);
            BoxCollider box = slope.AddComponent<BoxCollider>();
            box.size = new Vector3(width, thickness, Mathf.Sqrt(length * length + rise * rise));
            if (!hasModel)
            {
                Object.DestroyImmediate(root.transform.Find("Visual").gameObject);
                Cube(slope.transform, "Part", Vector3.zero, box.size, color);
            }
        }

        private static void Cube(Transform parent, string name, Vector3 position, Vector3 size, Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = size;
            EditorMaterialUtility.Apply(cube, color);
        }

        private static void AddHealth(BuildPieceDefinition def, float hp)
        {
            if (def.prefab == null) return;
            string path = AssetDatabase.GetAssetPath(def.prefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                StructureHealth health = root.GetComponent<StructureHealth>();
                if (health == null) health = root.AddComponent<StructureHealth>(); // not ??: Unity objects fake null
                health.maxHealth = hp;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void WireScene(List<BuildPieceDefinition> added)
        {
            var build = Object.FindFirstObjectByType<BuildModeController>();
            if (build == null) { Debug.LogWarning("[M22] no built scene: homestead assets written, wiring skipped."); return; }
            foreach (BuildPieceDefinition def in added) if (!build.availablePieces.Contains(def)) build.availablePieces.Add(def);
            EditorUtility.SetDirty(build);
            InventoryUI inventoryUi = Object.FindFirstObjectByType<InventoryUI>();
            if (inventoryUi != null)
            {
                foreach (BuildPieceDefinition def in added) if (!inventoryUi.knownRecipes.Contains(def)) inventoryUi.knownRecipes.Add(def);
                EditorUtility.SetDirty(inventoryUi);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
    }
}
