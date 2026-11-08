using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SheltreSim.Items;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// M9 — adds Stone, Iron Ore, and food (Blueberries) resource nodes to the M4 outdoor
    /// area, built as compound multi-primitive objects (not single flat-colored primitives
    /// like the original Wood/ScrapMetal nodes) so each resource type actually reads as
    /// what it is at a glance. Run after "Tools > SheltreSim > Setup M4 Outdoor Area".
    ///
    /// All positions below are hand-picked literal coordinates — never Random.Range — per
    /// the project's deliberate no-procedural-generation policy (see M4OutdoorAreaSetup's
    /// own rubble-placement comment, and DESIGN_NOTES.md for the history). Each position was
    /// checked to be at least 3m clear of every existing Wood/ScrapMetal node and every piece
    /// of decorative rubble placed by M4OutdoorAreaSetup.
    ///
    /// Note: Unity's built-in PrimitiveType enum has no Cone, so the food bush's "spruce"
    /// silhouette is approximated with a tall, narrow Cylinder (the closest built-in
    /// primitive) rather than a literal cone — consistent with the project's grey-box,
    /// primitives-only art direction.
    /// </summary>
    public static class M9WorldResourcesSetup
    {
        private static readonly Color StoneColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color IronColor = new Color(0.45f, 0.25f, 0.2f);
        private static readonly Color SpruceColor = new Color(0.1f, 0.3f, 0.15f);
        private static readonly Color BerryColor = new Color(0.15f, 0.15f, 0.5f);
        private static readonly Color TreeTrunkColor = new Color(0.4f, 0.25f, 0.1f);
        private static readonly Color TreeFoliageColor = new Color(0.15f, 0.4f, 0.15f);

        [MenuItem("Tools/SheltreSim/Setup M9 World Resources")]
        public static void SetupWorldResources()
        {
            GameObject outdoorArea = GameObject.Find("OutdoorArea");
            if (outdoorArea == null)
            {
                Debug.LogError("No 'OutdoorArea' GameObject found — run Tools > SheltreSim > Setup M4 Outdoor Area first.");
                return;
            }

            ResourceDefinition wood = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/Wood.asset");
            if (wood == null)
            {
                Debug.LogError("Could not load Assets/Data/Resources/Wood.asset — run Tools > SheltreSim > Setup M4 Outdoor Area first.");
                return;
            }

            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Resources");

            ResourceDefinition stone = CreateResourceDefinition("stone", "Stone", "Assets/Data/Resources/Stone.asset");
            ResourceDefinition ironOre = CreateResourceDefinition("iron_ore", "Iron Ore", "Assets/Data/Resources/IronOre.asset");

            ResourceDefinition berries = ScriptableObject.CreateInstance<ResourceDefinition>();
            berries.id = "food_berries";
            berries.displayName = "Blueberries";
            berries.isFood = true;
            berries.hungerRestored = 30f;
            AssetDatabase.CreateAsset(berries, "Assets/Data/Resources/Berries.asset");

            Transform parent = outdoorArea.transform;

            int treeCount = 0;
            BuildTree("TreeNode_1", new Vector3(19f, 0f, -14f), wood, parent); treeCount++;
            BuildTree("TreeNode_2", new Vector3(-19f, 0f, -24f), wood, parent); treeCount++;
            BuildTree("TreeNode_3", new Vector3(-19f, 0f, -6f), wood, parent); treeCount++;

            int stoneCount = 0;
            BuildRockCluster("StoneNode_1", new Vector3(6f, 0f, -5f), stone, StoneColor, parent); stoneCount++;
            BuildRockCluster("StoneNode_2", new Vector3(-4f, 0f, -18f), stone, StoneColor, parent); stoneCount++;
            BuildRockCluster("StoneNode_3", new Vector3(13f, 0f, -20f), stone, StoneColor, parent); stoneCount++;

            int ironCount = 0;
            BuildRockCluster("IronOreNode_1", new Vector3(-14f, 0f, -20f), ironOre, IronColor, parent); ironCount++;
            BuildRockCluster("IronOreNode_2", new Vector3(14f, 0f, -16f), ironOre, IronColor, parent); ironCount++;
            BuildRockCluster("IronOreNode_3", new Vector3(-6f, 0f, -30f), ironOre, IronColor, parent); ironCount++;

            int foodCount = 0;
            BuildFoodBush("FoodBush_1", new Vector3(10f, 0f, -6f), berries, parent); foodCount++;
            BuildFoodBush("FoodBush_2", new Vector3(-5f, 0f, -26f), berries, parent); foodCount++;
            BuildFoodBush("FoodBush_3", new Vector3(17f, 0f, -4f), berries, parent); foodCount++;
            BuildFoodBush("FoodBush_4", new Vector3(-9f, 0f, -6f), berries, parent); foodCount++;

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log($"M9 world resources ready: {treeCount} Tree (wood), {stoneCount} Stone, {ironCount} Iron Ore, and {foodCount} Food Bush (Blueberries) nodes added to OutdoorArea.");
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

        /// <summary>
        /// A tree: thin tall trunk (Cylinder) + wide flattish foliage (Sphere) on top. The
        /// ResourceNode and collider live on the parent empty so a raycast hit on either
        /// visual-only child primitive resolves to this node via GetComponentInParent.
        /// </summary>
        internal static GameObject BuildTree(string name, Vector3 position, ResourceDefinition wood, Transform parent)
        {
            GameObject node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.transform.position = position;

            ResourceNode resourceNode = node.AddComponent<ResourceNode>();
            resourceNode.resourceType = wood;
            resourceNode.quantityPerHarvest = 1;
            resourceNode.remainingCharges = 3;

            CapsuleCollider collider = node.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1.6f, 0f);
            collider.height = 3.2f;
            collider.radius = 0.9f;

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(node.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            trunk.transform.localScale = new Vector3(0.3f, 1.2f, 0.3f);
            EditorMaterialUtility.Apply(trunk, TreeTrunkColor);

            GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            foliage.name = "Foliage";
            foliage.transform.SetParent(node.transform, false);
            foliage.transform.localPosition = new Vector3(0f, 2.9f, 0f);
            foliage.transform.localScale = new Vector3(1.8f, 1.0f, 1.8f);
            EditorMaterialUtility.Apply(foliage, TreeFoliageColor);

            // Two smaller secondary foliage clumps at varying heights/offsets so the tree
            // reads as a fuller canopy instead of a single sphere on a stick ("lollipop").
            // Purely cosmetic children (no collider changes, same fallback-to-parent pattern
            // as Trunk/Foliage above). Offset direction/size deterministic per tree, seeded
            // from the node's own name — never UnityEngine.Random, see CLAUDE.md.
            System.Random rnd = new System.Random(name.GetHashCode());
            float[] clumpHeights = { 2.3f, 2.6f };
            for (int i = 0; i < clumpHeights.Length; i++)
            {
                float angle = (float)(rnd.NextDouble() * 360.0) * Mathf.Deg2Rad;
                float radius = 0.5f + (float)rnd.NextDouble() * 0.4f;
                float scale = 0.9f + (float)rnd.NextDouble() * 0.3f;

                GameObject clump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                clump.name = "FoliageClump_" + (i + 1);
                clump.transform.SetParent(node.transform, false);
                clump.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, clumpHeights[i], Mathf.Sin(angle) * radius);
                clump.transform.localScale = new Vector3(scale, scale * 0.8f, scale);
                EditorMaterialUtility.Apply(clump, TreeFoliageColor);
            }

            return node;
        }

        /// <summary>
        /// Shared builder for Stone and Iron Ore nodes: a small irregular cluster of two
        /// offset, slightly rotated cubes, colored differently per resource type.
        /// </summary>
        internal static GameObject BuildRockCluster(string name, Vector3 position, ResourceDefinition resourceType, Color color, Transform parent)
        {
            GameObject node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.transform.position = position;

            ResourceNode resourceNode = node.AddComponent<ResourceNode>();
            resourceNode.resourceType = resourceType;
            resourceNode.quantityPerHarvest = 1;
            resourceNode.remainingCharges = 3;

            BoxCollider collider = node.AddComponent<BoxCollider>();
            collider.center = new Vector3(0.1f, 0.4f, 0.05f);
            collider.size = new Vector3(1.5f, 0.9f, 1.4f);

            GameObject rockA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rockA.name = "RockA";
            rockA.transform.SetParent(node.transform, false);
            rockA.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            rockA.transform.localRotation = Quaternion.Euler(5f, 15f, 8f);
            rockA.transform.localScale = new Vector3(0.9f, 0.8f, 0.8f);
            EditorMaterialUtility.Apply(rockA, color);

            GameObject rockB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rockB.name = "RockB";
            rockB.transform.SetParent(node.transform, false);
            rockB.transform.localPosition = new Vector3(0.45f, 0.25f, 0.35f);
            rockB.transform.localRotation = Quaternion.Euler(-10f, -20f, 12f);
            rockB.transform.localScale = new Vector3(0.5f, 0.5f, 0.6f);
            EditorMaterialUtility.Apply(rockB, color);

            // A third, smaller rock piece for a less symmetric two-rock silhouette. Purely
            // cosmetic child (same fallback-to-parent pattern as RockA/RockB above); the
            // parent's own BoxCollider set above is untouched. Offset/rotation/scale
            // deterministic per node, seeded from its own name — never UnityEngine.Random.
            System.Random rnd = new System.Random(name.GetHashCode());
            GameObject rockC = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rockC.name = "RockC";
            rockC.transform.SetParent(node.transform, false);
            float offsetX = -0.3f - (float)rnd.NextDouble() * 0.25f;
            float offsetZ = -0.25f - (float)rnd.NextDouble() * 0.2f;
            rockC.transform.localPosition = new Vector3(offsetX, 0.2f + (float)rnd.NextDouble() * 0.1f, offsetZ);
            rockC.transform.localRotation = Quaternion.Euler(
                (float)(rnd.NextDouble() * 20.0 - 10.0),
                (float)(rnd.NextDouble() * 360.0),
                (float)(rnd.NextDouble() * 20.0 - 10.0));
            float rockCScale = 0.3f + (float)rnd.NextDouble() * 0.15f;
            rockC.transform.localScale = new Vector3(rockCScale, rockCScale * 0.8f, rockCScale * 0.9f);
            EditorMaterialUtility.Apply(rockC, color);

            return node;
        }

        /// <summary>
        /// A food bush: a tall, narrow Cylinder standing in for a small spruce (Unity has no
        /// built-in Cone primitive) with several small blue "berry" spheres dotted around its
        /// surface at varying heights.
        /// </summary>
        internal static GameObject BuildFoodBush(string name, Vector3 position, ResourceDefinition berries, Transform parent)
        {
            GameObject node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.transform.position = position;

            ResourceNode resourceNode = node.AddComponent<ResourceNode>();
            resourceNode.resourceType = berries;
            resourceNode.quantityPerHarvest = 1;
            resourceNode.remainingCharges = 3;

            CapsuleCollider collider = node.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.8f, 0f);
            collider.height = 1.8f;
            collider.radius = 0.6f;

            GameObject spruce = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spruce.name = "SpruceCone";
            spruce.transform.SetParent(node.transform, false);
            spruce.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            spruce.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            EditorMaterialUtility.Apply(spruce, SpruceColor);

            // Berry count/placement varies per bush — deterministic, seeded from the node's
            // own name (never UnityEngine.Random, see CLAUDE.md), not a fixed 4-offset array,
            // so bushes don't all look identical. Berries are spaced up the spruce stand-in
            // at increasing height, each at a small random angle/radius around it, so the
            // silhouette still reads clearly as "berries dotted on a bush" at a glance.
            System.Random rnd = new System.Random(name.GetHashCode());
            int berryCount = 3 + rnd.Next(0, 3); // 3-5 berries

            for (int i = 0; i < berryCount; i++)
            {
                float heightT = (float)(i + 1) / (berryCount + 1);
                float height = 0.4f + heightT * 1.3f;
                float angle = (float)(rnd.NextDouble() * 360.0) * Mathf.Deg2Rad;
                float radius = 0.2f + (float)rnd.NextDouble() * 0.2f;
                float berryScale = 0.12f + (float)rnd.NextDouble() * 0.08f;

                GameObject berry = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                berry.name = "Berry_" + (i + 1);
                berry.transform.SetParent(node.transform, false);
                berry.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                berry.transform.localScale = new Vector3(berryScale, berryScale, berryScale);
                EditorMaterialUtility.Apply(berry, BerryColor);
            }

            return node;
        }
    }
}
