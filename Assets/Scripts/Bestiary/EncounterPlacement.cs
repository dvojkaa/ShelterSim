using System.Collections.Generic;
using SheltreSim.Enemies;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// Shared pieces for world encounters (habitat groups, camps, boss lairs): where an actor can stand, its persistent slot,
    /// its loot seed, and plain primitive props. Everything here is deterministic: no random draws of its own.
    /// </summary>
    public static class EncounterPlacement
    {
        /// <summary>No world enemy is placed nearer the bunker than this, so home stays a safe base between raids.</summary>
        public const float SafeRadius = 200f;

        public static bool NearBunker(Vector3 p, float radius = SafeRadius)
        {
            float dx = p.x - WorldLayout.BunkerCenter.x, dz = p.z - WorldLayout.BunkerCenter.z;
            return dx * dx + dz * dz < radius * radius;
        }

        public static float BunkerDistance(Vector3 p) =>
            new Vector2(p.x - WorldLayout.BunkerCenter.x, p.z - WorldLayout.BunkerCenter.z).magnitude;

        /// <summary>Stable across runs and platforms (string.GetHashCode is not).</summary>
        public static int NameHash(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in text) h = (h ^ c) * 16777619u;
                return (int)(h & 0x7fffffff);
            }
        }

        /// <summary>One actor's loot seed: world seed + chunk + slot path. A revisit can't roll a different bag.</summary>
        public static int LootSeed(int seed, Vector2Int coordinate, string slotPath) =>
            WorldLayout.Hash(coordinate.x ^ NameHash(slotPath), coordinate.y, seed + 8803);

        /// <summary>Moving things and triggers: never part of the generated layout, so they can't change where anything is placed.</summary>
        public static bool IsDynamic(Collider c) =>
            c == null || c.isTrigger || c.attachedRigidbody != null || c is CharacterController ||
            c.GetComponentInParent<CharacterController>() != null || c.GetComponentInParent<WorldWildlife>() != null;

        /// <summary>
        /// Finds the floor at x/z (terrain, a deck, a road) and checks a capsule of this size fits there without touching
        /// anything solid. maxLift: how far above the terrain height the floor may be (a landmark deck is higher).
        /// </summary>
        public static bool TryStand(float x, float z, float radius, float height, int seed, out Vector3 feet, float maxLift = 0.8f)
        {
            float terrain = WorldLayout.HeightAt(x, z, seed);
            feet = new Vector3(x, terrain, z);
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, terrain + maxLift + height + 2f, z), Vector3.down,
                maxLift + height + 5f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (IsDynamic(hit.collider)) continue;
                if (hit.normal.y < 0.82f) return false;          // a roof edge, a rock, a slope too steep to stand
                if (hit.point.y > terrain + maxLift + 0.05f) return false; // on top of something
                feet = hit.point;
                found = true;
                break;
            }
            if (!found) return false;
            return Clear(feet, radius, height);
        }

        /// <summary>True when an upright capsule standing at feet touches nothing static (terrain excepted: it is the floor).</summary>
        public static bool Clear(Vector3 feet, float radius, float height)
        {
            float r = radius + 0.12f;
            Vector3 bottom = feet + Vector3.up * (r + 0.15f), top = feet + Vector3.up * Mathf.Max(r + 0.15f, height - r);
            foreach (Collider c in Physics.OverlapCapsule(bottom, top, r, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IsDynamic(c) || c.GetComponent<ProceduralTerrain>() != null) continue;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Spawns one actor in its own ChunkEncounterSlot named <paramref name="name"/>. The slot keeps Defeated and Health by
        /// that name, so the actor never respawns or heals. Returns the slot (with a null actor if the prefab is missing).
        /// </summary>
        public static ChunkEncounterSlot SpawnSlot(Transform parent, Transform chunk, string name, SpeciesId species, Vector3 feet,
            float yaw, Vector2Int coordinate, int seed, Vector3 guardPoint, float guardRadius)
        {
            var slotObject = new GameObject(name);
            slotObject.transform.SetParent(parent, false);
            slotObject.transform.position = feet;
            string path = chunk != null ? Path(chunk, slotObject.transform) : name;
            SpeciesActor actor = Bestiary.Spawn(species, slotObject.transform, feet, LootSeed(seed, coordinate, path), yaw);
            if (actor != null)
            {
                actor.GuardPoint = guardPoint;
                actor.GuardRadius = guardRadius;
            }
            var slot = slotObject.AddComponent<ChunkEncounterSlot>();
            slot.Initialize(actor != null ? actor.Enemy : null, seed);
            return slot;
        }

        private static string Path(Transform root, Transform target)
        {
            var parts = new List<string>();
            for (Transform t = target; t != null && t != root; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        // ---- plain primitive props ----

        public static GameObject Prop(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 size,
            Quaternion localRotation, Material material, bool solid, Color? tint = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            if (tint.HasValue) Tint(renderer, tint.Value);
            if (!solid)
            {
                Collider collider = go.GetComponent<Collider>();
                collider.enabled = false;
                Object.Destroy(collider);
            }
            return go;
        }

        public static void Tint(Renderer renderer, Color color)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>Hides generated scenery (trees, rocks) inside a cleared area. Deactivating draws nothing and keeps saved ids.</summary>
        public static void ClearScenery(Transform chunk, Vector3 center, float radius)
        {
            Transform content = chunk.Find("Content");
            if (content == null) return;
            foreach (Transform child in content)
            {
                Vector3 d = child.position - center;
                d.y = 0f;
                if (d.sqrMagnitude < radius * radius) child.gameObject.SetActive(false);
            }
        }

        public static void AddLoot(SheltreSim.Items.LootContainer loot, string id, int amount)
        {
            var def = SheltreSim.Crafting.ItemCatalog.Def(id);
            if (def != null && amount > 0) loot.entries.Add(new SheltreSim.Items.LootEntry { resource = def, amount = amount });
        }
    }
}
