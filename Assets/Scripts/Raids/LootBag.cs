using System.Collections.Generic;
using System.Linq;
using System.Text;
using SheltreSim.Audio;
using SheltreSim.Crafting;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Raids
{
    /// <summary>
    /// A dead enemy's belongings on the ground. Contents are decided once, at death, and never re-rolled. It's created at death
    /// and on restoring a saved bag, never by a chunk or an enemy coming back. Created through LootBagSystem.
    /// The collider is lower than the step height, so nobody trips on it, and solid, because the interact ray ignores triggers.
    /// </summary>
    public sealed class LootBag : MonoBehaviour, IInteractable
    {
        public int Id { get; private set; }
        public LootTier Tier { get; private set; }
        public IReadOnlyList<KeyValuePair<string, int>> Contents => contents;

        private List<KeyValuePair<string, int>> contents = new List<KeyValuePair<string, int>>();
        private bool taken;

        public static LootBag Create(Transform parent, Vector3 position, int id, LootTier tier, IEnumerable<KeyValuePair<string, int>> items)
        {
            var root = new GameObject(tier == LootTier.Warlord ? "WarlordCache" : "LootBag");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var box = root.AddComponent<BoxCollider>();
            box.center = Vector3.up * 0.14f;
            box.size = new Vector3(0.9f, 0.28f, 0.9f);

            bool boss = tier == LootTier.Warlord;
            Color color = boss ? new Color(1f, 0.8f, 0.2f) : tier == LootTier.Elite ? new Color(0.45f, 0.62f, 0.95f) : new Color(0.5f, 0.38f, 0.22f);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(mesh.GetComponent<Collider>());
            mesh.name = "Visual";
            mesh.transform.SetParent(root.transform, false);
            float size = boss ? 0.8f : 0.55f;
            mesh.transform.localPosition = Vector3.up * size * 0.3f;
            mesh.transform.localScale = new Vector3(size, size * 0.6f, size);
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            mesh.GetComponent<Renderer>().SetPropertyBlock(block);

            LootBag bag = root.AddComponent<LootBag>();
            bag.Id = id;
            bag.Tier = tier;
            bag.contents = items.Where(p => p.Value > 0).ToList();
            return bag;
        }

        public string InteractPrompt => Tier == LootTier.Warlord ? "Press E to take the Warlord's cache" : "Press E to take the bag";

        public void Interact(GameObject interactor)
        {
            if (Time.timeScale <= 0f || taken || interactor == null) return;
            Inventory inventory = interactor.GetComponentInParent<Inventory>();
            if (inventory == null) return;
            taken = true;
            var found = new StringBuilder();
            foreach (KeyValuePair<string, int> pair in contents)
            {
                inventory.Add(pair.Key, pair.Value);
                if (found.Length > 0) found.Append(", ");
                found.Append(pair.Value).Append(' ').Append(ItemCatalog.NameOf(pair.Key));
            }
            GameAudio.PlayAt(Sfx.Rummage, transform.position, 0.8f, 20f);
            GameAudio.Play2D(Sfx.LootFound, 0.55f, 0f, 0.3f);
            GameMessageBus.Post($"Found {found}.");
            LootBagSystem.Remove(this);
        }
    }

    /// <summary>
    /// Seeded death-loot tables and the live bags. Owned by RaidDirector, which saves them. A roll depends on the world seed and a saved
    /// counter, so it is reproducible, and a bag's contents are stored rather than rolled again.
    /// </summary>
    public static class LootBagSystem
    {
        public const int MaxBags = 100;

        private static readonly List<LootBag> bags = new List<LootBag>();
        private static Transform root;
        private static int nextId = 1;

        public static IReadOnlyList<LootBag> Bags => bags;
        public static int NextId { get => nextId; set => nextId = Mathf.Max(1, value); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            bags.Clear();
            root = null;
            nextId = 1;
        }

        public static void SetRoot(Transform value) => root = value;

        /// <summary>A real death. Does nothing the second time for the same enemy.</summary>
        public static LootBag DropFor(EnemyAIHandle enemy, int worldSeed)
        {
            if (!enemy.Enemy.TryClaimDeathLoot()) return null;
            int id = nextId++;
            var items = Roll(enemy.Tier, worldSeed, id);
            if (enemy.Agent != null)
                foreach (KeyValuePair<string, int> pair in enemy.Agent.Stolen) Merge(items, pair.Key, pair.Value);
            return Spawn(GroundUnder(enemy.Enemy.CenterPosition, enemy.Enemy.transform), id, enemy.Tier, items);
        }

        public static LootBag Spawn(Vector3 position, int id, LootTier tier, IEnumerable<KeyValuePair<string, int>> items)
        {
            if (bags.Count >= MaxBags)
            {
                Debug.LogWarning($"[LootBagSystem] {bags.Count} bags on the ground, removing the oldest.");
                LootBag oldest = bags[0];
                bags.RemoveAt(0);
                Discard(oldest);
            }
            LootBag bag = LootBag.Create(root, position, id, tier, items);
            bags.Add(bag);
            return bag;
        }

        public static void Remove(LootBag bag)
        {
            bags.Remove(bag);
            Discard(bag);
        }

        public static void Clear()
        {
            foreach (LootBag bag in bags) Discard(bag);
            bags.Clear();
        }

        private static void Discard(LootBag bag)
        {
            if (bag == null) return;
            // Inactive at once: a same-frame raycast or find must not see a bag that has been taken.
            bag.gameObject.SetActive(false);
            Object.Destroy(bag.gameObject);
        }

        public static List<KeyValuePair<string, int>> Roll(LootTier tier, int worldSeed, int id)
        {
            var random = new System.Random(unchecked(worldSeed * 397 ^ id * 7919 ^ 0x5EED));
            var items = new List<KeyValuePair<string, int>>();
            switch (tier)
            {
                case LootTier.Warlord:
                    Merge(items, "expedition_parts", 3);
                    Merge(items, "energy_cell", 4);
                    Merge(items, "circuit_board", 3);
                    Merge(items, "ammo_rifle", 30);
                    break;
                case LootTier.Elite:
                    // A guaranteed rare component, a little ammunition, and a chance of a second part.
                    Merge(items, Pick(random, "electronics", "gunpowder", "capacitor"), 1 + random.Next(2));
                    Merge(items, "ammo_rifle", 6 + random.Next(10));
                    Merge(items, "scrap_metal", 2 + random.Next(3));
                    if (random.Next(100) < 35) Merge(items, Pick(random, "copper_wire", "metal_plate", "circuit_board"), 1);
                    break;
                default:
                    // The base is guaranteed. Chance only decides the bonus, never whether a kill pays at all.
                    Merge(items, "scrap_metal", 1 + random.Next(3));
                    Merge(items, Pick(random, "cloth", "nails"), 1 + random.Next(3));
                    if (random.Next(100) < 45) Merge(items, "ammo_rifle", 2 + random.Next(6));
                    if (random.Next(100) < 25) Merge(items, Pick(random, "canned_food", "dried_meat", "bandage"), 1);
                    break;
            }
            items.RemoveAll(p => !ItemCatalog.Exists(p.Key));
            return items;
        }

        private static string Pick(System.Random random, params string[] ids) => ids[random.Next(ids.Length)];

        private static void Merge(List<KeyValuePair<string, int>> items, string id, int amount)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Key != id) continue;
                items[i] = new KeyValuePair<string, int>(id, items[i].Value + amount);
                return;
            }
            items.Add(new KeyValuePair<string, int>(id, amount));
        }

        /// <summary>The floor under the body. Characters and bags don't count as a floor.</summary>
        private static Vector3 GroundUnder(Vector3 center, Transform self)
        {
            Vector3 result = center - Vector3.up * 1f;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(center + Vector3.up * 0.2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= nearest) continue;
                if (hit.collider.GetComponentInParent<CharacterController>() != null || hit.collider.GetComponentInParent<LootBag>() != null) continue;
                nearest = hit.distance;
                result = hit.point;
            }
            return result + Vector3.up * 0.01f;
        }
    }

    /// <summary>What LootBagSystem needs to know about a dead enemy.</summary>
    public readonly struct EnemyAIHandle
    {
        public readonly SheltreSim.Enemies.EnemyAI Enemy;
        public readonly RaidAgent Agent;
        public LootTier Tier => Agent != null ? Agent.Spec.Loot : LootTier.Ordinary;

        public EnemyAIHandle(SheltreSim.Enemies.EnemyAI enemy)
        {
            Enemy = enemy;
            Agent = enemy != null ? enemy.GetComponent<RaidAgent>() : null;
        }
    }
}
