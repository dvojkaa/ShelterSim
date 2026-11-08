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

        // 70% of crates actually contain something — the rest come up empty on search. This
        // is the playtester's "chance to spawn some things" request; the other 30% is what
        // makes that chance meaningful instead of every single crate being a guaranteed hit.
        private const double HasLootChance = 0.7;

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
                    ResourceDefinition picked = candidates[rng.Next(candidates.Count)];
                    loot.resource = picked;
                    // Medicine/weapon finds are single items; everything else rolls 1-3,
                    // matching how resource nodes already hand out small stackable amounts.
                    loot.amount = (picked == firstAid || picked == weaponUpgrade) ? 1 : rng.Next(1, 4);
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
