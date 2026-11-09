using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SheltreSim.Bestiary;
using SheltreSim.Core;
using SheltreSim.Enemies;
using SheltreSim.Player;
using SheltreSim.Raids;
using SheltreSim.Survival;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// `--bestiary-probe [folder]`: spawns every species with Bestiary.Spawn on open flat ground away from the bunker, against
    /// the real player, and checks the fairness rules: model and stats, feet on the ground, the telegraph comes before the
    /// damage, stepping out during it means no damage, the recovery window after a boss's miss, dormant bosses that stay
    /// in their lair and don't heal, the Screamer's alarm and its interruption, the Plated's front plate, the Iron Boar
    /// stunning itself on a wall, the Rootwalker's arc, one death bag each, IsChasing, and pause. Run without -nographics.
    /// </summary>
    public sealed class BestiaryProbe : MonoBehaviour
    {
        private const float Speedup = 2f;
        private const float BossGuardRadius = 8f;

        private string folder;
        private int failures;
        private FirstPersonController player;
        private CharacterController body;
        private SurvivalStats stats;
        private Vector3 arena;
        private bool found;
        private bool waited;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private int seed = 4100;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--bestiary-probe");
            if (index < 0) return;
            var probe = new GameObject("BestiaryProbe").AddComponent<BestiaryProbe>();
            probe.folder = index + 1 < args.Length && !args[index + 1].StartsWith("-") ? args[index + 1] : Application.temporaryCachePath;
        }

        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            yield return null;
            yield return null;
            player = FindFirstObjectByType<FirstPersonController>();
            body = player != null ? player.GetComponent<CharacterController>() : null;
            stats = player != null ? player.GetComponent<SurvivalStats>() : null;
            HomeBase home = HomeBase.Active;
            Check(player != null && stats != null && home != null && ProceduralWorld.Active != null, "Player, home and world exist");
            if (failures > 0) { Finish(); yield break; }
            stats.maxHealth = 100000f;
            stats.health = 100000f;
            stats.decayPaused = true;
            home.DebugStartSurvival();
            stats.decayPaused = true;
            if (RaidDirector.Active != null) RaidDirector.Active.DebugSetCountdown(1000000f);
            yield return new WaitForSeconds(2f);

            yield return Run(FindArena());
            if (!found) { Check(false, "Found open flat ground for the arena"); Finish(); yield break; }
            Time.timeScale = Speedup;

            foreach (SpeciesSpec spec in SpeciesSpec.All) { yield return Run(CheckSpecies(spec.Id)); Cleanup(); }
            yield return Run(CheckPause()); Cleanup();
            yield return Run(CheckScreamer()); Cleanup();
            yield return Run(CheckPlated()); Cleanup();
            yield return Run(CheckShield()); Cleanup();
            yield return Run(CheckBoarWall()); Cleanup();
            yield return Run(CheckRootwalkerArc()); Cleanup();
            Finish();
        }

        private void Finish()
        {
            Time.timeScale = 1f;
            Debug.Log(failures == 0 ? "[BestiaryProbe] ALL CHECKS PASSED" : $"[BestiaryProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        // ---------- Framework ----------

        private IEnumerator Run(IEnumerator routine)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) break;
                    current = routine.Current;
                }
                catch (Exception error)
                {
                    Check(false, "Exception: " + error);
                    break;
                }
                yield return current;
            }
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(225f);
            Debug.LogError("[BestiaryProbe] FAIL: timed out");
            Application.Quit(1);
        }

        private void Check(bool condition, string message)
        {
            if (!condition) failures++;
            Debug.Log((condition ? "[BestiaryProbe] PASS: " : "[BestiaryProbe] FAIL: ") + message);
        }

        private static void Log(string message) => Debug.Log("[BestiaryProbe] " + message);

        /// <summary>Waits in game time until the condition holds; `waited` says whether it did.</summary>
        private IEnumerator Until(Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout;
            waited = condition();
            while (!waited && Time.time < end)
            {
                yield return null;
                waited = condition();
            }
        }

        /// <summary>Puts the player's feet on the highest ground under the point.</summary>
        private void Teleport(Vector3 feet)
        {
            float ground = GroundAt(feet);
            if (!float.IsNegativeInfinity(ground)) feet.y = ground;
            bool enabled = body.enabled;
            body.enabled = false;
            player.transform.position = feet + Vector3.up * 0.1f;
            body.enabled = enabled;
            Physics.SyncTransforms();
        }

        private static float GroundAt(Vector3 p, Transform ignore = null)
        {
            float best = float.NegativeInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(p.x, p.y + 30f, p.z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<CharacterController>() != null) continue;
                if (ignore != null && hit.transform.IsChildOf(ignore)) continue;
                if (hit.collider.GetComponentInParent<LootBag>() != null) continue;
                best = Mathf.Max(best, hit.point.y);
            }
            return best;
        }

        private Vector3 OnGround(Vector3 p) { p.y = GroundAt(p); return p; }

        /// <summary>Other enemies and animals near the arena would join in: switch them off. The probe's own actors stay.</summary>
        private void Quiet()
        {
            foreach (EnemyAI enemy in EnemyAI.Live.ToArray())
                if (enemy != null && !spawned.Contains(enemy.gameObject) && (enemy.transform.position - arena).sqrMagnitude < 200f * 200f)
                    enemy.gameObject.SetActive(false);
            foreach (WorldWildlife animal in FindObjectsByType<WorldWildlife>(FindObjectsSortMode.None))
                if ((animal.transform.position - arena).sqrMagnitude < 200f * 200f) animal.gameObject.SetActive(false);
        }

        private void Cleanup()
        {
            foreach (GameObject go in spawned) if (go != null) Destroy(go);
            spawned.Clear();
            Time.timeScale = Speedup;
            stats.health = stats.maxHealth;
        }

        private SpeciesActor Spawn(SpeciesId id, Vector3 feet, float yaw)
        {
            Quiet();
            SpeciesActor actor = SheltreSim.Bestiary.Bestiary.Spawn(id, null, OnGround(feet), seed++, yaw);
            if (actor != null) spawned.Add(actor.gameObject);
            if (actor != null && SpeciesSpec.Of(id).IsBoss)
            {
                actor.GuardPoint = OnGround(feet);
                actor.GuardRadius = BossGuardRadius;
            }
            Physics.SyncTransforms();
            return actor;
        }

        private static string Describe(SpeciesActor actor)
        {
            if (actor == null) return "(gone)";
            SpeciesBrain brain = actor.GetComponent<SpeciesBrain>();
            Vector3 p = actor.transform.position;
            return $"{actor.Species} at ({p.x:F1},{p.y:F2},{p.z:F1}) hp {actor.Enemy.health:F0}/{actor.Enemy.maxHealth:F0} " +
                   (brain != null ? $"state {brain.State} '{brain.Detail}' engaged {brain.Engaged} hits {brain.Hits} resolved {brain.AttacksResolved}" : "no brain");
        }

        private Vector3 InFront(SpeciesActor actor, float distance, float angle = 0f)
        {
            Vector3 forward = actor.transform.forward; forward.y = 0f; forward.Normalize();
            return OnGround(actor.transform.position + Quaternion.Euler(0f, angle, 0f) * forward * distance);
        }

        // ---------- Arena ----------

        /// <summary>Open flat dry ground far from the bunker: 9 rays within 9 m agree within 0.4 m and nothing stands on it.</summary>
        private IEnumerator FindArena()
        {
            int worldSeed = ProceduralWorld.Active.Seed;
            Vector3 home = WorldLayout.BunkerCenter;
            for (int ring = 0; ring < 6 && !found; ring++)
            {
                for (int a = 0; a < 8 && !found; a++)
                {
                    float angle = a * 45f + ring * 17f;
                    float radius = 180f + ring * 70f;
                    Vector3 c = home + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                    if (WorldLayout.IsWater(c.x, c.z, worldSeed, 25f) || WorldLayout.IsTownChunk(WorldLayout.Coordinate(c), worldSeed)) continue;
                    c.y = WorldLayout.HeightAt(c.x, c.z, worldSeed);
                    Teleport(c + Vector3.up * 1f);
                    yield return new WaitForSeconds(1.5f);
                    for (float dx = -40f; dx <= 40f && !found; dx += 8f)
                        for (float dz = -40f; dz <= 40f && !found; dz += 8f)
                        {
                            Vector3 p = c + new Vector3(dx, 0f, dz);
                            if (!ProceduralWorld.Active.HasGroundAt(p) || WorldLayout.IsWater(p.x, p.z, worldSeed, 14f)) continue;
                            if (Flat(p, out float top)) { arena = new Vector3(p.x, GroundAt(p), p.z); found = true; }
                        }
                }
            }
            if (!found) yield break;
            Teleport(arena + new Vector3(0f, 0f, -25f));
            yield return new WaitForSeconds(1f);
            Log($"Arena at {arena}, {Vector3.Distance(arena, home):F0} m from the bunker");
        }

        private static bool Flat(Vector3 p, out float top)
        {
            top = float.NegativeInfinity;
            float bottom = float.PositiveInfinity;
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    float y = GroundAt(p + new Vector3(x * 12f, 0f, z * 12f));
                    if (float.IsNegativeInfinity(y)) return false;
                    top = Mathf.Max(top, y);
                    bottom = Mathf.Min(bottom, y);
                }
            if (top - bottom > 0.6f) return false;
            Vector3 center = new Vector3(p.x, top + 0.4f + 1.5f, p.z);
            foreach (Collider c in Physics.OverlapBox(center, new Vector3(13f, 1.5f, 13f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (c.GetComponentInParent<FirstPersonController>() == null) return false;
            return true;
        }

        // ---------- Every species ----------

        private IEnumerator CheckSpecies(SpeciesId id)
        {
            SpeciesSpec spec = SpeciesSpec.Of(id);
            Teleport(arena + new Vector3(0f, 0f, spec.IsBoss ? -12f : -25f));
            SheltreSim.Raids.LootBagSystem.Clear(); // earlier kills left bags here; nothing should spawn standing on one
            yield return null;
            SpeciesActor actor = Spawn(id, arena, 180f);
            Check(actor != null, $"{spec.Name}: Bestiary.Spawn made it");
            if (actor == null) yield break;
            EnemyAI enemy = actor.Enemy;
            SpeciesBrain brain = actor.GetComponent<SpeciesBrain>();
            Check(brain != null && enemy.Brain == (IEnemyBrain)brain, $"{spec.Name}: has its brain ({(brain != null ? brain.GetType().Name : "none")})");
            if (brain == null) yield break;
            yield return new WaitForSeconds(1.2f);

            // Model and stats.
            Transform visual = actor.transform.Find("Visual");
            string[] parts = spec.Body == BodyKind.Quadruped
                ? new[] { "Torso", "Head", "Foreleg_L", "Foreleg_R", "Hindleg_L", "Hindleg_R" }
                : new[] { "Torso", "Head", "Arm_L", "Arm_R", "Leg_L", "Leg_R" };
            string[] missing = parts.Where(p => RaiderWalkAnimator.Part(visual, p) == null).ToArray();
            Check(visual != null && missing.Length == 0, $"{spec.Name}: model has its parts (missing: {string.Join(",", missing)})");
            Check(Mathf.Approximately(enemy.maxHealth, spec.Health) && Mathf.Approximately(enemy.health, spec.Health) &&
                  Mathf.Approximately(enemy.moveSpeed, spec.Speed) && Mathf.Approximately(enemy.attackDamage, spec.Damage) && enemy.superArmor == spec.IsBoss,
                  $"{spec.Name}: stats {enemy.health}/{enemy.maxHealth} hp, speed {enemy.moveSpeed}, damage {enemy.attackDamage}, super armour {enemy.superArmor}");
            bool walker = spec.Body == BodyKind.Quadruped ? actor.GetComponent<CreatureWalkAnimator>() is CreatureWalkAnimator c && c.enabled
                : actor.GetComponent<RaiderWalkAnimator>() is RaiderWalkAnimator rw && rw.enabled;
            var driver = actor.GetComponent<EnemyAnimationDriver>();
            bool animated = driver != null && driver.Driving;
            Check(animated, $"{spec.Name}: GPT's animation takes drive the model");
            if (animated)
            {
                Check(!walker && (actor.GetComponent<EnemyPose>() == null || !actor.GetComponent<EnemyPose>().enabled), $"{spec.Name}: the procedural walk and pose are off while the Animator drives");
                Transform[] moving = visual.GetComponentsInChildren<Transform>();
                Quaternion[] before = moving.Select(m => m.localRotation).ToArray();
                float moved = 0f;
                for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                {
                    yield return null;
                    for (int m = 0; m < moving.Length; m++) moved = Mathf.Max(moved, Quaternion.Angle(before[m], moving[m].localRotation));
                }
                Check(moved > 0.5f, $"{spec.Name}: the idle take moves its parts ({moved:F1} degrees)");
            }
            else Check(walker && actor.GetComponent<EnemyPose>() != null, $"{spec.Name}: walk animator for its body and the pose helper");

            // Feet.
            float ground = GroundAt(actor.transform.position, actor.transform);
            float feet = enemy.FeetPosition.y;
            float modelBottom = float.PositiveInfinity;
            foreach (Renderer r in visual != null ? visual.GetComponentsInChildren<Renderer>() : new Renderer[0]) modelBottom = Mathf.Min(modelBottom, r.bounds.min.y);
            float sink = id == SpeciesId.Mirelurker ? 0.6f : 0.3f; // it lies low on purpose
            Check(Mathf.Abs(feet - ground) < 0.2f && modelBottom > ground - sink && modelBottom < ground + 0.3f,
                $"{spec.Name}: feet on the ground (capsule bottom {feet:F2}, model bottom {modelBottom:F2}, ground {ground:F2})");

            // Idle.
            Check(!enemy.IsChasing && !brain.Engaged, $"{spec.Name}: idle with the player {Vector3.Distance(player.transform.position, actor.transform.position):F1} m away, IsChasing false. {Describe(actor)}");
            if (spec.IsBoss)
            {
                Vector3 before = actor.transform.position;
                yield return new WaitForSeconds(1.5f);
                Check(brain.IsDormant && !enemy.IsChasing && Vector3.Distance(before, actor.transform.position) < 0.3f,
                    $"{spec.Name}: dormant while the player watches from outside its lair (12 m, guard radius {BossGuardRadius}). {Describe(actor)}");
                SpeciesBehaviours.Wake(actor);
                Check(!brain.IsDormant, $"{spec.Name}: SpeciesBehaviours.Wake wakes it");
            }

            // Telegraph before damage.
            float distance = id == SpeciesId.Runner ? 3f : id == SpeciesId.IronBoar ? 6f : spec.Radius + 0.35f + 0.6f;
            Teleport(InFront(actor, distance));
            int hits = brain.Hits;
            float health = stats.health;
            yield return Until(() => brain.Hits > hits, 9f);
            Check(waited && stats.health < health, $"{spec.Name}: hits the player standing {distance:F1} m in front ({stats.health:F0} of {health:F0}). {Describe(actor)}");
            if (waited)
            {
                Check(brain.LastDamageAfterWindup >= spec.AttackWindup - 0.01f,
                    $"{spec.Name}: damage came {brain.LastDamageAfterWindup:F2} s after the wind-up started (spec wind-up {spec.AttackWindup:F2})");
                Check(enemy.IsChasing && brain.Engaged, $"{spec.Name}: IsChasing while engaged");
            }

            // Step out during the telegraph.
            yield return Until(() => brain.State == BrainState.Windup && brain.CurrentAttack != null && brain.CurrentAttack.Name != "scream", 10f);
            if (!waited) { Check(false, $"{spec.Name}: started another attack. {Describe(actor)}"); yield return Run(Kill(actor)); yield break; }
            float windup = brain.CurrentAttack.Windup;
            string attackName = brain.CurrentAttack.Name;
            hits = brain.Hits;
            int resolved = brain.AttacksResolved;
            // Well into the telegraph, so it has shown itself, with frames to spare before the strike.
            yield return Until(() => Time.time - brain.WindupStartedAt >= windup * 0.6f || brain.State != BrainState.Windup, 5f);
            Check(brain.AttacksResolved == resolved, $"{spec.Name}: probe stepped out before the strike ({Time.time - brain.WindupStartedAt:F2} of {windup:F2} s)");
            Vector3 side = actor.transform.right; side.y = 0f;
            Teleport(actor.transform.position + side.normalized * 6f - actor.transform.forward * 2f);
            yield return Until(() => brain.AttacksResolved > resolved || enemy.IsStunned, 6f);
            Check(waited && brain.Hits == hits && !brain.LastAttackHit, $"{spec.Name}: stepping out during the {attackName} telegraph means no damage. {Describe(actor)}");

            if (spec.IsBoss) yield return Run(CheckBossWindowAndLair(actor, brain));
            yield return Run(Kill(actor));
        }

        /// <summary>After a miss the boss can be hit without hitting back; it never leaves its lair; it doesn't heal.</summary>
        private IEnumerator CheckBossWindowAndLair(SpeciesActor actor, SpeciesBrain brain)
        {
            SpeciesSpec spec = actor.Spec;
            EnemyAI enemy = actor.Enemy;
            if (brain is RootColossusBrain colossus)
            {
                Teleport(InFront(actor, spec.Radius + 0.9f));
                yield return Until(() => colossus.Buried, 10f);
                Check(waited, $"{spec.Name}: buries its arms after a pair of sweeps. {Describe(actor)}");
            }
            Check(brain.State == BrainState.Recover && brain.RecoverUntil - Time.time >= 1.4f,
                $"{spec.Name}: a recovery window of {brain.RecoverUntil - Time.time:F1} s follows. {Describe(actor)}");
            Teleport(InFront(actor, spec.Radius + 0.9f));
            int hits = brain.Hits;
            float before = enemy.health;
            for (int i = 0; i < 3; i++)
            {
                enemy.TakeDamage(15f, player.transform.position);
                yield return new WaitForSeconds(0.4f);
            }
            Check(brain.Hits == hits && enemy.health < before, $"{spec.Name}: hit three times in its recovery without being hit back ({before:F0} to {enemy.health:F0} hp)");

            // The lair.
            Vector3 anchor = brain.Anchor;
            Teleport(OnGround(anchor + new Vector3(0f, 0f, -(BossGuardRadius + 13f))));
            yield return new WaitForSeconds(4f);
            Teleport(OnGround(anchor + new Vector3(0f, 0f, -(BossGuardRadius + 30f))));
            float hurt = enemy.health;
            yield return Until(() => !brain.Engaged && brain.State == BrainState.Idle, 15f);
            Check(waited, $"{spec.Name}: gives up and returns home when the player leaves. {Describe(actor)}");
            Check(brain.MaxDistanceFromAnchor <= BossGuardRadius + 15f + 0.5f,
                $"{spec.Name}: never further than {brain.MaxDistanceFromAnchor:F1} m from its lair (limit {BossGuardRadius + 15f})");
            yield return new WaitForSeconds(2f);
            Teleport(OnGround(anchor + new Vector3(0f, 0f, -4f)));
            yield return new WaitForSeconds(1f);
            Check(Mathf.Approximately(enemy.health, hurt) && enemy.health < enemy.maxHealth && brain.Engaged,
                $"{spec.Name}: no heal on return ({hurt:F0} then {enemy.health:F0} of {enemy.maxHealth:F0}), engaged again. {Describe(actor)}");
        }

        /// <summary>Kills it: exactly one bag with the species' guaranteed items, and a repeated death callback adds none.</summary>
        private IEnumerator Kill(SpeciesActor actor)
        {
            SpeciesSpec spec = actor.Spec;
            EnemyAI enemy = actor.Enemy;
            int bags = LootBagSystem.Bags.Count;
            enemy.TakeDamage(1000000f);
            Check(enemy.IsDead && LootBagSystem.Bags.Count == bags + 1, $"{spec.Name}: dies and drops one bag ({LootBagSystem.Bags.Count - bags})");
            LootBag bag = LootBagSystem.Bags.Count > bags ? LootBagSystem.Bags[LootBagSystem.Bags.Count - 1] : null;
            if (bag != null)
            {
                var missing = spec.Guaranteed.Where(g => SheltreSim.Crafting.ItemCatalog.Exists(g.id) &&
                    !bag.Contents.Any(p => p.Key == g.id && p.Value >= g.min)).Select(g => g.id).ToArray();
                Check(missing.Length == 0, $"{spec.Name}: bag holds the guaranteed items ({string.Join(", ", bag.Contents.Select(p => p.Key + " x" + p.Value))})");
                float drop = bag.transform.position.y - GroundAt(bag.transform.position, bag.transform);
                Check(Mathf.Abs(drop) < 0.5f, $"{spec.Name}: bag on the ground ({drop:F2} m)");
            }
            if (RaidDirector.Active != null) RaidDirector.Active.NotifyDeath(enemy);
            MethodInfo onDeath = typeof(SpeciesActor).GetMethod("OnDeath", BindingFlags.Instance | BindingFlags.NonPublic);
            onDeath?.Invoke(actor, new object[] { enemy });
            enemy.TakeDamage(10f);
            Check(LootBagSystem.Bags.Count == bags + 1, $"{spec.Name}: repeated death callbacks add no second bag");
            yield return new WaitForSeconds(0.3f);
        }

        // ---------- Specials ----------

        private IEnumerator CheckPause()
        {
            Teleport(arena + new Vector3(0f, 0f, -20f));
            SpeciesActor walker = Spawn(SpeciesId.Wanderer, arena, 180f);
            SpeciesActor boss = Spawn(SpeciesId.Gatekeeper, arena + new Vector3(8f, 0f, 0f), 180f);
            SpeciesBrain bossBrain = boss.GetComponent<SpeciesBrain>();
            yield return new WaitForSeconds(0.5f);
            SpeciesBehaviours.Wake(boss);
            Teleport(InFront(walker, 6f));
            yield return Until(() => walker.GetComponent<SpeciesBrain>().State == BrainState.Chasing && walker.Enemy.Body.velocity.sqrMagnitude > 0.1f, 4f);
            Teleport(InFront(boss, 1.6f));
            yield return Until(() => bossBrain.State == BrainState.Windup, 6f);
            Check(waited, $"Pause: the Gatekeeper is winding up. {Describe(boss)}");
            Time.timeScale = 0f;
            Vector3 w = walker.transform.position, b = boss.transform.position;
            float t = Time.time, health = stats.health;
            BrainState state = bossBrain.State;
            yield return new WaitForSecondsRealtime(1.2f);
            Check(walker.transform.position == w && boss.transform.position == b && Time.time == t && stats.health == health && bossBrain.State == state,
                $"Pause freezes walking, wind-up and damage. {Describe(walker)}; {Describe(boss)}");
            Time.timeScale = Speedup;
            yield return null;
        }

        private IEnumerator CheckScreamer()
        {
            // The alarm wakes an idle Wanderer 14 m behind it, 22 m from the player (its own sight is 11 m).
            Teleport(arena + new Vector3(0f, 0f, -25f));
            SpeciesActor screamer = Spawn(SpeciesId.Screamer, arena, 180f);
            SpeciesActor listener = Spawn(SpeciesId.Wanderer, arena + new Vector3(0f, 0f, 14f), 0f);
            var s = (ScreamerBrain)screamer.GetComponent<SpeciesBrain>();
            SpeciesBrain l = listener.GetComponent<SpeciesBrain>();
            yield return new WaitForSeconds(0.6f);
            Check(!l.Engaged && !s.Engaged, "Screamer: both idle before the player shows up");
            int brainsBefore = SpeciesBrain.All.Count, liveBefore = EnemyAI.Live.Count;
            Teleport(arena + new Vector3(0f, 0f, -8f));
            yield return Until(() => s.Screams > 0, 5f);
            yield return null;
            Check(waited && l.Engaged && listener.Enemy.IsChasing, $"Screamer: its scream wakes the idle Wanderer nearby. {Describe(screamer)}; {Describe(listener)}");
            Check(SpeciesBrain.All.Count == brainsBefore && EnemyAI.Live.Count == liveBefore, $"Screamer: nothing new was spawned ({SpeciesBrain.All.Count} brains, {EnemyAI.Live.Count} enemies)");
            Cleanup();

            Teleport(arena + new Vector3(0f, 0f, -25f));
            screamer = Spawn(SpeciesId.Screamer, arena, 180f);
            listener = Spawn(SpeciesId.Wanderer, arena + new Vector3(0f, 0f, 14f), 0f);
            s = (ScreamerBrain)screamer.GetComponent<SpeciesBrain>();
            l = listener.GetComponent<SpeciesBrain>();
            yield return new WaitForSeconds(0.6f);
            Teleport(arena + new Vector3(0f, 0f, -8f));
            yield return Until(() => s.State == BrainState.Windup && s.CurrentAttack != null && s.CurrentAttack.Name == "scream", 4f);
            Check(waited, $"Screamer: inhales. {Describe(screamer)}");
            yield return new WaitForSeconds(0.5f);
            screamer.Enemy.TakeDamage(2f, player.transform.position);
            yield return new WaitForSeconds(1.5f);
            Check(s.ScreamsInterrupted == 1 && s.Screams == 0 && !l.Engaged, $"Screamer: a hit during the inhale stops the alarm. {Describe(screamer)}; {Describe(listener)}");
        }

        private IEnumerator CheckPlated()
        {
            Teleport(arena + new Vector3(0f, 0f, -25f));
            SpeciesActor plated = Spawn(SpeciesId.Plated, arena, 180f);
            yield return new WaitForSeconds(0.5f);
            EnemyAI enemy = plated.Enemy;
            Vector3 forward = plated.transform.forward;
            float h0 = enemy.health;
            enemy.TakeDamage(10f, plated.transform.position + forward * 2f);
            float front = h0 - enemy.health;
            enemy.TakeDamage(10f, plated.transform.position - forward * 2f);
            float back = h0 - front - enemy.health;
            enemy.TakeDamage(10f, plated.transform.position + plated.transform.right * 2f);
            float flank = h0 - front - back - enemy.health;
            Check(Mathf.Abs(front - 10f * MeleeBrain.PlateFactor) < 0.01f && Mathf.Abs(back - 10f) < 0.01f && Mathf.Abs(flank - 10f) < 0.01f,
                $"Plated: 10 damage from the front does {front:F1}, from behind {back:F1}, from the side {flank:F1}");
            float h1 = enemy.health;
            enemy.TakeDamage(10f);
            Check(Mathf.Abs(h1 - enemy.health - 10f) < 0.01f, "Plated: a hit with no known direction does full damage");
        }

        private IEnumerator CheckShield()
        {
            Teleport(arena + new Vector3(0f, 0f, -12f));
            SpeciesActor boss = Spawn(SpeciesId.ShiftCommander, arena, 180f);
            yield return new WaitForSeconds(0.5f);
            EnemyAI enemy = boss.Enemy;
            Vector3 forward = boss.transform.forward;
            float h0 = enemy.health;
            enemy.TakeDamage(10f, boss.transform.position + forward * 2f);
            float front = h0 - enemy.health;
            enemy.TakeDamage(10f, boss.transform.position - forward * 2f);
            float back = h0 - front - enemy.health;
            Check(Mathf.Abs(front - 10f * ShiftCommanderBrain.ShieldFactor) < 0.01f && Mathf.Abs(back - 10f) < 0.01f,
                $"Shift Commander: the shield takes front hits ({front:F1} of 10), the back is open ({back:F1})");
            SpeciesBrain brain = boss.GetComponent<SpeciesBrain>();
            Check(!brain.IsDormant, "Shift Commander: a hit wakes it");

            // It turns at 90 degrees a second: circle it.
            Teleport(InFront(boss, 4f));
            yield return new WaitForSeconds(0.5f);
            Quaternion r0 = boss.transform.rotation;
            float t0 = Time.time;
            Teleport(OnGround(boss.transform.position - boss.transform.forward * 4f));
            yield return new WaitForSeconds(0.5f);
            float rate = Quaternion.Angle(r0, boss.transform.rotation) / Mathf.Max(0.01f, Time.time - t0);
            Check(rate <= ShiftCommanderBrain.TurnRate + 5f, $"Shift Commander: turns at {rate:F0} degrees a second when the player gets behind it");
        }

        private IEnumerator CheckBoarWall()
        {
            Teleport(arena + new Vector3(0f, 0f, -25f));
            SpeciesActor boar = Spawn(SpeciesId.IronBoar, arena, 180f);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "ProbeWall";
            wall.transform.position = OnGround(arena + new Vector3(0f, 0f, -9.8f)) + Vector3.up * 1.5f;
            wall.transform.localScale = new Vector3(16f, 3f, 0.6f);
            spawned.Add(wall);
            Physics.SyncTransforms();
            var brain = (IronBoarBrain)boar.GetComponent<SpeciesBrain>();
            yield return new WaitForSeconds(0.5f);
            Teleport(arena + new Vector3(0f, 0f, -7f));
            yield return Until(() => brain.State == BrainState.Windup && brain.CurrentAttack.Name == "charge", 5f);
            Check(waited, $"Iron Boar: paws the ground before charging. {Describe(boar)}");
            float windup = brain.CurrentAttack.Windup;
            int hits = brain.Hits;
            yield return Until(() => Time.time - brain.WindupStartedAt >= windup - 0.15f, 3f);
            Check(brain.State == BrainState.Windup, "Iron Boar: probe stepped aside before the charge started");
            Teleport(arena + new Vector3(5f, 0f, -6f));
            yield return Until(() => brain.WallStuns > 0, 4f);
            Check(waited && boar.Enemy.IsStunned && brain.Hits == hits, $"Iron Boar: a charge into a wall stuns it. {Describe(boar)}");
            yield return new WaitForSeconds(0.5f);
            Check(brain.State == BrainState.Stunned && boar.Enemy.IsStunned, $"Iron Boar: still stunned after 0.5 s (the opening). {Describe(boar)}");
        }

        private IEnumerator CheckRootwalkerArc()
        {
            Teleport(arena + new Vector3(0f, 0f, -25f));
            SpeciesActor root = Spawn(SpeciesId.Rootwalker, arena, 180f);
            SpeciesBrain brain = root.GetComponent<SpeciesBrain>();
            yield return new WaitForSeconds(0.5f);
            Teleport(InFront(root, 1.6f));
            yield return Until(() => brain.State == BrainState.Windup, 5f);
            int resolved = brain.AttacksResolved;
            Teleport(InFront(root, 1.8f, 55f));
            yield return Until(() => brain.AttacksResolved > resolved, 4f);
            Check(waited && brain.LastAttackHit, $"Rootwalker: the wide swing hits the player beside it (55 degrees). {Describe(root)}");
            Teleport(InFront(root, 1.6f));
            yield return Until(() => brain.State == BrainState.Windup, 6f);
            resolved = brain.AttacksResolved;
            Teleport(InFront(root, 1.6f, 180f));
            yield return Until(() => brain.AttacksResolved > resolved, 4f);
            Check(waited && !brain.LastAttackHit, $"Rootwalker: but not behind it. {Describe(root)}");
        }
    }
}
