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

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "OutdoorGround";
            ground.transform.SetParent(outdoorArea.transform, false);
            ground.transform.position = new Vector3(0f, -0.1f, -13f);
            ground.transform.localScale = new Vector3(24f, 0.2f, 20f);

            CreateBoundaryBlock("Boundary_Left", new Vector3(-12f, 1.5f, -13f), new Vector3(0.2f, 3f, 20f), outdoorArea.transform);
            CreateBoundaryBlock("Boundary_Right", new Vector3(12f, 1.5f, -13f), new Vector3(0.2f, 3f, 20f), outdoorArea.transform);
            CreateBoundaryBlock("Boundary_Far", new Vector3(0f, 1.5f, -23f), new Vector3(24f, 3f, 0.2f), outdoorArea.transform);

            CreateResourceNode("WoodNode_1", PrimitiveType.Cylinder, new Vector3(-6f, 1.5f, -8f), new Vector3(0.6f, 1.5f, 0.6f), wood, outdoorArea.transform);
            CreateResourceNode("WoodNode_2", PrimitiveType.Cylinder, new Vector3(4f, 1.5f, -18f), new Vector3(0.6f, 1.5f, 0.6f), wood, outdoorArea.transform);
            CreateResourceNode("ScrapMetalNode_1", PrimitiveType.Sphere, new Vector3(6f, 0.8f, -8f), new Vector3(0.8f, 0.8f, 0.8f), scrapMetal, outdoorArea.transform);
            CreateResourceNode("ScrapMetalNode_2", PrimitiveType.Sphere, new Vector3(-4f, 0.8f, -18f), new Vector3(0.8f, 0.8f, 0.8f), scrapMetal, outdoorArea.transform);

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

            Debug.Log("M4 outdoor area ready. Walk through the bunker doorway to find 2 Wood and 2 Scrap Metal nodes — press E near one to gather (Inventory logs totals to Console).");
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

        private static GameObject CreateBoundaryBlock(string name, Vector3 position, Vector3 scale, Transform parent)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = scale;
            return block;
        }

        private static GameObject CreateResourceNode(string name, PrimitiveType primitiveType, Vector3 position, Vector3 scale, ResourceDefinition resourceType, Transform parent)
        {
            GameObject node = GameObject.CreatePrimitive(primitiveType);
            node.name = name;
            node.transform.SetParent(parent, false);
            node.transform.position = position;
            node.transform.localScale = scale;

            ResourceNode resourceNode = node.AddComponent<ResourceNode>();
            resourceNode.resourceType = resourceType;
            resourceNode.quantityPerHarvest = 1;
            resourceNode.remainingCharges = 3;

            return node;
        }
    }
}
