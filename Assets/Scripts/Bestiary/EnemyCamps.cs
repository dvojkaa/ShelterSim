using System.Collections.Generic;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// Raider camps, ruined world only: about 1 in 12 open, dry, non-town chunks at least 350 m from the bunker, plus one
    /// guaranteed within 600 m (the nearest qualifying chunk). A ring of tents, a fire with smoke, sandbag walls, a flag
    /// pole, two crates, and a chest only the Shift Commander's defeat opens. Everything is placed from the chunk's own
    /// hash and random, so nothing else in the world moves.
    /// </summary>
    public static class EnemyCamps
    {
        public const string RootName = "EnemyCamp";
        public const float MinBunkerDistance = 350f, GuaranteedRange = 600f, Radius = 14f;
        /// <summary>Members keep to their own post in the camp; the brain's leash is this + 15 m.</summary>
        public const float MemberGuardRadius = 12f;
        public const string ChestMessage = "Locked. The Shift Commander has the key.";
        static readonly Dictionary<int, Vector2Int?> guaranteed = new Dictionary<int, Vector2Int?>();
        static readonly string[] Weapons = { "smg", "hunting_rifle", "shotgun" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => guaranteed.Clear();

        static int CampHash(Vector2Int c, int seed) => WorldLayout.Hash(c.x, c.y, seed + 6151);

        /// <summary>Camp centre on the ground plane (y = 0), from the seed alone.</summary>
        public static Vector3 Center(Vector2Int c, int seed)
        {
            int h = CampHash(c, seed);
            return new Vector3(c.x * 128f + 64f + (h % 17 - 8), 0f, c.y * 128f + 64f + ((h / 17) % 17 - 8));
        }

        static float Yaw(Vector2Int c, int seed) => (CampHash(c, seed) / 289) % 360;

        /// <summary>Could hold a camp: open dry ground away from towns, landmarks and the bunker. Seed only.</summary>
        public static bool Suitable(Vector2Int c, int seed)
        {
            if (WorldLayout.IsTownChunk(c, seed) || WorldLayout.IsSuburbChunk(c, seed) || ProceduralLandmarks.HasLandmark(c, seed)) return false;
            Vector3 center = Center(c, seed);
            if (EncounterPlacement.BunkerDistance(center) < MinBunkerDistance) return false;
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            for (int i = 0; i <= 8; i++)
            {
                float a = i * Mathf.PI * .25f, r = i == 8 ? 0f : Radius + 4f;
                float x = center.x + Mathf.Cos(a) * r, z = center.z + Mathf.Sin(a) * r;
                if (WorldLayout.IsWater(x, z, seed, 4f) || WorldLayout.IsHomePlot(x, z) || WorldLayout.IsRoad(x, z, 2f) ||
                    WorldLayout.IsConnectorRoad(x, z, seed, 2f)) return false;
                float y = WorldLayout.HeightAt(x, z, seed);
                min = Mathf.Min(min, y); max = Mathf.Max(max, y);
            }
            return max - min < 5f;
        }

        public static bool IsCamp(Vector2Int c, int seed) =>
            Suitable(c, seed) && (CampHash(c, seed) % 12 == 0 || Guaranteed(seed) == c);

        /// <summary>The camp every world has within 600 m of the bunker: the nearest suitable chunk. Null if none (never seen).</summary>
        public static Vector2Int? Guaranteed(int seed)
        {
            if (guaranteed.TryGetValue(seed, out Vector2Int? cached)) return cached;
            Vector2Int home = WorldLayout.Coordinate(WorldLayout.BunkerCenter);
            Vector2Int? best = null;
            float bestDistance = float.PositiveInfinity;
            int rings = Mathf.CeilToInt(GuaranteedRange / 128f) + 1;
            for (int dz = -rings; dz <= rings; dz++)
                for (int dx = -rings; dx <= rings; dx++)
                {
                    var c = new Vector2Int(home.x + dx, home.y + dz);
                    float d = EncounterPlacement.BunkerDistance(Center(c, seed));
                    // Ties broken by coordinate so the answer never depends on loop order.
                    if (d > GuaranteedRange || d > bestDistance + 0.001f) continue;
                    if (Mathf.Abs(d - bestDistance) <= 0.001f && best.HasValue && (c.x > best.Value.x || (c.x == best.Value.x && c.y > best.Value.y))) continue;
                    if (!Suitable(c, seed)) continue;
                    best = c; bestDistance = d;
                }
            guaranteed[seed] = best;
            return best;
        }

        public static void Build(Transform chunk, Vector2Int c, int seed, ProceduralWorldAssets assets)
        {
            Vector3 center = Center(c, seed);
            center.y = WorldLayout.HeightAt(center.x, center.z, seed);
            EncounterPlacement.ClearScenery(chunk, center, Radius + 3f);
            var root = new GameObject(RootName).transform;
            root.SetParent(chunk, false);
            root.SetPositionAndRotation(center, Quaternion.Euler(0f, Yaw(c, seed), 0f));
            var random = new System.Random(WorldLayout.Hash(c.x, c.y, seed + 6173));
            Material wall = assets.structureMaterial, dark = assets.roadMaterial;

            int tents = 3 + random.Next(2);
            float tentOffset = (float)random.NextDouble() * 30f;
            for (int i = 0; i < tents; i++) Tent(root, "Tent_" + i, tentOffset + i * 360f / tents, 9.5f, wall, seed);
            for (int i = 0; i < 8; i++)
            {
                if (i == 0 || i == 4) continue; // two entrances
                float a = i * 45f * Mathf.Deg2Rad;
                Vector3 local = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Radius;
                Grounded(root, "Sandbags_" + i, PrimitiveType.Cube, local, 0.38f, new Vector3(7.2f, 1.1f, .9f),
                    Quaternion.Euler(0f, i * 45f, 0f), wall, true, new Color(.55f, .5f, .38f), seed);
            }
            for (int side = -1; side <= 1; side += 2)
                Grounded(root, "Barricade_" + (side < 0 ? "S" : "N"), PrimitiveType.Cube, new Vector3(2.6f, 0f, side * (Radius + 1.2f)), .5f,
                    new Vector3(2.4f, 1.0f, .25f), Quaternion.Euler(0f, 0f, 12f), dark, true, null, seed);
            Fire(root, new Vector3(0f, 0f, -4f), seed);
            FlagPole(root, new Vector3(2.6f, 0f, 2.6f), wall, dark, seed);
            for (int i = 0; i < 2; i++)
            {
                var crate = CrateAt(root, "Crate_" + i, new Vector3(i == 0 ? -5.5f : 5.5f, 0f, 1f), assets, seed);
                var loot = crate.AddComponent<LootContainer>();
                SheltreSim.Crafting.LootTables.Fill(loot, i == 0 ? "military" : "town", random);
                loot.hasLoot = loot.entries.Count > 0;
            }
            int weapon = random.Next(Weapons.Length);
            int members = 4 + random.Next(3);
            var mix = new SpeciesId[members];
            for (int i = 0; i < members; i++)
            {
                double roll = random.NextDouble();
                mix[i] = i == 0 ? SpeciesId.Scavenger : i == 1 ? SpeciesId.Wanderer :
                    roll < .3 ? SpeciesId.Scavenger : roll < .5 ? SpeciesId.Wanderer : roll < .7 ? SpeciesId.Plated : roll < .85 ? SpeciesId.Breacher : SpeciesId.Screamer;
            }
            float memberOffset = (float)random.NextDouble() * 360f;

            Physics.SyncTransforms();
            // The boss and its chest, in the middle.
            GameObject chestObject = ChestAt(root, "BossChest", Vector3.zero, wall, dark, seed);
            Physics.SyncTransforms();
            Vector3 bossLocal = new Vector3(0f, 0f, 2.4f);
            Vector3 bossFeet = Stand(root, bossLocal, SpeciesSpec.Of(SpeciesId.ShiftCommander), seed, null);
            ChunkEncounterSlot boss = EncounterPlacement.SpawnSlot(root, chunk, "Boss", SpeciesId.ShiftCommander, bossFeet,
                root.eulerAngles.y + 180f, c, seed, bossFeet, 8f);
            var chest = chestObject.AddComponent<EncounterChest>();
            chest.guard = boss;
            chest.title = "Commander's chest";
            chest.lockedMessage = ChestMessage;
            EncounterPlacement.AddLoot(chest, "ammo_rifle", 30);
            EncounterPlacement.AddLoot(chest, "energy_cell", 3);
            EncounterPlacement.AddLoot(chest, "steel_bar", 3);
            EncounterPlacement.AddLoot(chest, "circuit_board", 2);
            EncounterPlacement.AddLoot(chest, Weapons[weapon], 1);
            chest.hasLoot = chest.entries.Count > 0;

            var camp = root.gameObject.AddComponent<EnemyCamp>();
            camp.boss = boss;
            var taken = new List<Vector3> { bossFeet };
            for (int i = 0; i < members; i++)
            {
                SpeciesSpec spec = SpeciesSpec.Of(mix[i]);
                float a = (memberOffset + i * 360f / members) * Mathf.Deg2Rad;
                Vector3 local = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 6.5f;
                Vector3? feet = TryStand(root, local, spec, seed, taken);
                if (!feet.HasValue) continue;
                taken.Add(feet.Value);
                camp.members.Add(EncounterPlacement.SpawnSlot(root, chunk, "Member_" + i, mix[i], feet.Value,
                    Quaternion.LookRotation(center - feet.Value).eulerAngles.y + 180f, c, seed, feet.Value, MemberGuardRadius));
            }
            var site = root.gameObject.AddComponent<EncounterSite>();
            site.siteId = EncounterAtlas.CampId(c, seed);
            site.title = "Raider camp";
        }

        // Tries the spot and a few nearby ones, inside the walls.
        static Vector3? TryStand(Transform root, Vector3 local, SpeciesSpec spec, int seed, List<Vector3> taken)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float a = attempt * 60f * Mathf.Deg2Rad, r = attempt == 0 ? 0f : 1.6f;
                Vector3 p = root.TransformPoint(local + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r));
                if (!EncounterPlacement.TryStand(p.x, p.z, spec.Radius, spec.Height, seed, out Vector3 feet)) continue;
                bool close = false;
                foreach (Vector3 t in taken) if ((t - feet).sqrMagnitude < 2.4f * 2.4f) close = true;
                if (!close) return feet;
            }
            return null;
        }

        static Vector3 Stand(Transform root, Vector3 local, SpeciesSpec spec, int seed, List<Vector3> taken)
        {
            Vector3? feet = TryStand(root, local, spec, seed, taken ?? new List<Vector3>());
            if (feet.HasValue) return feet.Value;
            Vector3 p = root.TransformPoint(local);
            p.y = WorldLayout.HeightAt(p.x, p.z, seed);
            return p;
        }

        // ---- props: each piece sits on the terrain under it ----

        static GameObject Grounded(Transform root, string name, PrimitiveType type, Vector3 local, float lift, Vector3 size,
            Quaternion rotation, Material material, bool solid, Color? tint, int seed)
        {
            Vector3 world = root.TransformPoint(local);
            float ground = WorldLayout.HeightAt(world.x, world.z, seed);
            var go = EncounterPlacement.Prop(root, name, type, Vector3.zero, size, rotation, material, solid, tint);
            go.transform.position = new Vector3(world.x, ground + lift, world.z);
            return go;
        }

        static void Tent(Transform root, string name, float angle, float radius, Material canvas, int seed)
        {
            float a = angle * Mathf.Deg2Rad;
            Vector3 local = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
            Vector3 world = root.TransformPoint(local);
            var tent = new GameObject(name).transform;
            tent.SetParent(root, false);
            tent.position = new Vector3(world.x, WorldLayout.HeightAt(world.x, world.z, seed), world.z);
            tent.localRotation = Quaternion.Euler(0f, angle, 0f); // long axis points at the fire
            Color cloth = new Color(.42f, .45f, .3f);
            float half = 1.35f, height = 2.0f, slant = Mathf.Atan2(half, height) * Mathf.Rad2Deg;
            float panel = Mathf.Sqrt(half * half + height * height);
            for (int side = -1; side <= 1; side += 2)
                EncounterPlacement.Prop(tent, "Panel", PrimitiveType.Cube, new Vector3(side * half * .5f, height * .5f, 0f),
                    new Vector3(.08f, panel, 3.6f), Quaternion.Euler(0f, 0f, side * slant), canvas, true, cloth);
            EncounterPlacement.Prop(tent, "BackFlap", PrimitiveType.Cube, new Vector3(0f, height * .4f, 1.75f),
                new Vector3(half * 1.4f, height * .8f, .06f), Quaternion.identity, canvas, true, cloth * .85f);
            EncounterPlacement.Prop(tent, "Ridge", PrimitiveType.Cylinder, new Vector3(0f, height, 0f),
                new Vector3(.08f, 1.9f, .08f), Quaternion.Euler(90f, 0f, 0f), canvas, false, new Color(.3f, .22f, .15f));
            EncounterPlacement.Prop(tent, "Bedroll", PrimitiveType.Cube, new Vector3(0f, .06f, .2f),
                new Vector3(.8f, .12f, 2f), Quaternion.identity, canvas, false, new Color(.35f, .2f, .15f));
        }

        static void Fire(Transform root, Vector3 local, int seed)
        {
            Vector3 world = root.TransformPoint(local);
            world.y = WorldLayout.HeightAt(world.x, world.z, seed);
            var fire = new GameObject("Campfire").transform;
            fire.SetParent(root, false);
            fire.position = world;
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f;
                EncounterPlacement.Prop(fire, "Stone", PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * .75f, .08f, Mathf.Sin(a) * .75f),
                    new Vector3(.35f, .25f, .3f), Quaternion.identity, null, false, new Color(.35f, .34f, .33f));
            }
            for (int i = 0; i < 3; i++)
                EncounterPlacement.Prop(fire, "Log", PrimitiveType.Cylinder, new Vector3(0f, .15f, 0f), new Vector3(.14f, .5f, .14f),
                    Quaternion.Euler(70f, i * 60f, 0f), null, false, new Color(.25f, .15f, .08f));
            var lightObject = new GameObject("FireLight");
            lightObject.transform.SetParent(fire, false);
            lightObject.transform.localPosition = Vector3.up * 1.1f;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .58f, .25f);
            light.range = 14f;
            light.intensity = 2.2f;
            light.shadows = LightShadows.None;
            lightObject.AddComponent<CampfireFlicker>();
            WorldEffects.SmokePlume(fire, world + Vector3.up * .2f);
        }

        static void FlagPole(Transform root, Vector3 local, Material wall, Material dark, int seed)
        {
            var pole = Grounded(root, "FlagPole", PrimitiveType.Cylinder, local, 5f, new Vector3(.18f, 5f, .18f), Quaternion.identity, dark, true, null, seed);
            Vector3 top = pole.transform.position + Vector3.up * 4.1f;
            var flag = EncounterPlacement.Prop(root, "Flag", PrimitiveType.Cube, Vector3.zero, new Vector3(2.4f, 1.4f, .05f), Quaternion.identity,
                wall, false, new Color(.55f, .08f, .06f));
            flag.transform.position = top + root.right * 1.25f;
        }

        static GameObject CrateAt(Transform root, string name, Vector3 local, ProceduralWorldAssets assets, int seed)
        {
            Vector3 world = root.TransformPoint(local);
            var crate = new GameObject(name);
            crate.transform.SetParent(root, false);
            crate.transform.position = new Vector3(world.x, WorldLayout.HeightAt(world.x, world.z, seed), world.z);
            if (assets.lootModel != null)
            {
                var visual = Object.Instantiate(assets.lootModel, crate.transform, false);
                visual.name = "Visual";
                visual.transform.localScale = Vector3.one * 2f;
                if (assets.atlasMaterial != null) foreach (var r in visual.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = assets.atlasMaterial;
            }
            else EncounterPlacement.Prop(crate.transform, "Visual", PrimitiveType.Cube, Vector3.up * .4f, Vector3.one * .8f, Quaternion.identity, assets.structureMaterial, false);
            var box = crate.AddComponent<BoxCollider>();
            box.size = Vector3.one * .8f;
            box.center = Vector3.up * .4f;
            return crate;
        }

        /// <summary>A banded strongbox. Shared by camps and the military lair.</summary>
        public static GameObject ChestAt(Transform root, string name, Vector3 local, Material wall, Material dark, int seed)
        {
            Vector3 world = root.TransformPoint(local);
            var chest = new GameObject(name);
            chest.transform.SetParent(root, false);
            chest.transform.position = new Vector3(world.x, WorldLayout.HeightAt(world.x, world.z, seed), world.z);
            chest.transform.localRotation = Quaternion.identity;
            EncounterPlacement.Prop(chest.transform, "Case", PrimitiveType.Cube, Vector3.up * .4f, new Vector3(1.3f, .8f, .85f), Quaternion.identity, wall, false, new Color(.33f, .36f, .28f));
            EncounterPlacement.Prop(chest.transform, "Lid", PrimitiveType.Cube, Vector3.up * .86f, new Vector3(1.38f, .14f, .92f), Quaternion.identity, dark, false);
            for (int i = -1; i <= 1; i += 2)
                EncounterPlacement.Prop(chest.transform, "Band", PrimitiveType.Cube, new Vector3(i * .42f, .45f, 0f), new Vector3(.08f, .86f, .9f), Quaternion.identity, dark, false);
            EncounterPlacement.Prop(chest.transform, "Padlock", PrimitiveType.Cube, new Vector3(0f, .62f, -.46f), new Vector3(.16f, .2f, .06f), Quaternion.identity, null, false, new Color(.8f, .65f, .2f));
            var box = chest.AddComponent<BoxCollider>();
            box.size = new Vector3(1.38f, .95f, .92f);
            box.center = Vector3.up * .47f;
            return chest;
        }
    }

    /// <summary>The camp as one group: hurting any member wakes them all, boss included.</summary>
    public sealed class EnemyCamp : MonoBehaviour
    {
        public const float AlertRadius = 30f;
        public ChunkEncounterSlot boss;
        public readonly List<ChunkEncounterSlot> members = new List<ChunkEncounterSlot>();
        public bool Alerted { get; private set; }
        private readonly Dictionary<ChunkEncounterSlot, float> lastHealth = new Dictionary<ChunkEncounterSlot, float>();
        private float nextAlert;

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            bool hurt = false;
            Check(boss, ref hurt);
            foreach (var m in members) Check(m, ref hurt);
            if (hurt && Time.time >= nextAlert)
            {
                nextAlert = Time.time + 3f;
                if (!Alerted) SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.RaiderAlert, transform.position, 0.9f, 60f);
                Alerted = true;
                SpeciesBehaviours.Alert(transform.position, AlertRadius);
            }
        }

        private void Check(ChunkEncounterSlot slot, ref bool hurt)
        {
            if (slot == null) return;
            float health = slot.Defeated ? 0f : slot.Actor != null ? slot.Actor.health : -1f;
            if (health < 0f) return;
            // A slot restored as damaged or defeated starts at its saved value: only a new hit counts.
            if (lastHealth.TryGetValue(slot, out float last) && health < last - 0.001f) hurt = true;
            lastHealth[slot] = health;
        }
    }

    public sealed class CampfireFlicker : MonoBehaviour
    {
        private Light fireLight;
        private float baseIntensity, phase;
        private void Awake() { fireLight = GetComponent<Light>(); baseIntensity = fireLight.intensity; phase = transform.position.x * .37f; }
        private void Update() => fireLight.intensity = baseIntensity * (0.8f + 0.35f * Mathf.PerlinNoise(Time.time * 6f + phase, phase));
    }
}
