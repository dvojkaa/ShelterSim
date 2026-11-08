using System;
using SheltreSim.Enemies;
using SheltreSim.Items;
using SheltreSim.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    public static class M19ExpeditionSetup
    {
        [MenuItem("Tools/SheltreSim/Setup M19 Expeditions")]
        public static void Setup()
        {
            var world=UnityEngine.Object.FindFirstObjectByType<ProceduralWorld>();
            var player=GameObject.Find("Player");
            if(world==null||player==null)throw new InvalidOperationException("Build M18 first.");
            const string partsPath="Assets/Data/Resources/ExpeditionParts.asset";
            var parts=AssetDatabase.LoadAssetAtPath<ResourceDefinition>(partsPath);
            if(parts==null)
            {
                parts=ScriptableObject.CreateInstance<ResourceDefinition>();parts.id="expedition_parts";parts.displayName="Expedition Parts";
                AssetDatabase.CreateAsset(parts,partsPath);
            }
            world.assets.expeditionParts=parts;
            var progression=player.GetComponent<ExpeditionProgression>()??player.AddComponent<ExpeditionProgression>();
            progression.expeditionParts=parts;progression.wood=world.assets.wood;progression.scrap=world.assets.scrap;progression.iron=world.assets.iron;
            var inventory=UnityEngine.Object.FindFirstObjectByType<InventoryUI>();
            if(inventory!=null&&!inventory.knownResources.Contains(parts)){inventory.knownResources.Add(parts);EditorUtility.SetDirty(inventory);}
            // A new map-owned placeholder material; existing art/audio assets remain read-only.
            const string waterPath="Assets/Data/World/ShallowWater.mat";
            var water=AssetDatabase.LoadAssetAtPath<Material>(waterPath);
            if(water==null)
            {
                water=new Material(world.assets.biomeMaterials[0]);water.color=new Color(.22f,.48f,.65f);
                AssetDatabase.CreateAsset(water,waterPath);
            }
            world.assets.waterMaterial=water;
            if(world.GetComponent<WorldAtmosphere>()==null)world.gameObject.AddComponent<WorldAtmosphere>();
            // Clone an existing fully dressed raider into a NEW map-owned prefab. Never mutate its source art.
            const string raiderPath="Assets/Data/World/ExpeditionRaider.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(raiderPath);
            if(prefab==null)
            {
                var source=UnityEngine.Object.FindFirstObjectByType<EnemyAI>();
                if(source==null)throw new InvalidOperationException("No original raider template.");
                var copy=UnityEngine.Object.Instantiate(source.gameObject);copy.name="ExpeditionRaider";
                copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                prefab=PrefabUtility.SaveAsPrefabAsset(copy,raiderPath);
                UnityEngine.Object.DestroyImmediate(copy);
            }
            world.assets.raiderPrefab=prefab;
            var console=GameObject.Find("ExpeditionConsole");
            if(console==null)
            {
                console=GameObject.CreatePrimitive(PrimitiveType.Cube);console.name="ExpeditionConsole";
                console.transform.position=new Vector3(-2.35f,.65f,1.7f);
                console.transform.localScale=new Vector3(.7f,1.3f,.7f);
                console.GetComponent<Renderer>().sharedMaterial=world.assets.structureMaterial;
                console.AddComponent<ExpeditionUpgradeStation>();
            }
            EditorUtility.SetDirty(world);EditorUtility.SetDirty(progression);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[M19] Expedition landmarks, waterways, wildlife, patrols, progression and simulation-only weather wired. Existing visual/audio assets untouched.");
        }
    }
}
