using System.Collections.Generic;
using SheltreSim.Enemies;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// What species an enemy is, and its one-time death loot. Bestiary.Spawn adds it. Raid actors (RaidAgent) keep their own loot.
    /// LootSeed must be stable for the actor's persistent identity (chunk + slot), so a revisit can't roll a second, different bag.
    /// </summary>
    public sealed class SpeciesActor : MonoBehaviour
    {
        public SpeciesId Species;
        public int LootSeed;
        /// <summary>Optional: where a boss or camp member stays. Brains read it; zero means no anchor.</summary>
        public Vector3 GuardPoint;
        public float GuardRadius;
        public SpeciesSpec Spec => SpeciesSpec.Of(Species);
        public EnemyAI Enemy { get; private set; }
        public event System.Action<SpeciesActor> Died;

        private void Awake()
        {
            Enemy = GetComponent<EnemyAI>();
            if (Enemy != null) Enemy.OnDeath += OnDeath;
        }

        private void OnDestroy() { if (Enemy != null) Enemy.OnDeath -= OnDeath; }

        private void OnDeath(EnemyAI enemy)
        {
            Died?.Invoke(this);
            if (GetComponent<SheltreSim.Raids.RaidAgent>() != null || !enemy.TryClaimDeathLoot()) return;
            List<KeyValuePair<string, int>> items = Spec.RollLoot(LootSeed);
            var tier = Spec.IsBoss ? SheltreSim.Raids.LootTier.Warlord : Spec.Health >= 70f ? SheltreSim.Raids.LootTier.Elite : SheltreSim.Raids.LootTier.Ordinary;
            SheltreSim.Raids.LootBagSystem.Spawn(Bestiary.GroundBelow(enemy.CenterPosition, transform), SheltreSim.Raids.LootBagSystem.NextId++, tier, items);
            if (Spec.IsBoss) SheltreSim.UI.GameMessageBus.Post($"{Spec.Name} is down. Its cache is on the ground.");
        }
    }

    public static class Bestiary
    {
        /// <summary>Builds an enemy of a species from the raider prefab. Active, under parent, standing at feet.</summary>
        public static SpeciesActor Spawn(SpeciesId species, Transform parent, Vector3 feet, int lootSeed, float yaw = 0f)
        {
            ProceduralWorld world = ProceduralWorld.Active;
            if (world == null || world.assets == null || world.assets.raiderPrefab == null) return null;
            SpeciesSpec spec = SpeciesSpec.Of(species);
            GameObject go = Object.Instantiate(world.assets.raiderPrefab, parent, true);
            go.SetActive(false);
            go.name = spec.Name.Replace(" ", "");
            go.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
            EnemyAI enemy = go.GetComponent<EnemyAI>();
            enemy.SetBodySize(1f, spec.Radius, spec.Height);
            CharacterController body = go.GetComponent<CharacterController>();
            // SetBodySize puts the capsule's bottom 1 m under the origin; the controller then rests a skin width above the floor.
            go.transform.position = feet + Vector3.up * (1f + body.skinWidth);
            enemy.Configure(spec.Health, spec.Speed, spec.Damage, spec.AttackCooldown, spec.AttackWindup, spec.AttackRange);
            enemy.detectionRadius = spec.Detection;
            enemy.superArmor = spec.IsBoss;
            bool humanoidWithWeapon = spec.Body == BodyKind.Humanoid && species != SpeciesId.Rootwalker && species != SpeciesId.RootColossus;
            EnemyModels.Apply(go, spec.ModelId, humanoidWithWeapon);
            SpeciesActor actor = go.AddComponent<SpeciesActor>();
            actor.Species = species;
            actor.LootSeed = lootSeed;
            go.SetActive(true);
            IEnemyBrain brain = SpeciesBehaviours.Create(actor);
            if (brain != null) enemy.SetBrain(brain);
            return actor;
        }

        public static Vector3 GroundBelow(Vector3 center, Transform self)
        {
            foreach (RaycastHit hit in Physics.RaycastAll(center + Vector3.up * 0.2f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<CharacterController>() == null && !hit.transform.IsChildOf(self)) return hit.point + Vector3.up * 0.01f;
            return center - Vector3.up;
        }
    }
}
