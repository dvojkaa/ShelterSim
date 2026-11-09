using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SheltreSim.Core;
using SheltreSim.Enemies;
using SheltreSim.Interaction;
using SheltreSim.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// M16: swaps the grey-box look for the Blender models in Assets/Art/Models (sources in
    /// ArtSource/). Runs LAST in BatchSetupAll, deliberately not inside the milestone builders:
    /// M10/M12 draw placement from one seeded RNG stream and M12 reads renderer bounds, so any
    /// visual change inside them would silently move the whole map. Running afterwards means
    /// every position, collider, component, and stable save ID is already final.
    ///
    /// Contract, enforced at the end of the pass by comparing collider snapshots: this tool only
    /// touches visuals. Renderers are removed, children with neither collider nor behaviour are
    /// removed, and new collider-free "Visual" children are added. A missing FBX logs a warning
    /// and leaves that object's primitive in place, so the game never loses an object.
    /// Variant picks use a stable name hash (string.GetHashCode is not stable across runtimes)
    /// and never advance anyone else's RNG.
    /// </summary>
    public static class M16ArtPass
    {
        private const string ModelsPath = "Assets/Art/Models/";
        private const string TexturesPath = "Assets/Art/Textures/";
        private const string MaterialsPath = "Assets/Art/Materials/";
        private const string VisualName = "Visual";
        private const string DressingName = "ArtDressing";

        private static Material atlasMaterial;
        private static Material raiderMaterial;
        private static int swapped;
        private static readonly HashSet<string> missingModels = new HashSet<string>();

        [MenuItem("Tools/SheltreSim/Apply M16 Art Pass")]
        public static void ApplyArt()
        {
            swapped = 0;
            missingModels.Clear();
            AssetDatabase.Refresh();
            EnsureMaterials();

            ApplyPrefabs();

            Dictionary<string, string> before = SnapshotColliders();

            ApplyAtmosphere();
            Transform dressing = RebuildDressingRoot();
            ApplyBunker(dressing);
            ApplyResourceNodes();
            ApplyOutdoorSurfaces(dressing);
            ApplyBuildings();
            ApplyCars();
            ApplyCrates();
            ApplyRaiders();
            ApplyPlayerWeapon();
            ApplyProceduralNature();

            VerifyCollidersUnchanged(before, SnapshotColliders());

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            string missing = missingModels.Count == 0 ? "none" : string.Join(", ", missingModels.OrderBy(m => m));
            Debug.Log($"[M16ArtPass] Done: {swapped} objects now use Blender models. Missing models (kept their primitive): {missing}.");
        }

        // ---------- Materials ----------

        private static void EnsureMaterials()
        {
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesPath + "SS_Atlas.png");
            atlasMaterial = StandardMaterial("SS_Atlas", atlas, false);
            // Separate material only for the damage flash: EnemyAI's flash used to whiten a dark
            // flat colour through _Color, which does nothing on a textured white-tinted material.
            raiderMaterial = StandardMaterial("SS_Raider", atlas, true);
        }

        private static Material StandardMaterial(string name, Texture2D texture, bool flashEmission)
        {
            string path = MaterialsPath + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("Standard");
            material.mainTexture = texture;
            material.color = Color.white;
            material.SetFloat("_Glossiness", 0.1f);
            material.SetFloat("_Metallic", 0f);
            // Sky reflections wash the muted palette towards blue-grey, most visibly on greens.
            material.SetFloat("_GlossyReflections", 0f);
            material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            if (flashEmission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material SurfaceMaterial(string name, float tileMeters, float macro = 0f, float depthBias = 0f)
        {
            string path = MaterialsPath + name + (depthBias != 0f ? "_Biased" : "") + ".mat";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesPath + name + ".png");
            if (texture == null)
            {
                missingModels.Add(name + ".png");
                return null;
            }
            Shader shader = Shader.Find("SheltreSim/WorldTriplanar");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_TileMeters", tileMeters);
            material.SetFloat("_Macro", macro);
            material.SetFloat("_DepthBias", depthBias);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetSurface(GameObject target, Material material)
        {
            if (target == null || material == null) return;
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        /// <summary>
        /// Dusty wasteland air in place of Unity's default blue sky. Trilight ambient needs no
        /// lighting bake, which batch builds don't run. Render settings only: no gameplay reads these.
        /// </summary>
        private static void ApplyAtmosphere()
        {
            string path = MaterialsPath + "SS_Sky.mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, path);
            }
            sky.SetFloat("_SunDisk", 1f);
            sky.SetFloat("_SunSize", 0.05f);
            sky.SetFloat("_AtmosphereThickness", 1.9f);
            sky.SetColor("_SkyTint", new Color(0.62f, 0.55f, 0.47f));
            sky.SetColor("_GroundColor", new Color(0.36f, 0.31f, 0.25f));
            sky.SetFloat("_Exposure", 1.05f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.58f, 0.54f);
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.45f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.26f, 0.21f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.68f, 0.60f, 0.49f);
            RenderSettings.fogStartDistance = 50f;
            RenderSettings.fogEndDistance = 320f;

            foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type != LightType.Directional) continue;
                light.color = new Color(1f, 0.92f, 0.78f);
                light.intensity = 1.15f;
                light.shadows = LightShadows.Soft;
            }
        }

        // ---------- Core swap helpers ----------

        private static GameObject LoadModel(string id)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsPath + id + ".fbx");
            if (model == null) missingModels.Add(id);
            return model;
        }

        /// <summary>Removes every renderer under root. Children left without collider or behaviour go too; crates parked under cars/buildings are never touched.</summary>
        private static void StripVisuals(GameObject root, params string[] keepChildren)
        {
            RemoveRenderer(root);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = root.transform.GetChild(i);
                if (child.name == VisualName || keepChildren.Contains(child.name) || child.GetComponentInChildren<LootContainer>(true) != null)
                {
                    if (keepChildren.Contains(child.name)) RemoveRenderer(child.gameObject);
                    continue;
                }
                StripVisuals(child.gameObject, keepChildren);
                bool hasSubstance = child.childCount > 0 || child.GetComponents<Component>().Any(c => !(c is Transform));
                if (!hasSubstance) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void RemoveRenderer(GameObject target)
        {
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
            MeshFilter filter = target.GetComponent<MeshFilter>();
            if (filter != null) UnityEngine.Object.DestroyImmediate(filter);
        }

        private static GameObject Spawn(GameObject model, Material material)
        {
            GameObject instance = UnityEngine.Object.Instantiate(model);
            instance.name = VisualName;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;
            return instance;
        }

        /// <summary>
        /// Replaces target's look with a model while leaving target's own transform and colliders
        /// untouched. Roots keep their old non-uniform scale because BoxColliders rely on it, so
        /// the child takes the inverse: the model comes out at true metres. worldOffset is in
        /// metres along target's own axes.
        /// </summary>
        private static GameObject Swap(GameObject target, string modelId, Vector3 worldOffset, float yawDegrees = 0f, Material material = null, params string[] keepChildren)
        {
            if (target.transform.Find(VisualName) != null) return null;
            GameObject model = LoadModel(modelId);
            if (model == null) return null;

            StripVisuals(target, keepChildren);
            GameObject visual = Spawn(model, material != null ? material : atlasMaterial);
            visual.transform.SetParent(target.transform, false);
            Vector3 lossy = target.transform.lossyScale;
            visual.transform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);
            visual.transform.localPosition = new Vector3(worldOffset.x / lossy.x, worldOffset.y / lossy.y, worldOffset.z / lossy.z);
            visual.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            swapped++;
            return visual;
        }

        private static GameObject Place(string modelId, Transform parent, Vector3 localPosition, float yawDegrees = 0f, string name = null)
        {
            GameObject model = LoadModel(modelId);
            if (model == null) return null;
            GameObject visual = Spawn(model, atlasMaterial);
            visual.name = name ?? "Dress_" + modelId;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            Vector3 lossy = parent.lossyScale;
            visual.transform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);
            swapped++;
            return visual;
        }

        /// <summary>FNV-1a. Stable across runs and runtimes, unlike string.GetHashCode.</summary>
        private static uint StableHash(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }

        private static string Variant(string name, params string[] ids) => ids[StableHash(name) % (uint)ids.Length];
        private static float StableYaw(string name) => (StableHash(name + "#yaw") % 360u);

        // ---------- Build piece prefabs ----------

        private static void ApplyPrefabs()
        {
            SwapPrefab("WallSegmentPiece", "piece_wall", 3f);
            SwapPrefab("FloorTilePiece", "piece_floor", 0.2f);
            SwapPrefab("WorkbenchPiece", "piece_workbench", 1f);
            SwapPrefab("BedPiece", "piece_bed", 0.5f);
            SwapPrefab("StorageCratePiece", "piece_crate", 1f);
            SwapPrefab("WaterCollectorPiece", "piece_water_collector", 1.2f);
            SwapPrefab("GeneratorPiece", "piece_generator", 1.2f);
        }

        /// <summary>M22 creates the bicycle prefab later than the regular M16 pass in BatchSetupAll.</summary>
        internal static void ApplyBicycleArt()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Building/ScrapBicyclePiece.prefab") != null)
                SwapPrefab("ScrapBicyclePiece", "piece_bicycle", 1.3f);
        }

        /// <summary>Prefab root is the footprint's centre, and models sit on their floor, so the offset is half the footprint height.</summary>
        private static void SwapPrefab(string prefabName, string modelId, float footprintHeight)
        {
            string path = "Assets/Prefabs/Building/" + prefabName + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string before = DescribeColliders(root.transform, root.transform);
                if (Swap(root, modelId, new Vector3(0f, -footprintHeight / 2f, 0f)) == null) return;
                string after = DescribeColliders(root.transform, root.transform);
                if (before != after)
                    throw new InvalidOperationException($"[M16ArtPass] {prefabName}: colliders changed.\nBefore:\n{before}\nAfter:\n{after}");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string DescribeColliders(Transform root, Transform relativeTo)
        {
            StringBuilder text = new StringBuilder();
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                text.AppendLine(DescribeCollider(collider, relativeTo));
            return text.ToString();
        }

        private static string DescribeCollider(Collider collider, Transform relativeTo)
        {
            Transform t = collider.transform;
            string shape = collider switch
            {
                BoxCollider box => $"box {box.center:F3} {box.size:F3}",
                SphereCollider sphere => $"sphere {sphere.center:F3} {sphere.radius:F3}",
                CapsuleCollider capsule => $"capsule {capsule.center:F3} {capsule.radius:F3} {capsule.height:F3} {capsule.direction}",
                CharacterController controller => $"cc {controller.center:F3} {controller.radius:F3} {controller.height:F3}",
                MeshCollider mesh => $"mesh {(mesh.sharedMesh != null ? mesh.sharedMesh.name : "null")}",
                _ => collider.GetType().Name,
            };
            return $"{t.name} {shape} pos={relativeTo.InverseTransformPoint(t.position):F3} rot={t.rotation.eulerAngles:F2} scale={t.lossyScale:F3} enabled={collider.enabled}";
        }

        // ---------- Bunker ----------

        private static void ApplyBunker(Transform dressing)
        {
            Material concrete = SurfaceMaterial("SS_Concrete", 4f);
            Material floor = SurfaceMaterial("SS_ConcreteFloor", 4f);

            // FindRoot, not GameObject.Find: 12 ruined buildings also have a child called "Floor".
            SetSurface(FindRoot("Floor"), floor);
            foreach (string wall in new[] { "Wall_North", "Wall_East", "Wall_West", "Wall_South_A", "Wall_South_B" })
            {
                // Roots only: ruined buildings' same-named walls are nested and handled in ApplyBuildings.
                foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    if (root.name == wall) SetSurface(root, concrete);
            }
            GameObject roof = FindRoot("Roof");
            SetSurface(roof, concrete);

            GameObject door = GameObject.Find("Door");
            if (door != null && door.GetComponent<InteractableDoor>() != null)
            {
                // Door GameObject is centred 1.1 m up the leaf, on the hinge child.
                Swap(door, "door_leaf", new Vector3(0f, -1.1f, 0f));
            }

            if (LoadModel("door_frame") != null)
            {
                foreach (string trim in new[] { "DoorTrim_Lintel", "DoorTrim_PostLeft", "DoorTrim_PostRight" })
                {
                    // Emptied, not destroyed: removing a root shifts every later root's sibling
                    // index, and EnemySpawner's raider save IDs include sibling indices.
                    GameObject found = FindRoot(trim);
                    if (found != null) RemoveRenderer(found);
                }
                Place("door_frame", dressing, new Vector3(0f, 0f, -3f));
            }

            GameObject button = GameObject.Find("ApocalypseButton");
            if (button != null && Swap(button, "apocalypse_button", Vector3.zero) != null)
            {
                foreach (string part in new[] { "ApocalypseButton_Panel", "ApocalypseButton_HazardStripe" })
                {
                    // Emptied, not destroyed, for the same save-ID reason as the door trim.
                    GameObject found = FindRoot(part);
                    if (found != null) RemoveRenderer(found);
                }
            }

            // Purely additive decoration. North wall inner face z=2.9, west wall inner face x=-2.9.
            Place("bunker_pipes", dressing, new Vector3(0f, 0f, 2.9f));
            Place("bunker_shelf", dressing, new Vector3(-2.9f, 0f, 0.9f), -90f);
            GameObject lamp = Place("bunker_lamp", dressing, new Vector3(0f, 3f, 0f));
            if (lamp != null)
            {
                // The roof blocks the sun, so the room otherwise gets ambient light only.
                GameObject lightObject = new GameObject("BunkerLampLight");
                lightObject.transform.SetParent(dressing, false);
                lightObject.transform.localPosition = new Vector3(0f, 2.35f, 0f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.82f, 0.58f);
                light.range = 8f;
                light.intensity = 1.5f;
                light.shadows = LightShadows.Soft;
            }
        }

        private static GameObject FindRoot(string name)
        {
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        private static Transform RebuildDressingRoot()
        {
            GameObject previous = FindRoot(DressingName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
            return new GameObject(DressingName).transform;
        }

        // ---------- Resource nodes ----------

        private static void ApplyResourceNodes()
        {
            foreach (ResourceNode node in UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Include))
            {
                if (node.resourceType == null) continue;
                Transform t = node.transform;
                string id = node.resourceType.id;
                // Every node type rests on y=0 ground, whichever height its pivot happens to have.
                Vector3 toGround = new Vector3(0f, -t.position.y, 0f);
                float yaw = StableYaw(t.name);
                string model = id switch
                {
                    "wood" when t.Find("Trunk") != null => Variant(t.name, "tree_a", "tree_b", "tree_c"),
                    "wood" => Variant(t.name, "dead_log_a", "dead_log_b"),
                    "scrap_metal" => Variant(t.name, "scrap_pile_a", "scrap_pile_b"),
                    "stone" => Variant(t.name, "rock_stone_a", "rock_stone_b"),
                    "iron_ore" => Variant(t.name, "ore_iron_a", "ore_iron_b"),
                    "food_berries" => Variant(t.name, "food_bush_a", "food_bush_b"),
                    "plant_fiber" => "node_tall_grass",
                    _ => null,
                };
                if (model != null) Swap(t.gameObject, model, toGround, yaw);
            }
        }

        /// <summary>Assigns harvested nature visuals to the streamed-world generator after gameplay setup.</summary>
        private static void ApplyProceduralNature()
        {
            SheltreSim.World.ProceduralWorld world = UnityEngine.Object.FindFirstObjectByType<SheltreSim.World.ProceduralWorld>(FindObjectsInactive.Include);
            if (world == null || world.assets == null) return;
            world.assets.tallGrass = LoadModel("node_tall_grass");
            world.assets.reeds = LoadModel("node_reeds");
            world.assets.plantFiber = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/PlantFiber.asset");
            EditorUtility.SetDirty(world);
        }

        // ---------- Ground, perimeter, rubble ----------

        private static void ApplyOutdoorSurfaces(Transform dressing)
        {
            GameObject outdoor = GameObject.Find("OutdoorArea");
            if (outdoor == null) return;

            Material ground = SurfaceMaterial("SS_Ground", 11f, 1f);
            Material fence = SurfaceMaterial("SS_CorrugatedRust", 4f);

            foreach (Transform t in outdoor.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "OutdoorGround" || t.name == "ExpandedGround") SetSurface(t.gameObject, ground);
                else if (t.name.StartsWith("Boundary_"))
                {
                    SetSurface(t.gameObject, fence);
                    DressBoundary(t, dressing);
                }
                else if (t.parent == outdoor.transform && t.name.StartsWith("Rubble_")) SwapRubble(t.gameObject);
            }
        }

        private static void DressBoundary(Transform wall, Transform dressing)
        {
            const float spacing = 7f;
            Vector3 scale = wall.lossyScale;
            bool alongZ = scale.z > scale.x;
            float length = alongZ ? scale.z : scale.x;
            for (float d = -length / 2f + spacing / 2f; d <= length / 2f; d += spacing)
            {
                Vector3 position = wall.position + (alongZ ? new Vector3(0f, 0f, d) : new Vector3(d, 0f, 0f));
                position.y = 0f;
                Place("boundary_post", dressing, position, alongZ ? 90f : 0f);
            }
        }

        /// <summary>The model is normalised 1 x 1 x 1 and stretched to the same footprint as the old primitive, resting on the ground up to the primitive's old top.</summary>
        private static void SwapRubble(GameObject rubble)
        {
            Transform t = rubble.transform;
            bool cylinder = rubble.GetComponent<MeshFilter>() != null && rubble.GetComponent<MeshFilter>().sharedMesh != null
                && rubble.GetComponent<MeshFilter>().sharedMesh.name.StartsWith("Cylinder");
            float primitiveHeight = cylinder ? t.localScale.y * 2f : t.localScale.y;
            float top = Mathf.Max(0.2f, t.position.y + primitiveHeight / 2f);
            string id = cylinder ? "rubble_slab" : Variant(t.name, "rubble_a", "rubble_b", "rubble_c");
            GameObject visual = Swap(rubble, id, new Vector3(0f, -t.position.y, 0f));
            if (visual == null) return;
            visual.transform.localScale = new Vector3(1f, top / t.localScale.y, 1f);
        }

        // ---------- Ruined buildings ----------

        private static void ApplyBuildings()
        {
            GameObject outdoor = GameObject.Find("OutdoorArea");
            Transform buildings = outdoor != null ? outdoor.transform.Find("ExpandedWasteland/Buildings") : null;
            if (buildings == null) return;

            Material brick = SurfaceMaterial("SS_Brick", 3f);
            Material plaster = SurfaceMaterial("SS_Plaster", 4f);
            // Ruin floors' top faces sit at y=0, exactly on the ground's top face, so they would z-fight.
            Material floor = SurfaceMaterial("SS_ConcreteFloor", 4f, 0f, -2f);
            Material roof = SurfaceMaterial("SS_RoofTar", 4f);

            foreach (Transform building in buildings)
            {
                Material walls = (StableHash(building.name) & 1u) == 0u ? brick : plaster;
                foreach (string wall in new[] { "Wall_North", "Wall_East", "Wall_West", "Wall_South_A", "Wall_South_B" })
                {
                    Transform found = building.Find(wall);
                    if (found != null) SetSurface(found.gameObject, walls);
                }
                SetSurface(building.Find("Floor")?.gameObject, floor);
                SetSurface(building.Find("Roof")?.gameObject, roof);
                DressBuilding(building);
            }
        }

        private static void DressBuilding(Transform building)
        {
            Transform north = building.Find("Wall_North");
            Transform east = building.Find("Wall_East");
            if (north == null || east == null) return;
            // Already dressed by an earlier menu run: nothing below removes its own output.
            foreach (Transform child in building)
                if (child.name.StartsWith("Dress_")) return;

            float width = north.localScale.x;
            float depth = east.localScale.z;
            float halfW = width / 2f;
            float halfD = depth / 2f;
            const float wallHalf = 0.1f;
            System.Random rnd = new System.Random((int)(StableHash(building.name) & 0x7fffffff));

            // Old loose rubble becomes debris piles at the same outside-the-footprint spots.
            for (int i = building.childCount - 1; i >= 0; i--)
            {
                Transform child = building.GetChild(i);
                bool loose = child.name.StartsWith("Rubble_");
                if (!loose && child.name != "BrokenCorner") continue;
                Vector3 at = child.localPosition;
                string name = child.name;
                UnityEngine.Object.DestroyImmediate(child.gameObject);
                if (loose)
                {
                    at.y = 0f;
                    Place(rnd.NextDouble() < 0.5 ? "ruin_debris_a" : "ruin_debris_b", building, at, (float)(rnd.NextDouble() * 360.0), "Dress_" + name);
                }
            }

            Place("ruin_doorway", building, new Vector3(0f, 0f, -halfD));

            // Boarded windows on the outside faces. Models protrude along -Y (Unity -Z), so yaw points that outward.
            PlaceWindows(building, new Vector3(0f, 0f, halfD + wallHalf), Vector3.right, width, 180f, rnd);
            PlaceWindows(building, new Vector3(halfW + wallHalf, 0f, 0f), Vector3.forward, depth, -90f, rnd);
            PlaceWindows(building, new Vector3(-halfW - wallHalf, 0f, 0f), Vector3.forward, depth, 90f, rnd);

            // Broken wall tops along the three unbroken sides, with gaps so no two ruins match.
            PlaceParapets(building, new Vector3(0f, 3f, halfD), Vector3.right, width, 0f, rnd);
            PlaceParapets(building, new Vector3(halfW, 3f, 0f), Vector3.forward, depth, 90f, rnd);
            PlaceParapets(building, new Vector3(-halfW, 3f, 0f), Vector3.forward, depth, 90f, rnd);
        }

        private static void PlaceWindows(Transform building, Vector3 wallCentre, Vector3 along, float length, float yaw, System.Random rnd)
        {
            int count = Mathf.Max(1, Mathf.FloorToInt(length / 3f));
            for (int i = 0; i < count; i++)
            {
                float d = (i + 0.5f) / count * length - length / 2f;
                if (rnd.NextDouble() < 0.25) continue;
                Place("ruin_window", building, wallCentre + along * d + Vector3.up * 1.6f, yaw);
            }
        }

        private static void PlaceParapets(Transform building, Vector3 wallTop, Vector3 along, float length, float yaw, System.Random rnd)
        {
            const float pieceLength = 2f;
            int count = Mathf.FloorToInt(length / pieceLength);
            float start = -count * pieceLength / 2f + pieceLength / 2f;
            for (int i = 0; i < count; i++)
            {
                if (rnd.NextDouble() < 0.3) continue;
                string id = rnd.NextDouble() < 0.5 ? "ruin_parapet_a" : "ruin_parapet_b";
                float flip = rnd.NextDouble() < 0.5 ? 0f : 180f;
                Place(id, building, wallTop + along * (start + i * pieceLength), yaw + flip);
            }
        }

        // ---------- Cars ----------

        private static void ApplyCars()
        {
            GameObject outdoor = GameObject.Find("OutdoorArea");
            Transform cars = outdoor != null ? outdoor.transform.Find("ExpandedWasteland/Cars") : null;
            if (cars == null) return;

            foreach (Transform car in cars)
            {
                Transform body = car.Find("Body");
                MeshRenderer renderer = body != null ? body.GetComponent<MeshRenderer>() : null;
                string paint = renderer != null ? NearestPaint(renderer.sharedMaterial.color) : "grey";
                string shape = (StableHash(car.name) & 1u) == 0u ? "sedan" : "pickup";
                Swap(car.gameObject, $"car_{shape}_{paint}", Vector3.zero);
            }
        }

        /// <summary>Keeps each wreck the colour M10's palette gave it.</summary>
        private static string NearestPaint(Color color)
        {
            (string name, Color value)[] paints =
            {
                ("red", new Color(0.55f, 0.25f, 0.18f)),
                ("blue", new Color(0.25f, 0.35f, 0.45f)),
                ("green", new Color(0.3f, 0.38f, 0.28f)),
                ("grey", new Color(0.45f, 0.45f, 0.45f)),
            };
            return paints.OrderBy(p => Mathf.Abs(p.value.r - color.r) + Mathf.Abs(p.value.g - color.g) + Mathf.Abs(p.value.b - color.b)).First().name;
        }

        // ---------- Crates ----------

        private static void ApplyCrates()
        {
            foreach (LootContainer crate in UnityEngine.Object.FindObjectsByType<LootContainer>(FindObjectsInactive.Include))
            {
                Swap(crate.gameObject, "loot_crate", new Vector3(0f, -crate.transform.position.y, 0f));
            }
        }

        // ---------- Raiders ----------

        private static void ApplyRaiders()
        {
            GameObject weaponModel = LoadModel("raider_weapon");
            foreach (EnemyAI enemy in UnityEngine.Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include))
            {
                // Pivot is the centre of a height-2 CharacterController. In play the controller
                // rests skinWidth above the ground (pivot y 1.08, measured), so feet go that much lower.
                float skinWidth = enemy.GetComponent<CharacterController>().skinWidth;
                GameObject visual = Swap(enemy.gameObject, "raider", new Vector3(0f, -1f - skinWidth, 0f), 0f, raiderMaterial, "WeaponProp");
                if (visual == null) continue;

                if (enemy.GetComponent<RaiderWalkAnimator>() == null) enemy.gameObject.AddComponent<RaiderWalkAnimator>();

                // EnemyAI finds "WeaponProp" by name and only ever rotates it: its old stretched
                // scale was just how the primitive got its shape.
                Transform prop = enemy.transform.Find("WeaponProp");
                if (prop != null && weaponModel != null)
                {
                    prop.localScale = Vector3.one;
                    prop.localPosition += Vector3.down * skinWidth; // Stays in the model's hand.
                    GameObject weapon = Spawn(weaponModel, raiderMaterial);
                    weapon.transform.SetParent(prop, false);
                }
            }
        }

        // ---------- Player weapon ----------

        private static void ApplyPlayerWeapon()
        {
            GameObject player = GameObject.Find("Player");
            Transform weapon = player != null ? player.transform.Find("CameraPivot/WeaponVisual") : null;
            if (weapon == null || weapon.Find(VisualName) != null || LoadModel("player_weapon") == null) return;
            // Removed before the scale reset below so WeaponHead doesn't briefly inherit it.
            StripVisuals(weapon.gameObject);
            weapon.localScale = Vector3.one;
            GameObject visual = Spawn(LoadModel("player_weapon"), atlasMaterial);
            visual.transform.SetParent(weapon, false);
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            swapped++;
        }

        // ---------- Gameplay-safety check ----------

        private static Dictionary<string, string> SnapshotColliders()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (Collider collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include))
            {
                string key = PathOf(collider.transform) + "#" + collider.GetType().Name;
                int duplicate = 0;
                while (result.ContainsKey(key + "#" + duplicate)) duplicate++;
                Transform t = collider.transform;
                result[key + "#" + duplicate] = DescribeCollider(collider, t) + " world=" + t.position.ToString("F3");
            }
            return result;
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        private static void VerifyCollidersUnchanged(Dictionary<string, string> before, Dictionary<string, string> after)
        {
            StringBuilder problems = new StringBuilder();
            foreach (KeyValuePair<string, string> entry in before)
            {
                if (!after.TryGetValue(entry.Key, out string now)) problems.AppendLine("REMOVED " + entry.Key);
                else if (now != entry.Value) problems.AppendLine($"CHANGED {entry.Key}: {entry.Value} -> {now}");
            }
            foreach (string key in after.Keys)
                if (!before.ContainsKey(key)) problems.AppendLine("ADDED " + key);
            if (problems.Length > 0)
                throw new InvalidOperationException("[M16ArtPass] Art pass changed gameplay colliders:\n" + problems);
            Debug.Log($"[M16ArtPass] PASS: all {before.Count} scene colliders are identical before and after the art pass.");
        }
    }
}
