using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SheltreSim.Items;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Builds the M4 outdoor area as an extension of the existing M1 bunker scene (not a
    /// separate Unity scene — deliberate decision to avoid scene-transition/persistence
    /// complexity). Run after "Tools > SheltreSim > Build M1 Bunker Scene" has already
    /// produced a scene with a "Player" GameObject.
    /// </summary>
    public static class M4OutdoorAreaSetup
    {
        [MenuItem("Tools/SheltreSim/Setup M4 Outdoor Area")]
        public static void SetupOutdoorArea()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("No 'Player' GameObject found — run Tools > SheltreSim > Build M1 Bunker Scene first.");
                return;
            }

            Inventory inventory = player.GetComponent<Inventory>();
            if (inventory == null)
            {
                inventory = player.AddComponent<Inventory>();
            }

            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Resources");

            ResourceDefinition wood = CreateResourceDefinition("wood", "Wood", "Assets/Data/Resources/Wood.asset");
            ResourceDefinition scrapMetal = CreateResourceDefinition("scrap_metal", "Scrap Metal", "Assets/Data/Resources/ScrapMetal.asset");

            GameObject outdoorArea = new GameObject("OutdoorArea");

            // Ground spans X [-20,20] (40m wide) x Z [-3,-39] (36m deep), starting right at
            // the bunker's south wall (Z=-3) and centered on the doorway's X position.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "OutdoorGround";
            ground.transform.SetParent(outdoorArea.transform, false);
            ground.transform.position = new Vector3(0f, -0.1f, -21f);
            ground.transform.localScale = new Vector3(40f, 0.2f, 36f);
            EditorMaterialUtility.Apply(ground, new Color(0.35f, 0.3f, 0.18f));

            Color boundaryColor = new Color(0.3f, 0.3f, 0.3f);
            CreateBoundaryBlock("Boundary_Left", new Vector3(-20f, 1.5f, -21f), new Vector3(0.2f, 3f, 36f), outdoorArea.transform, boundaryColor);
            CreateBoundaryBlock("Boundary_Right", new Vector3(20f, 1.5f, -21f), new Vector3(0.2f, 3f, 36f), outdoorArea.transform, boundaryColor);
            CreateBoundaryBlock("Boundary_Far", new Vector3(0f, 1.5f, -39f), new Vector3(40f, 3f, 0.2f), outdoorArea.transform, boundaryColor);

            Color woodColor = new Color(0.4f, 0.25f, 0.1f);
            CreateResourceNode("WoodNode_1", PrimitiveType.Cylinder, new Vector3(-14f, 1.5f, -8f), new Vector3(0.6f, 1.5f, 0.6f), wood, outdoorArea.transform, woodColor);
            CreateResourceNode("WoodNode_2", PrimitiveType.Cylinder, new Vector3(12f, 1.5f, -10f), new Vector3(0.6f, 1.5f, 0.6f), wood, outdoorArea.transform, woodColor);
            CreateResourceNode("WoodNode_3", PrimitiveType.Cylinder, new Vector3(-16f, 1.5f, -28f), new Vector3(0.6f, 1.5f, 0.6f), wood, outdoorArea.transform, woodColor);
            CreateResourceNode("WoodNode_4", PrimitiveType.Cylinder, new Vector3(6f, 1.5f, -34f), new Vector3(0.6f, 1.5f, 0.6f), wood, outdoorArea.transform, woodColor);

            Color scrapColor = new Color(0.6f, 0.6f, 0.65f);
            CreateResourceNode("ScrapMetalNode_1", PrimitiveType.Sphere, new Vector3(14f, 0.8f, -8f), new Vector3(0.8f, 0.8f, 0.8f), scrapMetal, outdoorArea.transform, scrapColor);
            CreateResourceNode("ScrapMetalNode_2", PrimitiveType.Sphere, new Vector3(-10f, 0.8f, -14f), new Vector3(0.8f, 0.8f, 0.8f), scrapMetal, outdoorArea.transform, scrapColor);
            CreateResourceNode("ScrapMetalNode_3", PrimitiveType.Sphere, new Vector3(16f, 0.8f, -30f), new Vector3(0.8f, 0.8f, 0.8f), scrapMetal, outdoorArea.transform, scrapColor);
            CreateResourceNode("ScrapMetalNode_4", PrimitiveType.Sphere, new Vector3(-6f, 0.8f, -36f), new Vector3(0.8f, 0.8f, 0.8f), scrapMetal, outdoorArea.transform, scrapColor);

            // Decorative rubble — purely visual clutter, no components beyond the primitive's
            // own default collider. Hand-picked positions/scales/rotations (not Random.Range)
            // to stay consistent with the project's no-procedural-generation decision. Mostly
            // flattened/rotated cubes, with a couple of pieces swapped to a squat wide Cylinder
            // ("slab" of broken concrete) or a slightly squashed Sphere ("boulder" chunk) so
            // the rubble field isn't visually all-identical-cubes.
            Color[] rubbleColors =
            {
                new Color(0.4f, 0.37f, 0.33f),
                new Color(0.33f, 0.3f, 0.28f),
                new Color(0.45f, 0.4f, 0.35f),
            };
            CreateRubble("Rubble_1", PrimitiveType.Cube, new Vector3(3f, 0.4f, -6f), new Vector3(0.8f, 0.6f, 0.7f), 15f, rubbleColors[0], outdoorArea.transform);
            CreateRubble("Rubble_2", PrimitiveType.Cube, new Vector3(-8f, 0.3f, -12f), new Vector3(0.5f, 0.5f, 0.6f), 40f, rubbleColors[1], outdoorArea.transform);
            CreateRubble("Rubble_3", PrimitiveType.Cylinder, new Vector3(10f, 0.12f, -18f), new Vector3(1.3f, 0.18f, 1.1f), 70f, rubbleColors[2], outdoorArea.transform);
            CreateRubble("Rubble_4", PrimitiveType.Cube, new Vector3(-2f, 0.35f, -22f), new Vector3(0.6f, 0.4f, 0.6f), 10f, rubbleColors[0], outdoorArea.transform);
            CreateRubble("Rubble_5", PrimitiveType.Sphere, new Vector3(18f, 0.33f, -24f), new Vector3(0.9f, 0.65f, 0.8f), 55f, rubbleColors[1], outdoorArea.transform);
            CreateRubble("Rubble_6", PrimitiveType.Cube, new Vector3(-18f, 0.4f, -18f), new Vector3(0.7f, 0.5f, 0.7f), 25f, rubbleColors[2], outdoorArea.transform);
            CreateRubble("Rubble_7", PrimitiveType.Cylinder, new Vector3(0f, 0.14f, -30f), new Vector3(1.6f, 0.22f, 1.3f), 80f, rubbleColors[0], outdoorArea.transform);
            CreateRubble("Rubble_8", PrimitiveType.Cube, new Vector3(8f, 0.3f, -36f), new Vector3(0.5f, 0.4f, 0.5f), 5f, rubbleColors[1], outdoorArea.transform);
            CreateRubble("Rubble_9", PrimitiveType.Sphere, new Vector3(-12f, 0.4f, -32f), new Vector3(1.0f, 0.75f, 0.8f), 60f, rubbleColors[2], outdoorArea.transform);

            GameObject saveManagerObject = GameObject.Find("SaveManager");
            if (saveManagerObject != null)
            {
                SheltreSim.Saving.SaveManager saveManager = saveManagerObject.GetComponent<SheltreSim.Saving.SaveManager>();
                if (saveManager != null)
                {
                    saveManager.inventory = inventory;
                }
                else
                {
                    Debug.LogWarning("No SaveManager found — inventory will not be included in save/load. Run Tools > SheltreSim > Setup M3 Survival & Save first if you want that.");
                }
            }
            else
            {
                Debug.LogWarning("No SaveManager found — inventory will not be included in save/load. Run Tools > SheltreSim > Setup M3 Survival & Save first if you want that.");
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("M4 outdoor area ready. Walk through the bunker doorway to find 4 Wood and 4 Scrap Metal nodes scattered across the expanded wasteland — press E near one to gather (Inventory logs totals to Console).");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static ResourceDefinition CreateResourceDefinition(string id, string displayName, string path)
        {
            ResourceDefinition definition = ScriptableObject.CreateInstance<ResourceDefinition>();
            definition.id = id;
            definition.displayName = displayName;

            AssetDatabase.CreateAsset(definition, path);

            return definition;
        }

        private static GameObject CreateBoundaryBlock(string name, Vector3 position, Vector3 scale, Transform parent, Color color)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = scale;
            EditorMaterialUtility.Apply(block, color);
            return block;
        }

        private static GameObject CreateResourceNode(string name, PrimitiveType primitiveType, Vector3 position, Vector3 scale, ResourceDefinition resourceType, Transform parent, Color color)
        {
            GameObject node = GameObject.CreatePrimitive(primitiveType);
            node.name = name;
            node.transform.SetParent(parent, false);
            node.transform.position = position;
            node.transform.localScale = scale;
            EditorMaterialUtility.Apply(node, color);

            ResourceNode resourceNode = node.AddComponent<ResourceNode>();
            resourceNode.resourceType = resourceType;
            resourceNode.quantityPerHarvest = 1;
            resourceNode.remainingCharges = 3;

            // --- Visual variation only below this point. The node's own transform/scale set
            // above (and therefore its own default collider, which PlayerInteractor's interact
            // raycast/reachability depend on) is never touched again after this point.
            // Deterministic, seeded from the node's own name — never UnityEngine.Random, see
            // CLAUDE.md's "what random means" rule. M9 gave Stone/Iron/Trees/Food a compound-
            // shape treatment; these original M4 Wood/ScrapMetal nodes stay their original
            // single-primitive shape (cylinder/sphere) by design, but get a bit of the same
            // "doesn't look identical to every other instance" treatment here. ---
            System.Random rnd = new System.Random(name.GetHashCode());

            // A Y-axis-only spin is safe here: the Cylinder's default MeshCollider and the
            // Sphere's default SphereCollider are both rotationally symmetric about the
            // vertical axis, so this changes only cosmetic facing, never the collider's
            // effective shape/size/position.
            node.transform.rotation = Quaternion.Euler(0f, (float)(rnd.NextDouble() * 360.0), 0f);

            AddResourceNodeDetail(node, primitiveType, scale, color, rnd);

            return node;
        }

        /// <summary>
        /// A second, smaller offset primitive of the same kind as the node — a purely
        /// cosmetic child with no ResourceNode of its own. A raycast hit on it resolves back
        /// to the parent node via PlayerInteractor's GetComponentInParent fallback, the same
        /// pattern M9WorldResourcesSetup's BuildTree/BuildRockCluster child meshes already
        /// rely on. Sized/positioned in world units then converted back through the node's
        /// own lossy scale, so the result looks proportioned regardless of the node's own
        /// (untouched) scale.
        /// </summary>
        private static void AddResourceNodeDetail(GameObject node, PrimitiveType primitiveType, Vector3 nodeScale, Color color, System.Random rnd)
        {
            Vector3 baseSize = primitiveType == PrimitiveType.Cylinder ? new Vector3(1f, 2f, 1f) : new Vector3(1f, 1f, 1f);
            Vector3 mainWorldSize = Vector3.Scale(baseSize, nodeScale);

            float sizeFactor = 0.4f + (float)rnd.NextDouble() * 0.2f; // 0.4-0.6x the main shape
            Vector3 detailWorldSize = mainWorldSize * sizeFactor;

            float angleRad = (float)(rnd.NextDouble() * 360.0) * Mathf.Deg2Rad;
            float distance = Mathf.Max(mainWorldSize.x, mainWorldSize.z) * 0.6f;

            GameObject detail = GameObject.CreatePrimitive(primitiveType);
            detail.name = node.name + "_Detail";
            detail.transform.SetParent(node.transform, false);
            detail.transform.rotation = Quaternion.Euler(0f, (float)(rnd.NextDouble() * 360.0), 0f);
            EditorMaterialUtility.Apply(detail, color);

            Vector3 parentLossyScale = node.transform.lossyScale;
            detail.transform.localScale = new Vector3(
                detailWorldSize.x / parentLossyScale.x,
                detailWorldSize.y / parentLossyScale.y,
                detailWorldSize.z / parentLossyScale.z);

            // Sits a bit lower than the main shape's own pivot, like a smaller companion
            // piece resting near its base, without needing to assume exactly where that
            // base is in world space (varies slightly between Wood and ScrapMetal nodes).
            float detailLocalY = -(mainWorldSize.y * 0.3f) / parentLossyScale.y;
            detail.transform.localPosition = new Vector3(
                Mathf.Cos(angleRad) * distance / parentLossyScale.x,
                detailLocalY,
                Mathf.Sin(angleRad) * distance / parentLossyScale.z);
        }

        private static GameObject CreateRubble(string name, PrimitiveType primitiveType, Vector3 position, Vector3 scale, float yRotation, Color color, Transform parent)
        {
            GameObject rubble = GameObject.CreatePrimitive(primitiveType);
            rubble.name = name;
            rubble.transform.SetParent(parent, false);
            rubble.transform.position = position;
            rubble.transform.localScale = scale;
            rubble.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
            EditorMaterialUtility.Apply(rubble, color);
            return rubble;
        }
    }
}
