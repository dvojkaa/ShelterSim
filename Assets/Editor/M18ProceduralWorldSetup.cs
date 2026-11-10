using System;
using System.Linq;
using SheltreSim.Items;
using SheltreSim.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>Adds streaming generation after the art pass without re-rolling the starter region.</summary>
    public static class M18ProceduralWorldSetup
    {
        [MenuItem("Tools/SheltreSim/Setup M18 Procedural World")]
        public static void Setup()
        {
            var root = GameObject.Find("ProceduralWorld");
            if (root == null) root = new GameObject("ProceduralWorld");
            var world = root.GetComponent<ProceduralWorld>() ?? root.AddComponent<ProceduralWorld>();
            world.initialSeed = 20261108;
            world.loadRadius = 3;
            var ground = Material("SS_Ground");
            Color[] colors = {
                new Color(.76f,.67f,.50f), new Color(.40f,.58f,.33f), new Color(1f,.66f,.35f),
                new Color(1.65f,1.9f,2.1f), new Color(.39f,.48f,.32f), new Color(.68f,.70f,.72f)
            };
            string[] names = { "Wasteland", "Forest", "Desert", "Snow", "Marsh", "Highlands" };
            if (!AssetDatabase.IsValidFolder("Assets/Data/World")) AssetDatabase.CreateFolder("Assets/Data", "World");
            var biomes = new Material[6];
            for (int i = 0; i < biomes.Length; i++)
            {
                string path = "Assets/Data/World/Biome_" + names[i] + ".mat";
                biomes[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (biomes[i] == null)
                {
                    biomes[i] = new Material(ground);
                    biomes[i].color = colors[i];
                    biomes[i].enableInstancing = true;
                    AssetDatabase.CreateAsset(biomes[i], path);
                }
                // Existing biome styling belongs to the visual developer; never overwrite it.
            }
            world.assets = new ProceduralWorldAssets
            {
                biomeMaterials = biomes,
                atlasMaterial = Material("SS_Atlas"), roadMaterial = Material("SS_RoofTar"),
                structureMaterial = Material("SS_Brick"),
                trees = Models("tree_a", "tree_b", "tree_c"),
                rocks = Models("rock_stone_a", "rock_stone_b", "ore_iron_a", "ore_iron_b"),
                bushes = Models("food_bush_a", "food_bush_b"),
                cars = Models("car_sedan_grey", "car_sedan_green", "car_pickup_green"),
                lootModel = Models("loot_crate")[0],
                wood = Resource("Wood"), stone = Resource("Stone"), iron = Resource("IronOre"),
                berries = Resource("Berries"), water = Resource("Water"), scrap = Resource("ScrapMetal"),
                medicine = Resource("FirstAidKit"), plantFiber = Resource("PlantFiber"),
                wildlifeDeer = Models("animal_deer")[0], wildlifeBoar = Models("animal_boar")[0], wildlifeCrow = Models("animal_crow")[0],
                campTent = Models("camp_tent")[0], campFire = Models("camp_fire")[0], campLockedChest = Models("camp_locked_chest")[0],
                landmarkRelayCabinet = Models("lm_relay_cabinet")[0], landmarkFuelPump = Models("lm_fuel_pump")[0],
                lairWarningPost = Models("lair_warning_post")[0]
            };
            if (root.GetComponent<WorldMapUI>() == null) root.AddComponent<WorldMapUI>();
            // Preserve hierarchy indices used by older saves; disable the old tiny-map fences.
            var outdoor = GameObject.Find("OutdoorArea");
            if (outdoor != null)
                foreach (Transform t in outdoor.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Boundary_", StringComparison.Ordinal)) t.gameObject.SetActive(false);
            // Lighting, fog and camera presentation remain owned by the visual developer.
            EditorUtility.SetDirty(world);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[M18] Seeded runtime world wired: 128m chunks, six biomes, towns, roads, sparse persistent resources. Starter region preserved.");
        }

        private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/" + name + ".mat")
            ?? throw new InvalidOperationException("Missing world material " + name);
        private static ResourceDefinition Resource(string name) => AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/" + name + ".asset")
            ?? throw new InvalidOperationException("Missing world resource " + name);
        private static GameObject[] Models(params string[] names) => names.Select(name =>
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/" + name + ".fbx")
            ?? throw new InvalidOperationException("Missing world model " + name)).ToArray();
    }
}
