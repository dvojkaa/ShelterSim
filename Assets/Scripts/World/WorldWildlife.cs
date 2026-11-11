using System;
using System.Collections.Generic;
using SheltreSim.Player;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Lightweight, non-hostile chunk residents. No loot, damage or global random state.</summary>
    public sealed class WorldWildlife : MonoBehaviour
    {
        private static readonly HashSet<WorldWildlife> live = new HashSet<WorldWildlife>();
        public static IEnumerable<WorldWildlife> Live => live;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => live.Clear();
        private Vector2Int coordinate;
        private int seed;
        private int species;
        public string SpeciesName => species == 0 ? "Deer" : species == 1 ? "Boar" : "Bird";
        private System.Random random;
        private Transform player;
        private Transform[] legs;
        private Transform[] wings;
        private Quaternion[] limbRestRotations;
        private readonly RaycastHit[] movementHits = new RaycastHit[24];
        private Vector3 destination;
        private float decisionTimer, animationTime;
        private float birdAltitude;

        // ---- M21: huntable deer and boar ----
        private float maxHealth, health, panicTimer, chargeTimer, attackTimer;
        public bool IsDead { get; private set; }
        public float Health => health;
        public bool IsHuntable => species != 2;
        private SheltreSim.Survival.SurvivalStats playerStats;

        private void OnEnable() => live.Add(this);
        private void OnDisable() => live.Remove(this);

        public static void Build(Transform parent, Vector2Int coordinate, int seed, ProceduralWorldAssets assets)
        {
            if (parent == null || assets == null || WorldLayout.IsTownChunk(coordinate, seed)) return;
            var random = new System.Random(WorldLayout.Hash(coordinate.x, coordinate.y, seed + 719));
            int count = 1 + random.Next(3);
            for (int i = 0; i < count; i++)
            {
                float x = coordinate.x * (float)WorldLayout.ChunkSize + 18f + (float)random.NextDouble() * 92f;
                float z = coordinate.y * (float)WorldLayout.ChunkSize + 18f + (float)random.NextDouble() * 92f;
                if (WorldLayout.IsHomePlot(x, z) || WorldLayout.IsRoad(x, z, 7f)) continue;
                int biome = WorldLayout.BiomeIndexAt(x, z, seed);
                int species = biome == 2 || biome == 4 ? 2 : random.Next(3);
                var go = new GameObject(species == 0 ? "WildDeer" : species == 1 ? "WildBoar" : "WildBird");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(x, WorldLayout.HeightAt(x, z, seed), z);
                var actor = go.AddComponent<WorldWildlife>();
                actor.coordinate = coordinate;
                actor.seed = seed;
                actor.species = species;
                actor.random = new System.Random(WorldLayout.Hash(coordinate.x, coordinate.y, seed + 211 + i));
                actor.birdAltitude = 5f + (float)actor.random.NextDouble() * 4f;
                if (species == 2) go.transform.position += Vector3.up * actor.birdAltitude;
                actor.decisionTimer = (float)actor.random.NextDouble() * 3f;
                actor.destination = go.transform.position;
                actor.BuildBody(ChooseMaterial(assets, biome), ModelFor(assets, species));
                actor.MakeHuntable();
            }
        }

        private static GameObject ModelFor(ProceduralWorldAssets assets, int species)
        {
            if (assets == null) return null;
            return species == 0 ? assets.wildlifeDeer : species == 1 ? assets.wildlifeBoar : assets.wildlifeCrow;
        }

        private static Material ChooseMaterial(ProceduralWorldAssets assets, int biome)
        {
            if (assets.biomeMaterials != null && assets.biomeMaterials.Length > biome && assets.biomeMaterials[biome] != null)
                return assets.biomeMaterials[biome];
            return assets.structureMaterial != null ? assets.structureMaterial : assets.atlasMaterial;
        }

        private void Start()
        {
            var controller = FindFirstObjectByType<FirstPersonController>();
            if (controller != null) player = controller.transform;
            if (controller != null) playerStats = controller.GetComponent<SheltreSim.Survival.SurvivalStats>();
        }

        /// <summary>Adds a vertical, body-sized solid collider and health to deer and boar. Birds stay nonblocking.</summary>
        public void MakeHuntable()
        {
            if (species == 2 || GetComponent<Collider>() != null) return;
            maxHealth = health = species == 0 ? 60f : 80f;
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var capsule = gameObject.AddComponent<CapsuleCollider>();
            // CapsuleCollider.direction is an axis index: 2 means Z, which made the old
            // "animal" capsule lie along its nose and only block the player's shins.
            capsule.direction = 1;
            capsule.radius = species == 0 ? .38f : .36f;
            capsule.height = species == 0 ? 1.9f : 1.2f;
            capsule.center = new Vector3(0f, species == 0 ? .98f : .62f, species == 0 ? .02f : .04f);
        }

        /// <summary>Test hook: puts the animal in a known place and state.</summary>
        public static WorldWildlife SpawnForTest(Transform parent, Vector3 position, int speciesIndex)
        {
            var go = new GameObject(speciesIndex == 0 ? "WildDeer" : speciesIndex == 1 ? "WildBoar" : "WildBird");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var actor = go.AddComponent<WorldWildlife>();
            actor.species = speciesIndex;
            actor.random = new System.Random(12345);
            actor.destination = position;
            actor.BuildBody(null);
            actor.MakeHuntable();
            return actor;
        }

        /// <summary>Applies melee damage and returns the player-facing message. A dead animal leaves a searchable carcass.</summary>
        public string TakeHit(float damage, Vector3 attackerPosition)
        {
            string name = SpeciesName.ToLowerInvariant();
            if (IsDead || species == 2 || damage <= 0f || float.IsNaN(damage)) return $"The {name} is already dead.";
            health -= damage;
            if (health <= 0f)
            {
                Die();
                return $"Killed {name}. Search the carcass for meat.";
            }
            panicTimer = 6f;
            if (species == 1)
            {
                chargeTimer = 8f;
                SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.BoarGrunt, transform.position, 0.8f, 25f);
                return $"Hit {name} for {damage:0} damage ({health:0} HP left). The boar charges!";
            }
            return $"Hit {name} for {damage:0} damage ({health:0} HP left).";
        }

        private void Die()
        {
            IsDead = true;
            var carcass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            carcass.name = "Carcass_" + SpeciesName;
            carcass.transform.SetParent(transform.parent, false);
            carcass.transform.position = transform.position + Vector3.up * .2f;
            carcass.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            carcass.transform.localScale = species == 0 ? new Vector3(.7f, .4f, 1.3f) : new Vector3(.8f, .4f, 1.0f);
            var sourceRenderer = GetComponentInChildren<Renderer>();
            if (sourceRenderer != null) carcass.GetComponent<Renderer>().sharedMaterial = sourceRenderer.sharedMaterial;
            var loot = carcass.AddComponent<SheltreSim.Items.LootContainer>();
            loot.entries = new System.Collections.Generic.List<SheltreSim.Items.LootEntry>();
            var meat = SheltreSim.Crafting.ItemCatalog.Def("raw_meat");
            var leather = SheltreSim.Crafting.ItemCatalog.Def("leather");
            if (meat != null) loot.entries.Add(new SheltreSim.Items.LootEntry { resource = meat, amount = species == 0 ? 3 : 5 });
            if (leather != null) loot.entries.Add(new SheltreSim.Items.LootEntry { resource = leather, amount = species == 0 ? 2 : 1 });
            loot.hasLoot = loot.entries.Count > 0;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void BuildBody(Material material, GameObject model = null)
        {
            if (model != null)
            {
                Transform visual = Instantiate(model, transform, false).transform;
                visual.name = "Visual_" + SpeciesName;
                // The crow mesh is authored around its body centre; its feet sit 0.15 m below that origin.
                if (species == 2) visual.localPosition = Vector3.up * 0.15f;
                if (material != null)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_Color", material.color);
                    block.SetColor("_BaseColor", material.color);
                    foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true)) renderer.SetPropertyBlock(block);
                }

                if (species == 2)
                {
                    wings = new[] { FindPart(visual, "Wing_L"), FindPart(visual, "Wing_R") };
                    limbRestRotations = RestRotations(wings);
                }
                else
                {
                    // C3 models use semantic fore/hind leg names (the older cube rig used Leg_FL etc.).
                    legs = new[] { FindPart(visual, "Foreleg_L"), FindPart(visual, "Foreleg_R"),
                        FindPart(visual, "Hindleg_L"), FindPart(visual, "Hindleg_R") };
                    limbRestRotations = RestRotations(legs);
                }
                return;
            }

            if (species == 2)
            {
                Part("Body", new Vector3(0f, 0f, 0f), new Vector3(.22f, .20f, .48f), material);
                Part("Head", new Vector3(0f, .09f, .25f), new Vector3(.18f, .18f, .19f), material);
                Part("Beak", new Vector3(0f, .06f, .37f), new Vector3(.07f, .06f, .13f), material);
                wings = new Transform[2];
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1f : 1f;
                    var pivot = new GameObject("WingPivot").transform;
                    pivot.SetParent(transform, false);
                    pivot.localPosition = new Vector3(side * .09f, 0f, 0f);
                    Part("Wing", new Vector3(side * .25f, 0f, 0f), new Vector3(.54f, .045f, .26f), material, pivot);
                    wings[i] = pivot;
                }
                limbRestRotations = RestRotations(wings);
                return;
            }
            bool deer = species == 0;
            float bodyHeight = deer ? 1.05f : .58f;
            Part("Body", new Vector3(0f, bodyHeight, 0f), new Vector3(deer ? .55f : .7f, deer ? .58f : .52f, 1.15f), material);
            Part("Neck", new Vector3(0f, bodyHeight + (deer ? .35f : .04f), .48f),
                new Vector3(.32f, deer ? .7f : .36f, .35f), material);
            Part("Head", new Vector3(0f, bodyHeight + (deer ? .7f : .08f), .71f), new Vector3(.31f, .3f, .5f), material);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Part("Ear", new Vector3(side * .22f, bodyHeight + (deer ? .92f : .3f), .60f), new Vector3(.16f, .25f, .12f), material);
                if (deer)
                {
                    Part("Antler", new Vector3(side * .14f, bodyHeight + 1.1f, .65f), new Vector3(.06f, .5f, .06f), material);
                    Part("AntlerFork", new Vector3(side * .25f, bodyHeight + 1.23f, .65f), new Vector3(.28f, .055f, .06f), material);
                }
            }
            legs = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float side = (i & 1) == 0 ? -.21f : .21f;
                float end = i < 2 ? -.4f : .4f;
                var pivot = new GameObject("LegPivot").transform;
                pivot.SetParent(transform, false);
                pivot.localPosition = new Vector3(side, bodyHeight - .12f, end);
                float length = bodyHeight - .12f;
                Part("Leg", new Vector3(0f, -length * .5f, 0f), new Vector3(.13f, length, .13f), material, pivot);
                legs[i] = pivot;
            }
            limbRestRotations = RestRotations(legs);
            Part("Tail", new Vector3(0f, bodyHeight + .05f, -.67f), new Vector3(.12f, .13f, .3f), material);
        }

        private static Transform FindPart(Transform root, string partName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == partName) return child;
            return null;
        }

        private static Quaternion[] RestRotations(Transform[] parts)
        {
            var result = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++) result[i] = parts[i] != null ? parts[i].localRotation : Quaternion.identity;
            return result;
        }

        private void Part(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent != null ? parent : transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || random == null || IsDead) return;
            float dt = Mathf.Min(Time.deltaTime, .1f);
            panicTimer -= dt;
            attackTimer -= dt;
            Vector3 position = transform.position;
            Vector3 away = player != null ? Vector3.ProjectOnPlane(position - player.position, Vector3.up) : Vector3.one * 1000f;
            bool charging = species == 1 && chargeTimer > 0f && player != null;
            chargeTimer -= dt;
            bool fleeing = !charging && (away.sqrMagnitude < 18f * 18f || panicTimer > 0f);
            decisionTimer -= dt;
            if (charging)
            {
                destination = player.position;
                decisionTimer = .5f;
                if (away.sqrMagnitude < 1.9f * 1.9f && attackTimer <= 0f && playerStats != null)
                {
                    attackTimer = 1.3f;
                    float healthBefore = playerStats.health;
                    playerStats.TakeDamage(10f);
                    if (playerStats.health < healthBefore) DamageDirection.Report(transform.position);
                    SheltreSim.UI.GameMessageBus.Post("The boar gores you.");
                }
            }
            else if (fleeing)
            {
                if (away.sqrMagnitude < .01f) away = transform.forward;
                destination = ClampToChunk(position + away.normalized * 15f);
                decisionTimer = 1f;
            }
            else if (decisionTimer <= 0f)
            {
                decisionTimer = 3f + (float)random.NextDouble() * 6f;
                destination = ClampToChunk(position + new Vector3((float)random.NextDouble() * 24f - 12f, 0f,
                    (float)random.NextDouble() * 24f - 12f));
            }
            Vector3 offset = Vector3.ProjectOnPlane(destination - position, Vector3.up);
            float speed = charging ? 6.5f : fleeing ? (species == 2 ? 8f : 5.5f) : species == 2 ? 2f : .75f;
            bool moving = offset.sqrMagnitude > .3f;
            if (moving)
            {
                Vector3 proposed = ClampToChunk(position + offset.normalized * Mathf.Min(speed * dt, offset.magnitude));
                if (!WorldLayout.IsHomePlot(proposed.x, proposed.z) && !WorldLayout.IsRoad(proposed.x, proposed.z, 2f))
                {
                    Vector3 movement = proposed - position;
                    position += SweepBody(movement);
                    if ((proposed - position).sqrMagnitude > .0004f) decisionTimer = 0f;
                }
                else decisionTimer = 0f;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(offset), dt * 5f);
            }
            animationTime += dt * (fleeing ? 13f : species == 2 ? 8f : 4f);
            position.y = WorldLayout.HeightAt(position.x, position.z, seed) + (species == 2 ? birdAltitude + Mathf.Sin(animationTime * .3f) * .25f : 0f);
            transform.position = position;
            if (legs != null)
                for (int i = 0; i < legs.Length; i++)
                    if (legs[i] != null) legs[i].localRotation = limbRestRotations[i] * Quaternion.Euler(moving ? Mathf.Sin(animationTime + (i == 0 || i == 3 ? 0f : Mathf.PI)) * 24f : 0f, 0f, 0f);
            if (wings != null)
                for (int i = 0; i < wings.Length; i++)
                    if (wings[i] != null) wings[i].localRotation = limbRestRotations[i] * Quaternion.Euler(0f, 0f, Mathf.Sin(animationTime) * (i == 0 ? 32f : -32f));
        }

        /// <summary>Move huntable wildlife only as far as its solid capsule can travel without crossing a collider.</summary>
        private Vector3 SweepBody(Vector3 movement)
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            float distance = movement.magnitude;
            if (capsule == null || distance <= .0001f) return movement;

            Vector3 scale = transform.lossyScale;
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(capsule.height * Mathf.Abs(scale.y), radius * 2f);
            Vector3 center = transform.TransformPoint(capsule.center) + Vector3.up * .04f;
            Vector3 halfAxis = Vector3.up * Mathf.Max(0f, height * .5f - radius);
            Vector3 bottom = center - halfAxis;
            Vector3 top = center + halfAxis;
            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, movement / distance, movementHits,
                distance, ~0, QueryTriggerInteraction.Ignore);
            float allowedDistance = distance;
            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = movementHits[i].collider;
                if (hitCollider == null || hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform)) continue;
                allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, movementHits[i].distance - .035f));
            }
            Array.Clear(movementHits, 0, count);
            return movement / distance * allowedDistance;
        }

        private Vector3 ClampToChunk(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, coordinate.x * (float)WorldLayout.ChunkSize + 8f, (coordinate.x + 1f) * WorldLayout.ChunkSize - 8f);
            position.z = Mathf.Clamp(position.z, coordinate.y * (float)WorldLayout.ChunkSize + 8f, (coordinate.y + 1f) * WorldLayout.ChunkSize - 8f);
            return position;
        }
    }
}
