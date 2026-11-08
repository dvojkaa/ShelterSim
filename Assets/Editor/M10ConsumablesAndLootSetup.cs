using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SheltreSim.Items;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// M10 — creates the three new consumable resources (Water, First Aid Kit, a weapon
    /// upgrade) and populates every "LootSpawn" marker left by M10MapExpansionSetup's
    /// buildings/cars with a visible crate prop carrying a <see cref="LootContainer"/>.
    ///
    /// Must run after M10MapExpansionSetup (the markers it searches for don't exist until
    /// then) and before M8MenusSetup (which wires these three new resources, plus the
    /// Player's MeleeAttacker, into InventoryUI — those assets must already exist for that
    /// wiring to find them).
    ///
    /// The loot roll is seeded (System.Random, not UnityEngine.Random) so a re-run of this
    /// tool reproduces the same contents — consistent with every other "randomized" pass in
    /// this project (see M9WorldResourcesSetup's placement comment and M10MapExpansionSetup's
    /// building/car scatter): "random" here means rolled once, deterministically, at
    /// scene-construction time, not re-rolled at runtime.
    /// </summary>
    public static class M10ConsumablesAndLootSetup
    {
        private const int LootSeed = 20261108;

        // 85% of crates contain something — raised from the original 70% after the playtester
        // reported 10 crates yielding only 2 Water and nothing else. The remaining 15% is meant
        // to keep "might be empty" a real possibility (LootContainer's prompt deliberately never
        // reveals contents) without it being the dominant outcome — but note that with the
        // LootSeed below it does not currently happen at all: the shipped build rolls 38 of 38
        // crates non-empty (measured, not assumed). That is a legitimate consequence of a fixed
        // seed plus a changed draw sequence (the multi-roll rebalance below consumes a different
        // number of System.Random draws per crate than the single-roll version did, which shifts
        // every later crate's hasLoot draw), not a bug in this constant. It over-delivers against
        // the "too stingy" complaint rather than undermining it, so it is left alone; anyone who
        // wants visible empty crates back should change LootSeed and re-check the tool's own
        // "(N with something inside, M empty)" summary line, not change this number.
        private const double HasLootChance = 0.85;

        // A crate that has loot rolls this many distinct item stacks (1-2), not always exactly
        // one — the single-roll-per-crate design was the other half of the "too stingy"
        // complaint: even a "successful" crate often handed over a single stack of 1.
        private const int MinRollsPerCrate = 1;
        private const int MaxRollsPerCrate = 2;

        private static readonly Color CrateColor = new Color(0.45f, 0.35f, 0.2f);

        [MenuItem("Tools/SheltreSim/Setup M10 Consumables And Loot")]
        public static void SetupConsumablesAndLoot()
        {
            GameObject outdoorArea = GameObject.Find("OutdoorArea");
            if (outdoorArea == null)
            {
                Debug.LogError("No 'OutdoorArea' GameObject found — run Tools > SheltreSim > Setup M4 Outdoor Area first.");
                return;
            }

            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Resources");

            ResourceDefinition water = CreateOrLoadResource("water", "Water", "Assets/Data/Resources/Water.asset",
                def => { def.isDrink = true; def.thirstRestored = 35f; });

            ResourceDefinition firstAid = CreateOrLoadResource("first_aid", "First Aid Kit", "Assets/Data/Resources/FirstAidKit.asset",
                def => { def.isMedicine = true; def.healAmount = 50f; });

            ResourceDefinition weaponUpgrade = CreateOrLoadResource("weapon_upgrade", "Makeshift Blade", "Assets/Data/Resources/WeaponUpgrade.asset",
                def => { def.isWeapon = true; def.damageBonus = 15f; });

            // Weighted candidate pool for crate rolls — repeated entries = higher weight.
            // Raw materials/food/water common, medicine/weapon upgrades rare finds.
            List<ResourceDefinition> candidates = new List<ResourceDefinition>();
            AddCandidate(candidates, "Assets/Data/Resources/Wood.asset", 3);
            AddCandidate(candidates, "Assets/Data/Resources/ScrapMetal.asset", 3);
            AddCandidate(candidates, "Assets/Data/Resources/Stone.asset", 2);
            AddCandidate(candidates, "Assets/Data/Resources/IronOre.asset", 2);
            AddCandidate(candidates, "Assets/Data/Resources/Berries.asset", 3);
            for (int i = 0; i < 4; i++)
            {
                candidates.Add(water);
            }
            candidates.Add(firstAid);
            candidates.Add(weaponUpgrade);

            if (candidates.Count == 0)
            {
                Debug.LogError("M10: no resource definitions could be loaded for the loot table — aborting crate population.");
                return;
            }

            List<Transform> markers = new List<Transform>();
            foreach (Transform t in outdoorArea.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "LootSpawn")
                {
                    markers.Add(t);
                }
            }

            if (markers.Count == 0)
            {
                Debug.LogWarning("M10: no 'LootSpawn' markers found under OutdoorArea — run Tools > SheltreSim > Setup M10 Map Expansion first. Resource definitions were still created/updated.");
                AssetDatabase.SaveAssets();
                return;
            }

            System.Random rng = new System.Random(LootSeed);
            int crateCount = 0;
            int lootedCount = 0;
            List<GameObject> crates = new List<GameObject>();

            foreach (Transform marker in markers)
            {
                Vector3 worldPos = marker.position;
                Transform parent = marker.parent;

                GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.name = "LootCrate";
                crate.transform.SetParent(parent, false);
                crate.transform.position = worldPos;
                crate.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                crate.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                EditorMaterialUtility.Apply(crate, CrateColor);

                LootContainer loot = crate.AddComponent<LootContainer>();
                bool hasLoot = rng.NextDouble() < HasLootChance;
                loot.hasLoot = hasLoot;

                if (hasLoot)
                {
                    int rollCount = rng.Next(MinRollsPerCrate, MaxRollsPerCrate + 1);
                    List<ResourceDefinition> alreadyRolled = new List<ResourceDefinition>();

                    for (int roll = 0; roll < rollCount; roll++)
                    {
                        ResourceDefinition picked = PickDistinctCandidate(rng, candidates, alreadyRolled);
                        if (picked == null)
                        {
                            break; // ran out of distinct candidates (tiny pool) — stop rather than duplicate.
                        }
                        alreadyRolled.Add(picked);

                        // Medicine/weapon finds are single items regardless of roll count; raw
                        // materials/food/water hand out a bigger, more satisfying stack than the
                        // original 1-3 — a direct response to the "too stingy" playtest feedback.
                        int amount = (picked == firstAid || picked == weaponUpgrade) ? 1 : rng.Next(2, 6);
                        loot.entries.Add(new LootEntry { resource = picked, amount = amount });
                    }

                    lootedCount++;
                }

                UnityEngine.Object.DestroyImmediate(marker.gameObject);
                crates.Add(crate);
                crateCount++;
            }

            int buried = ReportBuriedCrates(crates);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log($"M10 consumables & loot ready: Water/First Aid Kit/Makeshift Blade resources created; {crateCount} loot crates placed ({lootedCount} with something inside, {crateCount - lootedCount} empty, {buried} buried inside other geometry).");
        }

        /// <summary>
        /// Construction-time assertion, added by the M10 QC pass after the first version of
        /// this milestone shipped 15 of its 38 crates sealed inside the car body cube: a crate
        /// whose centre sits inside another collider is invisible (hidden by an opaque mesh)
        /// AND unreachable (PlayerInteractor's raycast stops at the enclosing surface, and the
        /// crate isn't a child of it, so GetComponentInParent never finds the LootContainer).
        /// That is dead content a batch run otherwise reports as a success — every count in the
        /// summary line was correct, the wiring was correct, nothing threw.
        ///
        /// Tests the crate's CENTRE POINT rather than its full box deliberately: a crate
        /// resting on a floor is tangent to it, and an exact-tangency box overlap is a
        /// floating-point coin flip (see the second QC pass's Physics.CheckBox measurement).
        /// A 5cm sphere at the centre can only be hit by something the crate is genuinely
        /// inside of, with no name-based allowlist to keep in sync.
        /// </summary>
        private static int ReportBuriedCrates(List<GameObject> crates)
        {
            // Colliders created this frame aren't in the physics scene until transforms sync.
            Physics.SyncTransforms();

            int buried = 0;
            foreach (GameObject crate in crates)
            {
                Collider[] overlaps = Physics.OverlapSphere(crate.transform.position, 0.05f);
                foreach (Collider other in overlaps)
                {
                    if (other.gameObject == crate)
                    {
                        continue;
                    }

                    Debug.LogError($"M10: loot crate under '{(crate.transform.parent != null ? crate.transform.parent.name : "(root)")}' at {crate.transform.position} is buried inside '{other.name}' — it would be invisible and impossible to interact with in Play Mode. Move the LootSpawn marker that produced it.");
                    buried++;
                    break;
                }
            }

            return buried;
        }

        private static ResourceDefinition CreateOrLoadResource(string id, string displayName, string path, Action<ResourceDefinition> configure)
        {
            ResourceDefinition existing = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(path);
            if (existing != null)
            {
                return existing;
            }

            ResourceDefinition definition = ScriptableObject.CreateInstance<ResourceDefinition>();
            definition.id = id;
            definition.displayName = displayName;
            configure(definition);

            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        /// <summary>
        /// Picks a weighted-random candidate, re-rolling (up to a bounded number of times)
        /// if it duplicates something already rolled for this crate — a multi-roll crate
        /// should read as "varied loot", not "3 Wood" three times over. Falls back to null if
        /// the candidate pool genuinely has no distinct option left (never happens with this
        /// project's 8-resource pool at 1-2 rolls, but a tiny future pool shouldn't infinite-loop).
        /// </summary>
        private static ResourceDefinition PickDistinctCandidate(System.Random rng, List<ResourceDefinition> candidates, List<ResourceDefinition> alreadyRolled)
        {
            const int maxAttempts = 20;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                ResourceDefinition candidate = candidates[rng.Next(candidates.Count)];
                if (!alreadyRolled.Contains(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void AddCandidate(List<ResourceDefinition> candidates, string path, int weight)
        {
            ResourceDefinition resource = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(path);
            if (resource == null)
            {
                Debug.LogWarning($"M10: could not load '{path}' for the loot table — skipping this candidate. Run the earlier milestone setup tools first.");
                return;
            }

            for (int i = 0; i < weight; i++)
            {
                candidates.Add(resource);
            }
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
