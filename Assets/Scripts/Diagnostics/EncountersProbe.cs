using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SheltreSim.Bestiary;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Raids;
using SheltreSim.Survival;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// `--encounters-probe`: world encounters in the built player. Ruined-only, the guaranteed camp near home and its parts,
    /// the safe radius round the bunker, species by habitat, determinism, the locked chest, defeat persistence across
    /// unload and the save capture/restore path, no second loot bag, camp alert, a dormant lair boss, standing actors,
    /// reachable crates. Never touches a user save. Run without -nographics.
    /// </summary>
    public sealed class EncountersProbe : MonoBehaviour
    {
        private ProceduralWorld world;
        private FirstPersonController player;
        private SurvivalStats stats;
        private Inventory inventory;
        private int failures;
        private bool finished;
        private float started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--encounters-probe") < 0) return;
            new GameObject("EncountersProbe").AddComponent<EncountersProbe>();
        }

        private IEnumerator Start()
        {
            started = Time.realtimeSinceStartup;
            var routines = new Stack<IEnumerator>();
            routines.Push(Run());
            while (routines.Count > 0 && !finished)
            {
                object next = null;
                bool advanced;
                try
                {
                    advanced = routines.Peek().MoveNext();
                    if (advanced) next = routines.Peek().Current;
                    else routines.Pop();
                }
                catch (Exception exception)
                {
                    Check(false, "Exception: " + exception);
                    break;
                }
                if (!advanced) continue;
                if (next is IEnumerator nested) routines.Push(nested);
                else yield return next;
            }
            Finish();
        }

        private void Update()
        {
            // The probe stands among hostiles: keep the player alive so checks never run on a game-over screen.
            if (stats != null && !stats.IsDead && stats.health < stats.maxHealth) stats.Heal(stats.maxHealth);
            if (!finished && started > 0f && Time.realtimeSinceStartup - started > 230f)
            {
                Check(false, "Timed out after 230 seconds");
                Finish();
            }
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            Time.timeScale = 1f;
            Debug.Log(failures == 0 ? "[EncountersProbe] ALL CHECKS PASSED" : $"[EncountersProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private IEnumerator Run()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return null;
            yield return null;
            world = ProceduralWorld.Active;
            player = FindFirstObjectByType<FirstPersonController>();
            if (!Check(world != null && player != null, "World and player exist")) yield break;
            stats = player.GetComponent<SurvivalStats>();
            inventory = player.GetComponent<Inventory>();
            stats.decayPaused = true;
            int seed = world.Seed;

            // ---- the normal world has none of it ----
            Vector2Int? guaranteed = EnemyCamps.Guaranteed(seed);
            if (!Check(guaranteed.HasValue, "A camp is guaranteed near home")) yield break;
            Vector2Int campChunk = guaranteed.Value;
            Vector3 campCenter = EnemyCamps.Center(campChunk, seed);
            float campDistance = EncounterPlacement.BunkerDistance(campCenter);
            Check(campDistance >= EnemyCamps.MinBunkerDistance && campDistance <= EnemyCamps.GuaranteedRange,
                $"Guaranteed camp {campChunk} is {campDistance:0} m from the bunker (350-600)");
            for (int i = 0; i < 5; i++) yield return null;
            Check(!world.Ruined, "The game starts in the normal world");
            GameObject normalCamp = world.EnsureChunk(campChunk);
            Check(normalCamp.transform.Find(EnemyCamps.RootName) == null, "Normal world: no camp in the camp chunk");
            Check(world.GetComponentsInChildren<ChunkEncounterSlot>(true).Length == 0 && world.GetComponentsInChildren<SpeciesActor>(true).Length == 0,
                "Normal world: no encounter slot or species actor anywhere");

            SheltreSim.Core.HomeBase.Active.DebugStartSurvival();
            for (int i = 0; i < 45; i++) yield return null;
            Check(world.Ruined, "Ruined after DebugStartSurvival");

            // ---- home stays safe ----
            yield return Visit(WorldLayout.BunkerCenter + new Vector3(0f, 0f, -30f));
            var nearHome = world.GetComponentsInChildren<ChunkEncounterSlot>(true);
            Check(nearHome.All(s => !EncounterPlacement.NearBunker(s.transform.position)),
                $"No encounter within 200 m of the bunker ({nearHome.Length} slots loaded around home)");
            Check(world.GetComponentsInChildren<SpeciesActor>(true).All(a => !EncounterPlacement.NearBunker(a.transform.position)),
                "No world species actor within 200 m of the bunker");

            // ---- the guaranteed camp ----
            Vector3 lookout = campCenter + (new Vector3(WorldLayout.BunkerCenter.x, 0f, WorldLayout.BunkerCenter.z) - campCenter).normalized * 28f;
            yield return Visit(lookout);
            Transform camp = world.EnsureChunk(campChunk).transform.Find(EnemyCamps.RootName);
            if (!Check(camp != null, "The guaranteed camp is built in the ruined world")) yield break;
            CampParts(camp, out EncounterChest chest, out ChunkEncounterSlot boss, out List<ChunkEncounterSlot> members);
            CheckActorsStand(camp, "camp");
            CheckReachable(camp.GetComponentsInChildren<LootContainer>(), camp.Find("Campfire").position, "camp");
            yield return new WaitForSeconds(1.2f);
            Check(EncounterAtlas.IsDiscovered(EncounterAtlas.CampId(campChunk, seed)), "Camp discovered for the atlas from 28 m");

            // Determinism: the save path rebuilds the chunk from the seed; the same slots, species and places come back.
            string before = Signature(camp);
            world.RestoreWorld(seed, world.CaptureWorld(), true);
            yield return null;
            camp = world.EnsureChunk(campChunk).transform.Find(EnemyCamps.RootName);
            Check(camp != null && Signature(camp) == before, "Camp rebuilds identically from the seed");
            if (camp == null) yield break;
            CampParts(camp, out chest, out boss, out members);

            // Alert: one member hurt wakes the others.
            if (members.Count >= 2 && members[0].Actor != null)
            {
                int engagedBefore = members.Count(m => m.Actor != null && m.Actor.IsChasing);
                Debug.Log($"[EncountersProbe] {engagedBefore} of {members.Count} members engaged before the alert");
                members[0].Actor.TakeDamage(1f);
                for (int i = 0; i < 8; i++) yield return null;
                var campBrain = camp.GetComponent<EnemyCamp>();
                Check(campBrain.Alerted, "Hurting a member alerts the camp");
                Check(members.Skip(1).Any(m => m.Actor != null && m.Actor.IsChasing), "The alert engages the other members");
            }

            // The chest: locked while the boss lives, opens after.
            int ammo = inventory.GetCount("ammo_rifle");
            Check(chest.Locked && chest.InteractPrompt.Contains("locked"), "Commander's chest is locked: " + chest.InteractPrompt);
            chest.Interact(player.gameObject);
            Check(!chest.Searched && inventory.GetCount("ammo_rifle") == ammo, "Locked chest gives nothing");
            int bags = LootBagSystem.Bags.Count;
            boss.Actor.TakeDamage(100000f);
            yield return null;
            Check(boss.Defeated && boss.Actor == null, "Killing the Shift Commander defeats its slot");
            Check(LootBagSystem.Bags.Count == bags + 1, "The boss drops exactly one bag");
            Check(!chest.Locked, "The chest unlocks after the boss falls");
            chest.Interact(player.gameObject);
            Check(chest.Searched && inventory.GetCount("ammo_rifle") == ammo + 30, "The chest gives its guaranteed 30 rifle rounds");
            string killedName = null, woundedName = null;
            float woundedHealth = 0f;
            var alive = members.Where(m => m.Actor != null).ToList();
            if (alive.Count >= 2)
            {
                killedName = alive[0].name;
                alive[0].Actor.TakeDamage(100000f);
                woundedName = alive[1].name;
                alive[1].Actor.TakeDamage(5f);
                woundedHealth = alive[1].Health;
            }
            yield return null;
            int bagsAfterKills = LootBagSystem.Bags.Count;

            // Unload and revisit.
            yield return Visit(campCenter + new Vector3(1100f, 0f, 0f));
            Check(world.transform.Find($"Chunk_{campChunk.x}_{campChunk.y}") == null, "The camp chunk unloads when the player leaves");
            yield return Visit(lookout);
            camp = world.EnsureChunk(campChunk).transform.Find(EnemyCamps.RootName);
            CheckPersisted(camp, killedName, woundedName, woundedHealth, bagsAfterKills, "after unload and revisit");

            // The save path.
            var snapshot = world.CaptureWorld();
            world.RestoreWorld(seed, snapshot, true);
            yield return null;
            camp = world.EnsureChunk(campChunk).transform.Find(EnemyCamps.RootName);
            CheckPersisted(camp, killedName, woundedName, woundedHealth, bagsAfterKills, "after capture and restore");
            var atlas = FindFirstObjectByType<EncounterAtlas>();
            if (Check(atlas != null, "The encounters save section exists"))
            {
                string saved = atlas.CaptureState();
                atlas.RestoreState(null);
                Check(!EncounterAtlas.IsDiscovered(EncounterAtlas.CampId(campChunk, seed)), "A new game has no discovered camps");
                atlas.RestoreState(saved);
                Check(EncounterAtlas.IsDiscovered(EncounterAtlas.CampId(campChunk, seed)), "Discovered camps survive save and load");
            }

            yield return CheckLair(seed);
            yield return CheckHabitats(seed);
        }

        private void CampParts(Transform camp, out EncounterChest chest, out ChunkEncounterSlot boss, out List<ChunkEncounterSlot> members)
        {
            int tents = 0;
            foreach (Transform child in camp) if (child.name.StartsWith("Tent_")) tents++;
            Check(tents >= 3 && tents <= 4, $"Camp has {tents} tents");
            Check(camp.Find("Campfire")?.GetComponentInChildren<Light>() != null && camp.Find("Campfire")?.GetComponentInChildren<ParticleSystem>() != null,
                "Camp has a fire with light and smoke");
            Check(camp.Find("FlagPole") != null, "Camp has a flag pole");
            Check(camp.Cast<Transform>().Count(t => t.name.StartsWith("Sandbags_")) >= 4, "Camp has sandbag walls");
            var crates = camp.GetComponentsInChildren<LootContainer>().Where(l => !(l is EncounterChest)).ToArray();
            Check(crates.Length == 2, $"Camp has 2 loot crates ({crates.Length})");
            chest = camp.GetComponentInChildren<EncounterChest>();
            Check(chest != null, "Camp has the boss chest");
            boss = camp.Find("Boss")?.GetComponent<ChunkEncounterSlot>();
            Check(boss != null && boss.Actor != null && boss.Actor.GetComponent<SpeciesActor>().Species == SpeciesId.ShiftCommander,
                "The camp boss is a Shift Commander");
            members = camp.GetComponentsInChildren<ChunkEncounterSlot>().Where(s => s.name.StartsWith("Member_")).ToList();
            Check(members.Count >= 4 && members.Count <= 6, $"Camp has {members.Count} members (4-6)");
            var humanoids = new[] { SpeciesId.Scavenger, SpeciesId.Wanderer, SpeciesId.Plated, SpeciesId.Breacher, SpeciesId.Screamer };
            Check(members.All(m => m.Actor == null || humanoids.Contains(m.Actor.GetComponent<SpeciesActor>().Species)), "Camp members are the humanoid mix");
            Check(members.All(m => m.Actor == null || m.Actor.GetComponent<SpeciesActor>().GuardRadius > 0f), "Camp members guard the camp");
        }

        private void CheckPersisted(Transform camp, string killed, string wounded, float woundedHealth, int bags, string when)
        {
            if (!Check(camp != null, "Camp exists " + when)) return;
            var boss = camp.Find("Boss")?.GetComponent<ChunkEncounterSlot>();
            Check(boss != null && boss.Defeated && boss.Actor == null && boss.GetComponentInChildren<SpeciesActor>() == null, "Boss stays defeated " + when);
            var chest = camp.GetComponentInChildren<EncounterChest>();
            Check(chest != null && !chest.Locked && chest.Searched, "Chest stays open and searched " + when);
            if (killed != null)
            {
                var k = camp.Find(killed)?.GetComponent<ChunkEncounterSlot>();
                Check(k != null && k.Defeated && k.Actor == null, "Killed member stays dead " + when);
                var w = camp.Find(wounded)?.GetComponent<ChunkEncounterSlot>();
                Check(w != null && !w.Defeated && Mathf.Abs(w.Health - woundedHealth) < 0.01f, $"Wounded member keeps its health {woundedHealth:0.0} {when} ({(w != null ? w.Health : -1f):0.0})");
            }
            Check(LootBagSystem.Bags.Count == bags, "No second loot bag " + when);
        }

        private IEnumerator CheckLair(int seed)
        {
            Vector2Int? site = ProceduralLandmarks.FindNearest(new[] { ProceduralLandmarks.KindFactory, ProceduralLandmarks.KindFarm, ProceduralLandmarks.KindMilitary },
                WorldLayout.BunkerCenter, seed, 40, id => ProceduralLandmarks.TryParseStableId(id, out Vector2Int c, out _) &&
                    !BossLairs.TryPosition(c, seed, out _, out _));
            if (!Check(site.HasValue, "A factory, farm or military landmark within reach")) yield break;
            Vector3 center = ProceduralLandmarks.WorldCenter(site.Value);
            yield return Visit(center + new Vector3(-10f, 0f, -50f));
            Transform lair = world.EnsureChunk(site.Value).transform.Find(BossLairs.RootName);
            if (!Check(lair != null, "Lair built at landmark " + site.Value)) yield break;
            var slot = lair.Find("Boss")?.GetComponent<ChunkEncounterSlot>();
            var store = lair.GetComponentInChildren<EncounterChest>();
            if (!Check(slot != null && slot.Actor != null && store != null, "Lair has its boss and its store")) yield break;
            SpeciesActor boss = slot.Actor.GetComponent<SpeciesActor>();
            Check(boss.Spec.IsBoss, "The lair boss is a boss: " + boss.Spec.Name);
            CheckActorsStand(lair, "lair");
            CheckReachable(new LootContainer[] { store }, slot.transform.position, "lair");
            Vector3 start = slot.Actor.transform.position;
            var brain = slot.Actor.GetComponent<SpeciesBrain>();
            yield return new WaitForSeconds(1.5f);
            Check(brain != null && brain.IsDormant && !slot.Actor.IsChasing && (slot.Actor.transform.position - start).magnitude < 1.0f,
                "The lair boss is dormant while the player keeps away");
            Check(EncounterAtlas.IsDiscovered(EncounterAtlas.LairId(site.Value, seed)), "Lair discovered for the atlas");
            Check(store.Locked, "The store is sealed while the boss lives");
            store.Interact(player.gameObject);
            for (int i = 0; i < 6; i++) yield return null;
            Check(!store.Searched, "The sealed store gives nothing");
            Check(brain != null && !brain.IsDormant, "Touching the store wakes the boss");
            BossLairs.TryLayout(KindOf(site.Value, seed), out BossLairs.Layout layout);
            int count = inventory.GetCount(layout.contents[0].id);
            if (slot.Actor != null) slot.Actor.TakeDamage(100000f);
            yield return null;
            store.Interact(player.gameObject);
            Check(store.Searched && inventory.GetCount(layout.contents[0].id) == count + layout.contents[0].amount,
                $"After the {layout.bossName} falls the store gives {layout.contents[0].amount} {layout.contents[0].id}");
        }

        private static int KindOf(Vector2Int c, int seed) => ProceduralLandmarks.TryGetKind(c, seed, out int kind) ? kind : -1;

        private IEnumerator CheckHabitats(int seed)
        {
            var wanted = new List<(string label, Func<Habitat, bool> match)>
            {
                ("town", h => h == Habitat.Town),
                ("forest", h => h == Habitat.Forest || h == Habitat.ForestEdge),
                ("desert or highlands", h => h == Habitat.RedDesert || h == Habitat.Highlands),
                ("wetlands", h => h == Habitat.Wetlands || h == Habitat.WetlandsWater),
            };
            Vector2Int home = WorldLayout.Coordinate(WorldLayout.BunkerCenter);
            foreach (var target in wanted)
            {
                var candidates = new List<Vector2Int>();
                for (int ring = 2; ring <= 36 && candidates.Count < 3; ring++)
                    for (int dz = -ring; dz <= ring && candidates.Count < 3; dz++)
                        for (int dx = -ring; dx <= ring && candidates.Count < 3; dx += (dz == -ring || dz == ring) ? 1 : ring * 2)
                        {
                            var c = new Vector2Int(home.x + dx, home.y + dz);
                            Vector3 middle = new Vector3(c.x * 128f + 64f, 0f, c.y * 128f + 64f);
                            if (EncounterPlacement.BunkerDistance(middle) < 330f || EncounterHabitats.PlannedGroups(c, seed) == 0) continue;
                            if (target.match(EncounterHabitats.HabitatAt(c, seed, middle.x, middle.z))) candidates.Add(c);
                        }
                if (!Check(candidates.Count > 0, "Found a " + target.label + " chunk with a planned group")) continue;
                Transform root = null;
                Vector2Int chunk = candidates[0];
                foreach (Vector2Int c in candidates)
                {
                    chunk = c;
                    yield return Visit(new Vector3(c.x * 128f + 64f, 0f, c.y * 128f - 70f));
                    root = world.EnsureChunk(c).transform.Find(EncounterHabitats.RootName);
                    if (root != null) break;
                    Debug.Log($"[EncountersProbe] {target.label} chunk {c}: planned group found no free spot");
                }
                if (!Check(root != null, $"A {target.label} chunk has a group ({chunk})")) continue;
                var groups = root.GetComponentsInChildren<EncounterGroupInfo>();
                var line = new StringBuilder();
                foreach (var g in groups)
                {
                    var species = g.GetComponentsInChildren<SpeciesActor>().Select(a => a.Species).ToArray();
                    line.Append($"{g.habitat}:{string.Join(",", species)} ");
                    Check(species.Length >= 1 && species.Length <= 3 && species.All(s => g.pool.Contains(s)) && species.Distinct().Count() == 1,
                        $"{target.label}: one species from the {g.habitat} pool, 1-3 of them");
                    Check(EncounterHabitats.Pool(g.habitat).SequenceEqual(g.pool), "Pool matches the habitat table");
                }
                Debug.Log($"[EncountersProbe] {target.label} chunk {chunk}: {line}");
                CheckActorsStand(root, target.label);
                string before = Signature(root);
                world.RestoreWorld(seed, world.CaptureWorld(), true);
                yield return null;
                root = world.EnsureChunk(chunk).transform.Find(EncounterHabitats.RootName);
                Check(root != null && Signature(root) == before, target.label + " group rebuilds identically");
            }
        }

        // ---- helpers ----

        private IEnumerator Visit(Vector3 point)
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            point.y = WorldLayout.HeightAt(point.x, point.z, world.Seed) + 1.1f;
            player.transform.SetPositionAndRotation(point, Quaternion.identity);
            player.RestoreView(0f);
            body.enabled = true;
            Physics.SyncTransforms();
            for (int frame = 0; frame < 70; frame++) yield return null;
        }

        private static string Signature(Transform root)
        {
            var sb = new StringBuilder();
            foreach (var slot in root.GetComponentsInChildren<ChunkEncounterSlot>(true).OrderBy(s => s.transform.parent.name + "/" + s.name, StringComparer.Ordinal))
            {
                var actor = slot.GetComponentInChildren<SpeciesActor>(true);
                Vector3 p = slot.transform.position;
                sb.Append(slot.transform.parent.name).Append('/').Append(slot.name).Append(':').Append(actor != null ? actor.Species.ToString() : "-")
                    .Append($"@{p.x:F2},{p.y:F2},{p.z:F2};");
            }
            return sb.ToString();
        }

        /// <summary>Each actor stands on a floor and its capsule touches nothing solid but the ground.</summary>
        private void CheckActorsStand(Transform root, string label)
        {
            Physics.SyncTransforms();
            int bad = 0, total = 0;
            var problems = new StringBuilder();
            foreach (SpeciesActor actor in root.GetComponentsInChildren<SpeciesActor>())
            {
                total++;
                CharacterController body = actor.Enemy.Body;
                float scale = Mathf.Abs(actor.transform.lossyScale.y);
                float radius = body.radius * scale, height = body.height * scale;
                Vector3 center = actor.Enemy.CenterPosition, feet = center - Vector3.up * height * .5f;
                bool grounded = false;
                foreach (RaycastHit hit in Physics.RaycastAll(center, Vector3.down, height * .5f + 1.4f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                {
                    if (EncounterPlacement.IsDynamic(hit.collider)) continue;
                    grounded = hit.distance <= height * .5f + 1.2f;
                    break;
                }
                float terrain = WorldLayout.HeightAt(feet.x, feet.z, world.Seed);
                bool aboveTerrain = feet.y > terrain - 0.2f;
                var blockers = Physics.OverlapCapsule(feet + Vector3.up * (radius + .1f), feet + Vector3.up * Mathf.Max(radius + .1f, height - radius), radius * .95f,
                    ~0, QueryTriggerInteraction.Ignore).Where(c => !EncounterPlacement.IsDynamic(c) && c.GetComponent<ProceduralTerrain>() == null).ToArray();
                if (!grounded || !aboveTerrain || blockers.Length > 0)
                {
                    bad++;
                    problems.Append($"{actor.name} at {feet} grounded={grounded} aboveTerrain={aboveTerrain} inside={string.Join(",", blockers.Select(b => b.name))}; ");
                }
            }
            Check(total > 0 && bad == 0, $"{label}: {total} actors stand on ground, none inside a collider {problems}");
        }

        /// <summary>Each container has a free standing spot beside it, from which a look at it hits the container first.</summary>
        private void CheckReachable(IEnumerable<LootContainer> containers, Vector3 openSide, string label)
        {
            foreach (LootContainer loot in containers)
            {
                var collider = loot.GetComponent<Collider>();
                Vector3 target = collider != null ? collider.bounds.center : loot.transform.position;
                Vector3 away = openSide - target;
                away.y = 0f;
                away = away.sqrMagnitude > .01f ? away.normalized : Vector3.back;
                bool ok = false;
                for (int attempt = 0; attempt < 8 && !ok; attempt++)
                {
                    float turn = ((attempt + 1) / 2) * 40f * (attempt % 2 == 0 ? -1f : 1f);
                    Vector3 dir = Quaternion.Euler(0f, turn, 0f) * away;
                    float reach = (collider != null ? collider.bounds.extents.magnitude : .5f) + 0.9f;
                    Vector3 spot = target + dir * reach;
                    float lift = Mathf.Max(0.8f, target.y - WorldLayout.HeightAt(spot.x, spot.z, world.Seed) + 0.5f);
                    if (!EncounterPlacement.TryStand(spot.x, spot.z, .3f, 1.75f, world.Seed, out Vector3 feet, lift)) continue;
                    Vector3 eye = feet + Vector3.up * 1.6f;
                    foreach (RaycastHit hit in Physics.RaycastAll(eye, target - eye, (target - eye).magnitude + .5f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                    {
                        if (EncounterPlacement.IsDynamic(hit.collider)) continue;
                        ok = hit.collider == collider;
                        break;
                    }
                }
                Check(ok, $"{label}: {loot.name} can be reached and looked at");
            }
        }

        private bool Check(bool condition, string message)
        {
            if (condition) Debug.Log("[EncountersProbe] PASS: " + message);
            else { failures++; Debug.LogError("[EncountersProbe] FAIL: " + message); }
            return condition;
        }
    }
}
