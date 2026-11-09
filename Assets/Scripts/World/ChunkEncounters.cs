using System;
using SheltreSim.Enemies;
using SheltreSim.Items;
using SheltreSim.Player;
using UnityEngine;

namespace SheltreSim.World
{
    public static class ChunkEncounters
    {
        /// <summary>
        /// Ruined world only (ProceduralWorld calls it after the bombs). Habitat groups, camps and boss lairs live in
        /// SheltreSim.Bestiary; each draws from its own per-chunk random, so none of them shifts anything else.
        /// </summary>
        public static void Build(Transform parent, Vector2Int coordinate, int seed, ProceduralWorldAssets assets)
        {
            float x = coordinate.x * 128f + 64f, z = coordinate.y * 128f + 64f;
            int hash = WorldLayout.Hash(coordinate.x,coordinate.y,seed+197);
            bool town=WorldLayout.IsTownChunk(coordinate,seed), landmark=ProceduralLandmarks.HasLandmark(coordinate,seed);
            bool camp=SheltreSim.Bestiary.EnemyCamps.IsCamp(coordinate,seed);
            if (!camp && !town && !landmark && hash%23==0 && !WorldLayout.IsHomePlot(x,z) && !WorldLayout.IsWater(x,z,seed,10f))
                ReliefCache(parent, coordinate, seed, assets, x, z);
            // Placement checks query this chunk's colliders, built a moment ago this frame.
            Physics.SyncTransforms();
            if (camp) SheltreSim.Bestiary.EnemyCamps.Build(parent, coordinate, seed, assets);
            else SheltreSim.Bestiary.EncounterHabitats.Build(parent, coordinate, seed);
            if (landmark) SheltreSim.Bestiary.BossLairs.Build(parent, coordinate, seed, assets);
        }

        private static void ReliefCache(Transform parent, Vector2Int coordinate, int seed, ProceduralWorldAssets assets, float x, float z)
        {
                var cache=new GameObject("ReliefCache");
                cache.transform.SetParent(parent,false);
                cache.transform.position=new Vector3(x,WorldLayout.HeightAt(x,z,seed),z);
                if(assets.lootModel!=null)
                {
                    var visual=UnityEngine.Object.Instantiate(assets.lootModel,cache.transform,false);
                    visual.transform.localScale=Vector3.one*2f;
                    foreach(var renderer in visual.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=assets.atlasMaterial;
                }
                var box=cache.AddComponent<BoxCollider>();box.center=Vector3.up*.4f;box.size=Vector3.one*.8f;
                var loot=cache.AddComponent<LootContainer>();loot.hasLoot=true;
                loot.entries.Add(new LootEntry{resource=assets.water,amount=3});
                loot.entries.Add(new LootEntry{resource=assets.medicine,amount=1});
                if(assets.expeditionParts!=null)loot.entries.Add(new LootEntry{resource=assets.expeditionParts,amount=1});
                var discovery=cache.AddComponent<LandmarkDiscovery>();
                discovery.stableId=$"relief_{seed}_{coordinate.x}_{coordinate.y}";
                discovery.title="Abandoned relief supplies";
        }
    }

    public sealed class ChunkEncounterSlot : MonoBehaviour
    {
        public EnemyAI Actor { get; private set; }
        public bool Defeated { get; private set; }
        public float Health => Defeated || Actor==null ? 0f : Actor.health;
        private Vector3 anchor;
        private int seed;
        private Transform player;
        public void Initialize(EnemyAI actor,int worldSeed)
        {
            Actor=actor;seed=worldSeed;anchor=transform.position;
            if(Actor!=null)Actor.OnDeath+=OnDeath;
            var controller=UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            if(controller!=null)player=controller.transform;
        }
        private void OnDeath(EnemyAI actor){Defeated=true;Actor=null;}
        public void Restore(bool defeated,float health)
        {
            if(defeated)
            {
                Defeated=true;
                if(Actor!=null){Actor.OnDeath-=OnDeath;Actor.gameObject.SetActive(false);Destroy(Actor.gameObject);Actor=null;}
            }
            else if(Actor!=null)Actor.RestoreState(Actor.transform.position,Actor.transform.rotation,health);
        }
        private void Update()
        {
            if(Actor==null||Defeated||Actor.IsDead||Actor.Brain!=null||Actor.IsChasing||Time.timeScale<=0f||player==null)return;
            if(ProceduralWorld.Active!=null&&!ProceduralWorld.Active.HasGroundAt(Actor.transform.position))return;
            if((player.position-Actor.transform.position).sqrMagnitude<Actor.detectionRadius*Actor.detectionRadius)return;
            float phase=Time.time*.045f+(WorldLayout.Hash((int)anchor.x,(int)anchor.z,seed)%628)/100f;
            Vector3 destination=anchor+new Vector3(Mathf.Sin(phase)*5f,0f,Mathf.Cos(phase)*18f);
            Vector3 direction=Vector3.ProjectOnPlane(destination-Actor.transform.position,Vector3.up).normalized;
            var body=Actor.GetComponent<CharacterController>();
            if(body.enabled)body.Move(direction*1.1f*Time.deltaTime);
            if(direction.sqrMagnitude>.01f)Actor.transform.rotation=Quaternion.Slerp(Actor.transform.rotation,Quaternion.LookRotation(direction),Time.deltaTime*3f);
        }
        private void OnDestroy(){if(Actor!=null)Actor.OnDeath-=OnDeath;}
    }
}
