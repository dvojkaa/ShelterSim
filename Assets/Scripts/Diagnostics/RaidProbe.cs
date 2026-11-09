using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SheltreSim.Audio;
using SheltreSim.Building;
using SheltreSim.Core;
using SheltreSim.Enemies;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Power;
using SheltreSim.Raids;
using SheltreSim.Storage;
using SheltreSim.Survival;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// `--raid-probe`, without -nographics: real raiders on the real stairs, door and yard, in the built player. This project has failed three
    /// times on "the code's correct but the raider doesn't fit", so most of it is walking real capsules through real geometry and logging
    /// where each one was and why it stopped. Saves nothing to disk and doesn't touch PlayerPrefs.
    /// </summary>
    public sealed class RaidProbe : MonoBehaviour
    {
        private const float Speedup = 4f;
        private int failures;
        private FirstPersonController player;
        private CharacterController body;
        private SurvivalStats stats;
        private Inventory inventory;
        private GameStateManager state;
        private HomeBase home;
        private RaidDirector director;
        private InteractableDoor door;
        private readonly List<GameObject> placed = new List<GameObject>();

        private static readonly Vector3 RoomSpot = new Vector3(72f, 0.1f, -215f);
        private static readonly Vector3 FarAway = new Vector3(60f, 40f, -560f);
        private static readonly Vector3 YardSpot = new Vector3(84f, 6.1f, -228f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--raid-probe") >= 0)
                new GameObject("RaidProbe").AddComponent<RaidProbe>();
        }

        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            yield return null;
            yield return null;
            player = FindFirstObjectByType<FirstPersonController>();
            body = player.GetComponent<CharacterController>();
            stats = player.GetComponent<SurvivalStats>();
            inventory = player.GetComponent<Inventory>();
            state = FindFirstObjectByType<GameStateManager>();
            home = HomeBase.Active;
            director = RaidDirector.Active;
            door = FindObjectsByType<InteractableDoor>(FindObjectsSortMode.None).FirstOrDefault(d => Vector3.Distance(d.PassageCenter, new Vector3(72f, 1.1f, -223.8f)) < 1f);
            Check(player != null && home != null && director != null && door != null, $"Scene pieces exist (door at {(door != null ? door.PassageCenter.ToString() : "none")})");
            stats.maxHealth = 100000f;
            stats.health = 100000f;
            stats.decayPaused = true;

            yield return Run(CheckNothingBeforeSurvival());
            home.DebugStartSurvival();
            stats.decayPaused = true;
            yield return new WaitForSeconds(2f);
            Physics.SyncTransforms();
            Time.timeScale = Speedup;

            yield return Run(CheckGeometry());
            yield return Run(CheckFullRaidWalksIn());
            yield return Run(CheckVariantsThroughDoor());
            yield return Run(CheckClosedDoor());
            yield return Run(CheckDoorSaveAndRepair());
            yield return Run(CheckWallAtStairFoot());
            yield return Run(CheckGunner());
            yield return Run(CheckScavenger());
            yield return Run(CheckMachineGun());
            yield return Run(CheckLaser());
            yield return Run(CheckTraps());
            yield return Run(CheckDeathLoot());
            yield return Run(CheckWarningAndAlarm());
            yield return Run(CheckFarPlayer());
            yield return Run(CheckRestoreDuringRaid());
            yield return Run(CheckRaidEnds());
            yield return Run(CheckPatrolUnchanged());

            Time.timeScale = 1f;
            Debug.Log(failures == 0 ? "[RaidProbe] ALL CHECKS PASSED" : $"[RaidProbe] {failures} CHECKS FAILED");
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
            Clean();
            yield return null;
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(290f);
            Debug.LogError("[RaidProbe] FAIL: timed out");
            Application.Quit(1);
        }

        private void Check(bool ok, string message)
        {
            Debug.Log($"[RaidProbe] {(ok ? "PASS" : "FAIL")}: {message}");
            if (!ok) failures++;
        }

        private static void Log(string message) => Debug.Log("[RaidProbe] " + message);

        /// <summary>The state of every raid actor, on one line each: enough to tell why a test failed from the log alone.</summary>
        private void Trace(string label)
        {
            foreach (RaidAgent a in director.Actors)
            {
                if (a == null) continue;
                Vector3 p = a.transform.position;
                EnemyAI e = a.GetComponent<EnemyAI>();
                Log($"  {label} t={Time.time:F1} {a.Variant} pos=({p.x:F1},{p.y:F1},{p.z:F1}) state='{a.State}' route={a.RouteIndex}/{a.RouteCount} hp={e.health:F0} stuck={a.StuckSeconds:F1}s hits={a.StructureHits} reason='{a.LastStuckReason}'");
            }
        }

        private void Clean()
        {
            foreach (GameObject obj in placed)
            {
                if (obj == null) continue;
                obj.SetActive(false);
                Destroy(obj);
            }
            placed.Clear();
            director.RestoreState(null);
            director.DebugSetCountdown(1000000f);
            door.RestoreState(true);
            door.RestoreRaidState(door.maxRaidHealth, false);
            stats.RestoreVitals(100f, 100f, stats.maxHealth);
            PowerGrid.RestoreHeat(YardSpot, 0f);
            Physics.SyncTransforms();
        }

        private void Teleport(Vector3 position)
        {
            bool enabled = body.enabled;
            body.enabled = false;
            player.transform.position = position;
            body.enabled = enabled;
            Physics.SyncTransforms();
        }

        private GameObject Block(string name, Vector3 center, Vector3 size, float structureHealth = 0f)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = center;
            cube.transform.localScale = size;
            if (structureHealth > 0f)
            {
                StructureHealth structure = cube.AddComponent<StructureHealth>();
                structure.maxHealth = structureHealth;
            }
            placed.Add(cube);
            Physics.SyncTransforms();
            return cube;
        }

        /// <summary>A piece as the coordinator's tool will make it: a root with a box collider, plus the script and health.</summary>
        private T Piece<T>(string name, Vector3 feet, Vector3 size) where T : Component
        {
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.position = feet;
            var box = root.AddComponent<BoxCollider>();
            box.center = Vector3.up * size.y * 0.5f;
            box.size = size;
            root.AddComponent<StructureHealth>();
            T piece = root.AddComponent<T>();
            placed.Add(root);
            root.SetActive(true);
            Physics.SyncTransforms();
            return piece;
        }

        private ProbePower Power(Vector3 position, float kw = 50f)
        {
            var obj = new GameObject("ProbePower");
            obj.transform.position = position;
            ProbePower power = obj.AddComponent<ProbePower>();
            power.Output = kw;
            placed.Add(obj);
            return power;
        }

        private void GridTick()
        {
            PowerGrid.DebugTick(0.25f);
            PowerGrid.DebugTick(0.25f);
        }

        private RaidAgent Held(RaidVariant variant, Vector3 feet)
        {
            RaidAgent agent = director.DebugSpawn(variant, feet);
            agent.GetComponent<EnemyAI>().Stun(100000f);
            return agent;
        }

        private IEnumerator WaitUntil(Func<bool> condition, float gameSeconds, string label)
        {
            float start = Time.time;
            float nextTrace = Time.time;
            while (!condition() && Time.time - start < gameSeconds)
            {
                if (Time.time >= nextTrace)
                {
                    nextTrace = Time.time + 8f;
                    Trace(label);
                }
                yield return null;
            }
            Trace(label + " end");
        }

        // ---------- Phases ----------

        private IEnumerator CheckNothingBeforeSurvival()
        {
            Check(state.CurrentPhase == GamePhase.Building, "Starts in the Building phase");
            director.DebugSetCountdown(0.2f);
            director.DebugStartRaid(1);
            yield return new WaitForSeconds(1.5f);
            Check(director.CurrentPhase == RaidDirector.Phase.Countdown && director.RaidersLeft == 0 && director.BannerText == "", $"No raid while Building ({director.CurrentPhase}, {director.RaidersLeft} raiders)");
            Check(director.Countdown > 0f, $"The clock doesn't run while Building ({director.Countdown:F2})");
            state.RestorePhase(GamePhase.Survival);
            yield return new WaitForSeconds(0.5f);
            Check(!home.BunkerComplete && director.RaidersLeft == 0, "No raid in Survival while the bunker is unfinished");
            state.RestorePhase(GamePhase.Building);
        }

        // ---------- Measuring, before anyone walks ----------

        private IEnumerator CheckGeometry()
        {
            door.RestoreState(true);
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();

            bool headroom = Physics.Raycast(new Vector3(72f, 1f, -223.8f), Vector3.up, out RaycastHit lintel, 5f, ~0, QueryTriggerInteraction.Ignore);
            Log($"Door opening: underside of the lintel y={(headroom ? lintel.point.y : -1f):F2}");
            float left = RayDistance(new Vector3(72f, 1f, -223.8f), Vector3.left), right = RayDistance(new Vector3(72f, 1f, -223.8f), Vector3.right);
            Log($"Door opening with the leaf swung open: {left:F2} m to the left of centre, {right:F2} m to the right (usable {left + right:F2})");
            float shaftL = RayDistance(new Vector3(72f, 3f, -232f), Vector3.left), shaftR = RayDistance(new Vector3(72f, 3f, -232f), Vector3.right);
            Log($"Stair shaft at z=-232: width {shaftL + shaftR:F2}");

            Vector3[] points = { RaidRoute.StairHead, RaidRoute.RampTop, RaidRoute.StairFoot, RaidRoute.DoorFront, new Vector3(72f, 0.1f, -223.8f), RaidRoute.DoorBack };
            foreach (RaidVariant variant in Enum.GetValues(typeof(RaidVariant)))
            {
                RaidVariantSpec spec = RaidVariantSpec.Of(variant);
                float diameter = (spec.BodyRadius + 0.08f) * 2f;
                Check(diameter < left + right - 0.1f, $"{variant}: capsule {diameter:F2} m with skin fits the {left + right:F2} m door opening");
                Check(diameter < shaftL + shaftR - 0.1f, $"{variant}: fits the {shaftL + shaftR:F2} m shaft");
                Check(headroom && spec.BodyHeight + 0.16f < lintel.point.y, $"{variant}: capsule {spec.BodyHeight + 0.16f:F2} m high with skin fits under the lintel at {(headroom ? lintel.point.y : -1f):F2}");
                foreach (Vector3 point in points)
                {
                    // Standing on the real floor there.
                    if (!Physics.Raycast(new Vector3(point.x, point.y + 1.2f, point.z), Vector3.down, out RaycastHit floor, 12f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        Check(false, $"{variant}: no floor under {point}");
                        continue;
                    }
                    float half = spec.BodyHeight * 0.5f - spec.BodyRadius;
                    Vector3 center = floor.point + Vector3.up * (spec.BodyHeight * 0.5f + 0.09f);
                    Collider[] overlaps = Physics.OverlapCapsule(center - Vector3.up * half, center + Vector3.up * half, spec.BodyRadius + 0.06f, ~0, QueryTriggerInteraction.Ignore);
                    // The floor it stands on is touched by the upright capsule on a slope: the controller handles that, it isn't an obstacle.
                    string names = string.Join(", ", overlaps.Where(c => !c.GetComponentInParent<CharacterController>() && c != floor.collider).Select(c => c.name));
                    Check(names.Length == 0, $"{variant}: capsule free at ({point.x:F1},{floor.point.y:F2},{point.z:F1}){(names.Length > 0 ? " but touches " + names : "")}");
                }
            }
            Check(RaidDirector.IntervalAfter(1) == 600f && RaidDirector.IntervalAfter(30) == 360f, "Raids come every 10 minutes, shrinking to 6");
            Check(RaidVariantSpec.Of(RaidVariant.Warlord).ModelId == "boss_shift_commander" && RaidVariantSpec.Of(RaidVariant.Brute).ModelId == "enemy_breacher", "Model id table");
            var first = RaidDirector.RosterFor(7, 1234);
            Check(first.SequenceEqual(RaidDirector.RosterFor(7, 1234)), "A raid's party is reproducible: " + string.Join(",", first));
            Check(RaidDirector.RosterFor(5, 1234).Contains(RaidVariant.Warlord) && !RaidDirector.RosterFor(4, 1234).Contains(RaidVariant.Warlord), "Every 5th raid has a Warlord");
            Check(RaidDirector.RosterFor(1, 1).Count < RaidDirector.RosterFor(8, 1).Count, "Later raids are bigger");
        }

        private static float RayDistance(Vector3 from, Vector3 direction) =>
            Physics.Raycast(from, direction, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 20f;

        // ---------- Walking ----------

        private IEnumerator CheckFullRaidWalksIn()
        {
            door.RestoreState(true);
            Teleport(RoomSpot);
            yield return new WaitForSeconds(0.5f);
            float healthBefore = stats.health;
            float started = Time.time;
            director.DebugStartRaid(1);
            Check(director.CurrentPhase == RaidDirector.Phase.Active && director.RaidersLeft >= 3, $"Raid 1 starts with {director.RaidersLeft} raiders");
            foreach (RaidAgent a in director.Actors)
            {
                Vector3 p = a.transform.position;
                float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(72f, 0f, -238f));
                Check(d >= RaidDirector.SpawnRingMin - 0.5f && d <= RaidDirector.SpawnRingMax + 0.5f, $"Spawned on the ring, {d:F1} m from the stair head at ({p.x:F1},{p.y:F1},{p.z:F1})");
            }
            yield return null;
            Check(director.BannerText.StartsWith("RAID 1:"), $"Banner: '{director.BannerText}'");
            Check(RaiderAudioChasing(), "Raid actors count as chasing for the music");

            RaidAgent arrived = null;
            yield return WaitUntil(() =>
            {
                arrived = director.Actors.FirstOrDefault(a => a != null && Vector3.Distance(a.transform.position, player.transform.position) < 2.2f && RaidRoute.InRoom(a.transform.position));
                return arrived != null && stats.health < healthBefore;
            }, 110f, "open-door");
            Check(arrived != null, arrived != null
                ? $"A raider walked from the surface, down the stairs, through the door and reached the player in {Time.time - started:F0} game seconds"
                : "A raider did NOT reach the player in the room");
            Check(stats.health < healthBefore, $"It hit the player ({healthBefore:F0} to {stats.health:F0})");
        }

        private bool RaiderAudioChasing() => director.Actors.All(a => a.GetComponent<EnemyAI>().IsChasing);

        /// <summary>Each variant on its own, from the stair head to the player, door open. Sizes differ, and so does what can go wrong.</summary>
        private IEnumerator CheckVariantsThroughDoor()
        {
            foreach (RaidVariant variant in new[] { RaidVariant.Brute, RaidVariant.Warlord, RaidVariant.Scavenger, RaidVariant.Gunner })
            {
                door.RestoreState(true);
                Teleport(RoomSpot);
                RaidAgent agent = director.DebugSpawn(variant, new Vector3(72f, 6.05f, -241f));
                Transform t = agent.transform;
                EnemyAI enemy = agent.GetComponent<EnemyAI>();
                float reach = variant == RaidVariant.Gunner ? RaidVariantSpec.Of(variant).RangedRange + 2f : 2.4f; // measured from feet, the agent measures centres
                // A gunner that has the player in sight stops on the stairs and shoots, by design.
                bool arrived() => Vector3.Distance(t.position, player.transform.position) < reach &&
                    (RaidRoute.InRoom(t.position) || (variant == RaidVariant.Gunner && agent.State.Contains("shooting")));
                yield return WaitUntil(arrived, 45f, variant.ToString());
                Check(arrived(),
                    $"{variant} reached the room at ({t.position.x:F1},{t.position.y:F1},{t.position.z:F1}), state '{agent.State}', reason '{agent.LastStuckReason}'");
                Check(enemy.maxHealth == RaidVariantSpec.Of(variant).Health && enemy.moveSpeed == RaidVariantSpec.Of(variant).Speed, $"{variant} has its stats ({enemy.maxHealth} HP, speed {enemy.moveSpeed})");
                Clean();
                yield return null;
            }
            Check(RaidVariantSpec.Of(RaidVariant.Brute).Health == 3f * RaidVariantSpec.Of(RaidVariant.Raider).Health, "Brute has 3x health");
            Check(RaidVariantSpec.Of(RaidVariant.Warlord).Health == 8f * RaidVariantSpec.Of(RaidVariant.Raider).Health, "Warlord has 8x health");
            Check(RaidVariantSpec.Of(RaidVariant.Brute).StructureDamage == 2f * RaidVariantSpec.Of(RaidVariant.Raider).StructureDamage, "Brute doubles structure damage");
        }

        private IEnumerator CheckClosedDoor()
        {
            door.RestoreState(false);
            yield return new WaitForFixedUpdate();
            Teleport(RoomSpot);
            director.DebugSpawn(RaidVariant.Raider, new Vector3(72f, 2f, -231f));
            director.DebugSpawn(RaidVariant.Raider, new Vector3(71f, 3.5f, -234f));
            float firstHit = float.NaN;
            yield return WaitUntil(() =>
            {
                if (float.IsNaN(firstHit) && door.RaidHealth < door.maxRaidHealth) firstHit = Time.time;
                return door.IsOpen;
            }, 90f, "closed-door");
            Check(!float.IsNaN(firstHit), $"Raiders attacked the closed door (HP {door.RaidHealth:F0})");
            Check(door.IsBroken && door.IsOpen, $"The door broke down (broken={door.IsBroken}, open={door.IsOpen})");
            float took = Time.time - firstHit;
            Check(took > 5f, $"Breaking it took time, not one hit: {took:F1} game seconds for {door.maxRaidHealth:F0} HP");
            RaidAgent inRoom = null;
            yield return WaitUntil(() => (inRoom = director.Actors.FirstOrDefault(a => a != null && RaidRoute.InRoom(a.transform.position))) != null, 25f, "after-door");
            Check(inRoom != null, "They came through the broken door into the room");
        }

        private IEnumerator CheckDoorSaveAndRepair()
        {
            door.RestoreState(false);
            door.TakeRaidDamage(120f);
            Check(door.RaidHealth == door.maxRaidHealth - 120f && !door.IsOpen, $"A hit dents the door and it stays shut ({door.RaidHealth:F0})");
            string dented = director.CaptureState();
            door.TakeRaidDamage(10000f);
            Check(door.IsBroken && door.IsOpen, "Enough hits break it");
            door.RestoreState(false); // what SaveManager does to an old-pose save before the raid section runs
            string broken = null;
            door.RestoreRaidState(0f, true);
            broken = director.CaptureState();
            Check(door.IsOpen, "A broken door can't be shut by restoring a pose");
            director.RestoreState(dented);
            Check(!door.IsBroken && door.RaidHealth == door.maxRaidHealth - 120f, $"Loading the earlier save brings back the dented door ({door.RaidHealth:F0})");
            door.RestoreState(false);
            director.RestoreState(broken);
            Check(door.IsBroken && door.IsOpen, "Loading the later save brings back the broken door");
            yield return null;

            inventory.SetCount("repair_kit_metal", 0);
            door.Interact(player.gameObject);
            Check(door.IsBroken, $"No kit, no repair. Prompt: '{door.InteractPrompt}'");
            inventory.SetCount("repair_kit_metal", 1);
            door.Interact(player.gameObject);
            Check(!door.IsBroken && door.RaidHealth == door.maxRaidHealth && inventory.GetCount("repair_kit_metal") == 0, "One Metal Repair Kit repairs it");
            door.Interact(player.gameObject);
            Check(!door.IsOpen, "A repaired door closes again");
            door.RestoreState(true);
            door.ForceOpen();
            Check(door.RaidHealth == door.maxRaidHealth, "A patrol's ForceOpen is unchanged and does no damage");
        }

        private IEnumerator CheckWallAtStairFoot()
        {
            door.RestoreState(true);
            yield return new WaitForFixedUpdate();
            Teleport(RoomSpot);
            GameObject wall = Block("ProbeWall", new Vector3(72f, 1.6f, -226.6f), new Vector3(2.8f, 3.2f, 0.25f), 60f);
            StructureHealth structure = wall.GetComponent<StructureHealth>();
            RaidAgent agent = director.DebugSpawn(RaidVariant.Raider, new Vector3(72f, 5.7f, -237f));
            yield return WaitUntil(() => wall == null || !wall.activeInHierarchy, 70f, "wall");
            Check(wall == null || !wall.activeInHierarchy, $"The raider destroyed the wall sealing the stair foot (hits {agent.StructureHits}, state '{agent.State}', HP left {(structure != null ? structure.Health : 0f):F0})");
            Vector3 stopped = agent.transform.position;
            Check(agent.StructureHits >= 1, "It hit it rather than sliding along it");
            yield return WaitUntil(() => RaidRoute.InRoom(agent.transform.position), 25f, "after-wall");
            Check(RaidRoute.InRoom(agent.transform.position), $"Then it came through to the room, from ({stopped.x:F1},{stopped.y:F1},{stopped.z:F1}) to ({agent.transform.position.x:F1},{agent.transform.position.y:F1},{agent.transform.position.z:F1})");
        }

        // ---------- Variants ----------

        private IEnumerator CheckGunner()
        {
            Teleport(YardSpot);
            RaidAgent agent = director.DebugSpawn(RaidVariant.Gunner, YardSpot + new Vector3(0f, 0f, -11f));
            float before = stats.health;
            int plays = 0;
            GameAudio.PlayLog = new List<string>();
            yield return WaitUntil(() => stats.health <= before - 16f, 12f, "gunner");
            plays = GameAudio.PlayLog.Count(k => k == Sfx.RaiderGunshot);
            GameAudio.PlayLog = null;
            float dealt = before - stats.health;
            Check(dealt >= 8f && Mathf.Abs(dealt % 8f) < 0.01f, $"Gunner shoots the player in 8 damage steps from range ({dealt:F0})");
            Check(plays >= 2, $"Gunshots are heard ({plays})");
            Check(Vector3.Distance(agent.transform.position, player.transform.position) > 3f, $"It keeps its distance while shooting ({Vector3.Distance(agent.transform.position, player.transform.position):F1} m)");

            Clean();
            yield return null;
            Teleport(YardSpot);
            Block("ProbeThickWall", YardSpot + new Vector3(0f, 2f, -3f), new Vector3(10f, 4f, 0.4f));
            agent = Held(RaidVariant.Gunner, YardSpot + new Vector3(0f, 0f, -8f));
            agent.GetComponent<EnemyAI>().Stun(0f);
            before = stats.health;
            agent.GetComponent<EnemyAI>().ApplySlow(0.05f, 1000f);
            yield return new WaitForSeconds(5f);
            Log($"gunner behind wall ended at {agent.transform.position}, state '{agent.State}'");
        }

        private IEnumerator CheckScavenger()
        {
            door.RestoreState(true);
            Teleport(new Vector3(77f, 0.1f, -213f));
            var obj = new GameObject("ProbeCrate");
            obj.transform.position = new Vector3(67f, 0.5f, -222.4f);
            ProbeStore store = obj.AddComponent<ProbeStore>();
            store.Add("nails", 10);
            store.Add("cloth", 10);
            placed.Add(obj);
            RaidAgent agent = director.DebugSpawn(RaidVariant.Scavenger, new Vector3(72f, 0.1f, -225f));
            int total() => store.GetCount("nails") + store.GetCount("cloth");
            yield return WaitUntil(() => total() < 20, 30f, "scavenger");
            Check(total() < 20, $"The scavenger stole from the crate ({total()} of 20 left, state '{agent.State}')");
            Check(obj.activeInHierarchy && obj != null, "It destroyed nothing");
            int stolen = agent.Stolen.Values.Sum();
            Check(stolen == 20 - total(), $"What left the crate is in its bag ({stolen})");
            EnemyAI enemy = agent.GetComponent<EnemyAI>();
            int bags = LootBagSystem.Bags.Count;
            enemy.TakeDamage(1000f);
            Check(LootBagSystem.Bags.Count == bags + 1, "It dropped a bag");
            LootBag bag = LootBagSystem.Bags.Last();
            int inBag = bag.Contents.Where(p => p.Key == "nails" || p.Key == "cloth").Sum(p => p.Value);
            Check(inBag >= stolen, $"Stolen items are in that bag ({inBag} of nails and cloth, stole {stolen})");
            StoreUnregister(store);
        }

        private static void StoreUnregister(ProbeStore store) { if (store != null) StorageRegistry.Unregister(store); }

        // ---------- Turrets ----------

        private IEnumerator CheckMachineGun()
        {
            Vector3 spot = YardSpot + new Vector3(0f, -0.1f, -8f);
            Teleport(YardSpot + new Vector3(0f, 0f, 18f));
            MachineGunTurretPiece gun = Piece<MachineGunTurretPiece>("ProbeTurret", spot, new Vector3(1f, 1.5f, 1f));
            ProbePower power = Power(spot + Vector3.right * 2f);
            GridTick();
            Vector3 targetFeet = spot + Vector3.forward * 8f;

            // No ammunition: powered, target in sight, silence.
            RaidAgent agent = Held(RaidVariant.Raider, targetFeet);
            yield return new WaitForSeconds(4f);
            float hp = agent.GetComponent<EnemyAI>().health;
            Check(gun.Powered && gun.ShotsFired == 0 && hp == 30f, $"Empty turret doesn't fire (powered {gun.Powered}, shots {gun.ShotsFired}, hp {hp:F0}). Prompt: '{gun.InteractPrompt}'");
            Check(gun.InteractPrompt.Contains("no ammo"), "Its prompt says so");

            // No power.
            gun.DebugSetRounds(60);
            power.Output = 0f;
            GridTick();
            yield return new WaitForSeconds(4f);
            Check(!gun.Powered && gun.ShotsFired == 0 && agent.GetComponent<EnemyAI>().health == 30f, $"Unpowered turret doesn't fire (powered {gun.Powered}, shots {gun.ShotsFired})");
            Check(gun.InteractPrompt.Contains("no power"), $"Its prompt says so: '{gun.InteractPrompt}'");

            // A wall between. It takes an ordinary solid block, no special cases.
            power.Output = 50f;
            GridTick();
            GameObject wall = Block("ProbeLosWall", spot + new Vector3(0f, 2f, 4f), new Vector3(8f, 4f, 0.3f));
            yield return new WaitForSeconds(4f);
            Check(gun.ShotsFired == 0, $"A wall blocks line of sight (shots {gun.ShotsFired}, target {(gun.Target != null ? gun.Target.name : "none")})");

            // The player between: must not be hit, and must not fire "through" them.
            wall.SetActive(false);
            Destroy(wall);
            placed.Remove(wall);
            Teleport(spot + Vector3.forward * 4f + Vector3.up * 0.1f);
            Physics.SyncTransforms();
            float playerHealth = stats.health;
            yield return new WaitForSeconds(3f);
            Check(stats.health == playerHealth, "The player in the line of fire takes no damage");
            Check(gun.ShotsFired == 0, $"And the turret holds fire ({gun.ShotsFired})");

            // In sight, powered, loaded.
            Teleport(spot + new Vector3(0f, 0.1f, -14f));
            playerHealth = stats.health;
            EnemyAI enemy = agent.GetComponent<EnemyAI>();
            yield return WaitUntil(() => enemy == null || enemy.IsDead, 15f, "turret");
            Check(enemy == null || enemy.IsDead, $"The turret killed the target ({gun.ShotsFired} shots, {gun.Rounds} rounds left)");
            Check(gun.Rounds == 60 - gun.ShotsFired, $"It spends one round a shot ({gun.Rounds} left after {gun.ShotsFired})");
            Check(stats.health == playerHealth, "The player took no damage");
            Check(LootBagSystem.Bags.Count == 1, "A kill by a turret drops a bag too");

            // Loading.
            gun.DebugSetRounds(10);
            inventory.SetCount(MachineGunTurretPiece.AmmoId, 100);
            gun.Interact(player.gameObject);
            Check(gun.Rounds == 60 && inventory.GetCount(MachineGunTurretPiece.AmmoId) == 50, $"E loads from the pack up to the magazine ({gun.Rounds}, {inventory.GetCount(MachineGunTurretPiece.AmmoId)} left)");
            inventory.SetCount(MachineGunTurretPiece.AmmoId, 0);
            gun.DebugSetRounds(0);
            gun.Interact(player.gameObject);
            Check(gun.Rounds == 0, "Empty pack, nothing loaded");

            var crateObj = new GameObject("ProbeAmmoCrate");
            crateObj.transform.position = spot + Vector3.back * 4f;
            ProbeStore crate = crateObj.AddComponent<ProbeStore>();
            crate.Add(MachineGunTurretPiece.AmmoId, 25);
            placed.Add(crateObj);
            yield return new WaitForSeconds(3f);
            Check(gun.Rounds == 25 && crate.GetCount(MachineGunTurretPiece.AmmoId) == 0, $"Auto-loads from a crate within 8 m ({gun.Rounds} rounds, {crate.GetCount(MachineGunTurretPiece.AmmoId)} left in the crate)");

            // Saved rounds.
            string saved = gun.CapturePieceState();
            gun.DebugSetRounds(0);
            gun.RestorePieceState(saved);
            Check(gun.Rounds == 25, "Rounds are saved per turret");
            gun.RestorePieceState(null);
            Check(gun.Rounds == 25, "A missing state leaves the turret as it is");
        }

        private IEnumerator CheckLaser()
        {
            Vector3 spot = YardSpot + new Vector3(0f, -0.1f, -8f);
            Teleport(YardSpot + new Vector3(0f, 0f, 18f));
            LaserTurretPiece laser = Piece<LaserTurretPiece>("ProbeLaser", spot, new Vector3(1f, 1.6f, 1f));
            ProbePower power = Power(spot + Vector3.right * 2f, 0.1f);
            GridTick();
            RaidAgent agent = Held(RaidVariant.Raider, spot + Vector3.forward * 8f);
            EnemyAI enemy = agent.GetComponent<EnemyAI>();
            yield return new WaitForSeconds(3f);
            Check(!laser.Firing && enemy.health == 30f, $"Not enough power for the 2 kW beam: it doesn't fire (powered {laser.Powered}, demand {laser.PowerDemand})");

            power.Output = 50f;
            GridTick();
            yield return new WaitForSeconds(2f);
            Check(laser.DamageDealt > 0f, $"With 2 kW it fires ({laser.DamageDealt:F1} damage, heat output {laser.HeatOutput})");
            Check(!laser.Firing || (laser.PowerDemand == 2f && laser.HeatOutput == 6f), "It draws 2 kW and makes 6 heat while firing");
            float playerHealth = stats.health;

            yield return WaitUntil(() => enemy == null || enemy.IsDead, 10f, "laser");
            Check(enemy == null || enemy.IsDead, "The beam killed the target");
            yield return new WaitForSeconds(1f);
            Check(!laser.Firing && laser.PowerDemand == 0.2f && laser.HeatOutput == 0f, $"Idle: 0.2 kW, no heat ({laser.PowerDemand}, {laser.HeatOutput})");
            Check(stats.health == playerHealth, "The player took no damage");

            agent = Held(RaidVariant.Raider, spot + Vector3.forward * 8f);
            enemy = agent.GetComponent<EnemyAI>();
            PowerGrid.RestoreHeat(spot, PowerGrid.OverheatAt);
            GridTick();
            yield return new WaitForSeconds(1f);
            float hp = enemy.health;
            PowerNetwork network = PowerGrid.NetworkAt(spot);
            Check(network != null && network.Overheated, $"The network is overheated (heat {(network != null ? network.Heat : -1f):F0})");
            yield return new WaitForSeconds(1f);
            Check(laser.Overheated && !laser.Firing && enemy.health == hp, $"An overheated network shuts the laser off (firing {laser.Firing}, hp {enemy.health:F0}). Prompt: '{laser.InteractPrompt}'");
        }

        // ---------- Traps ----------

        private IEnumerator CheckTraps()
        {
            // On the raiders' own way in, in front of the stair head, clear of the pieces the turret tests left behind.
            Vector3 spot = new Vector3(72f, 6f, -242.5f);
            // Measure the real floor so the trap lies on it.
            Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 6f, ~0, QueryTriggerInteraction.Ignore);
            spot = ground.point;
            SpikeTrapPiece spike = Piece<SpikeTrapPiece>("ProbeSpike", spot, new Vector3(2f, 0.25f, 2f));
            Check(spike.GetComponents<Collider>().Count(c => !c.isTrigger) == 1 && spike.GetComponents<BoxCollider>().Where(c => !c.isTrigger).All(c => c.size.y <= 0.05f), "The trap's solid part is only a thin plate");

            // The player: a real walk across it.
            Teleport(spot + new Vector3(0f, 0.1f, -3f));
            yield return new WaitForSeconds(0.5f);
            float z = player.transform.position.z;
            float playerHealth = stats.health;
            for (float until = Time.time + 6f; Time.time < until && player.transform.position.z < spot.z + 2.5f;)
            {
                body.Move(Vector3.forward * 3f * Time.deltaTime + Vector3.down * 2f * Time.deltaTime);
                yield return null;
            }
            Check(player.transform.position.z >= spot.z + 2.4f, $"The player walked across the spike trap (z {z:F1} to {player.transform.position.z:F1}, trap at {spot.z:F1})");
            Teleport(spot + Vector3.up * 0.1f);
            yield return new WaitForSeconds(2.2f);
            Check(stats.health == playerHealth && spike.HitsDealt == 0, $"Standing on it does nothing to the player (hits {spike.HitsDealt})");

            // A raider: it can walk onto it, it hurts and slows.
            Teleport(spot + new Vector3(12f, 0.1f, 0f));
            RaidAgent walker = director.DebugSpawn(RaidVariant.Brute, spot + new Vector3(0f, 0f, -4f));
            EnemyAI enemy = walker.GetComponent<EnemyAI>();
            bool slowed = false;
            float maxZ = walker.transform.position.z;
            yield return WaitUntil(() =>
            {
                if (enemy == null) return true;
                slowed |= enemy.SpeedFactor < 1f;
                maxZ = Mathf.Max(maxZ, walker.transform.position.z);
                return spike.HitsDealt >= 1;
            }, 20f, "spike");
            Check(spike.HitsDealt >= 1, $"A raider on the trap gets hurt (hits {spike.HitsDealt}, hp {(enemy != null ? enemy.health : 0f):F0}, furthest z {maxZ:F1}, trap at {spot.z:F1})");
            Check(slowed, "And slowed");
            StructureHealth wear = spike.GetComponent<StructureHealth>();
            Check(wear.Health < wear.maxHealth, $"The trap wears out as it works ({wear.Health:F2} of {wear.maxHealth:F0})");
            Clean();
            yield return null;

            // Shock.
            Teleport(spot + new Vector3(12f, 0.1f, 0f));
            ShockTrapPiece shock = Piece<ShockTrapPiece>("ProbeShock", spot, new Vector3(2f, 0.2f, 2f));
            ProbePower power = Power(spot + Vector3.right * 2f);
            power.Output = 0f;
            GridTick();
            RaidAgent victim = Held(RaidVariant.Brute, spot);
            enemy = victim.GetComponent<EnemyAI>();
            yield return new WaitForSeconds(2f);
            Check(shock.Fired == 0 && enemy.health == enemy.maxHealth, "An unpowered shock trap does nothing");
            power.Output = 50f;
            GridTick();
            yield return new WaitForSeconds(1f);
            Check(shock.Fired == 1 && enemy.health == enemy.maxHealth - 25f, $"Powered, it fires once: 25 damage ({enemy.health:F0}/{enemy.maxHealth:F0}), {shock.Fired} shots");
            yield return new WaitForSeconds(2.5f);
            Check(shock.Fired == 1, $"Not during its 5 s cooldown ({shock.Fired})");
            yield return new WaitForSeconds(3f);
            Check(shock.Fired == 2, $"And again after it ({shock.Fired})");
            Clean();
            yield return null;

            // Stunned means stopped.
            shock = Piece<ShockTrapPiece>("ProbeShock", spot, new Vector3(2f, 0.2f, 2f));
            power = Power(spot + Vector3.right * 2f);
            GridTick();
            Teleport(spot + new Vector3(0f, 0.1f, 8f));
            walker = director.DebugSpawn(RaidVariant.Brute, spot + new Vector3(0f, 0f, -3f));
            enemy = walker.GetComponent<EnemyAI>();
            bool stunned = false;
            Vector3 stunAt = Vector3.zero;
            yield return WaitUntil(() =>
            {
                if (enemy.IsStunned && !stunned) { stunned = true; stunAt = walker.transform.position; }
                return stunned;
            }, 15f, "shock-walk");
            Check(stunned, "A raider walking over it is stunned");
            yield return new WaitForSeconds(1f);
            Check(Vector3.Distance(walker.transform.position, stunAt) < 0.3f, $"And stops ({Vector3.Distance(walker.transform.position, stunAt):F2} m moved)");
            Check(stats.health == stats.maxHealth, "The player is untouched by traps");
        }

        // ---------- Loot ----------

        private IEnumerator CheckDeathLoot()
        {
            Teleport(YardSpot);
            yield return null;
            Check(LootBagSystem.Bags.Count == 0, "No bags to begin with");

            // Exactly one bag, however often the callback runs.
            RaidAgent agent = Held(RaidVariant.Raider, YardSpot + new Vector3(4f, 0f, 0f));
            EnemyAI enemy = agent.GetComponent<EnemyAI>();
            enemy.TakeDamage(10000f);
            Check(LootBagSystem.Bags.Count == 1, $"Death drops one bag ({LootBagSystem.Bags.Count})");
            Check(director.NotifyDeath(enemy) == null && director.NotifyDeath(enemy) == null, "A repeated death callback drops nothing");
            enemy.TakeDamage(10000f);
            Check(LootBagSystem.Bags.Count == 1, $"A repeated hit on the dead drops nothing ({LootBagSystem.Bags.Count})");
            LootBag bag = LootBagSystem.Bags[0];
            Check(bag.Contents.Count > 0, "It holds something: " + Describe(bag));
            Check(bag.Contents.Any(p => p.Key == "scrap_metal"), "An ordinary kill always pays in materials");
            Check(Mathf.Abs(bag.transform.position.y - 6.02f) < 0.2f, $"It lies on the ground (y {bag.transform.position.y:F2})");

            yield return null;
            string before = Describe(bag);

            // Save and load don't multiply or re-roll it.
            string save = director.CaptureState();
            director.RestoreState(save);
            director.RestoreState(save);
            Check(LootBagSystem.Bags.Count == 1 && Describe(LootBagSystem.Bags[0]) == before, $"Capture and restore keep exactly that bag ({LootBagSystem.Bags.Count}): {Describe(LootBagSystem.Bags[0])}");
            Check(FindObjectsByType<LootBag>(FindObjectsSortMode.None).Length == 1, "One object in the scene, too");

            // Taking.
            bag = LootBagSystem.Bags[0];
            int scrap = inventory.GetCount("scrap_metal");
            int inBag = bag.Contents.First(p => p.Key == "scrap_metal").Value;
            bag.Interact(player.gameObject);
            bag.Interact(player.gameObject);
            Check(inventory.GetCount("scrap_metal") == scrap + inBag, $"Taking it pays out once ({scrap} to {inventory.GetCount("scrap_metal")})");
            Check(LootBagSystem.Bags.Count == 0, "And removes it");
            string taken = director.CaptureState();
            director.RestoreState(taken);
            Check(LootBagSystem.Bags.Count == 0, "A taken bag stays gone after save and load");
            director.RestoreState(save);
            Check(LootBagSystem.Bags.Count == 1, "Loading the save from before taking it brings it back");
            director.RestoreState(taken);

            // A world patrol, through the real encounter slot.
            var slotObject = new GameObject("ProbeSlot");
            slotObject.transform.position = YardSpot + new Vector3(4f, 1.1f, 0f);
            placed.Add(slotObject);
            GameObject actor = Instantiate(ProceduralWorld.Active.assets.raiderPrefab, slotObject.transform, false);
            actor.SetActive(true);
            ChunkEncounterSlot slot = slotObject.AddComponent<ChunkEncounterSlot>();
            EnemyAI patrol = actor.GetComponent<EnemyAI>();
            slot.Initialize(patrol, ProceduralWorld.Active.Seed);
            Check(!patrol.RaidMode, "A patrol has no brain");
            patrol.TakeDamage(10000f);
            Check(slot.Defeated && LootBagSystem.Bags.Count == 1, $"A patrol's death drops one bag too (defeated {slot.Defeated}, bags {LootBagSystem.Bags.Count})");
            slot.Restore(true, 0f);
            slot.Restore(true, 0f);
            Check(LootBagSystem.Bags.Count == 1, "Restoring a defeated encounter drops nothing");

            // A chunk coming back with a saved defeat: the actor is removed and no bag appears.
            var slotObject2 = new GameObject("ProbeSlot2");
            slotObject2.transform.position = YardSpot + new Vector3(4f, 1.1f, 3f);
            placed.Add(slotObject2);
            GameObject actor2 = Instantiate(ProceduralWorld.Active.assets.raiderPrefab, slotObject2.transform, false);
            actor2.SetActive(true);
            ChunkEncounterSlot slot2 = slotObject2.AddComponent<ChunkEncounterSlot>();
            slot2.Initialize(actor2.GetComponent<EnemyAI>(), ProceduralWorld.Active.Seed);
            slot2.Restore(true, 0f);
            Check(LootBagSystem.Bags.Count == 1 && slot2.Actor == null, "Revisiting a defeated patrol's chunk creates no second bag");

            // Tables.
            var elite = LootBagSystem.Roll(LootTier.Elite, 123, 7);
            Check(elite.Any(p => p.Key == "electronics" || p.Key == "gunpowder" || p.Key == "capacitor") && elite.Any(p => p.Key == "ammo_rifle"), "Elite: a rare component and ammunition: " + string.Join(", ", elite));
            var boss = LootBagSystem.Roll(LootTier.Warlord, 123, 8);
            Check(boss.Any(p => p.Key == "expedition_parts" && p.Value == 3) && boss.Any(p => p.Key == "energy_cell" && p.Value == 4) && boss.Any(p => p.Key == "circuit_board" && p.Value == 3), "Warlord: the guaranteed reward: " + string.Join(", ", boss));
            Check(LootBagSystem.Roll(LootTier.Ordinary, 123, 9).SequenceEqual(LootBagSystem.Roll(LootTier.Ordinary, 123, 9)), "A roll is reproducible from seed and id");
            int empty = 0;
            for (int i = 0; i < 300; i++) if (LootBagSystem.Roll(LootTier.Ordinary, 77, i).Count == 0) empty++;
            Check(empty == 0, "No ordinary roll is ever empty");
            Check(RaidVariantSpec.Of(RaidVariant.Gunner).Loot == LootTier.Elite && RaidVariantSpec.Of(RaidVariant.Brute).Loot == LootTier.Elite && RaidVariantSpec.Of(RaidVariant.Warlord).Loot == LootTier.Warlord, "Tiers by variant");

            // Over the cap, it logs and removes the oldest rather than growing forever.
            LootBagSystem.Clear();
            for (int i = 0; i < LootBagSystem.MaxBags + 3; i++)
                LootBagSystem.Spawn(YardSpot, 1000 + i, LootTier.Ordinary, new[] { new KeyValuePair<string, int>("nails", 1) });
            Check(LootBagSystem.Bags.Count == LootBagSystem.MaxBags && LootBagSystem.Bags[0].Id == 1003, $"Capped at {LootBagSystem.MaxBags}, oldest removed ({LootBagSystem.Bags.Count}, oldest id {LootBagSystem.Bags[0].Id})");
        }

        private static string Describe(LootBag bag) => string.Join(", ", bag.Contents.Select(p => p.Value + " " + p.Key));

        // ---------- The clock ----------

        private IEnumerator CheckWarningAndAlarm()
        {
            Teleport(RoomSpot);
            GameAudio.PlayLog = new List<string>();
            director.DebugSetCountdown(66f);
            yield return new WaitForSeconds(2f);
            Check(!director.WarningActive && director.BannerText == "", $"No warning yet at {director.Countdown:F0} s");
            director.DebugSetCountdown(62f);
            yield return WaitUntil(() => director.WarningActive, 6f, "warning");
            yield return null;
            Check(director.WarningActive && director.BannerText.StartsWith("RAID INBOUND "), $"Warning at 60 s: '{director.BannerText}'");
            Check(GameAudio.PlayLog.Contains(Sfx.RaidHorn), "With the horn");

            Clean();
            yield return null;
            Vector3 spot = YardSpot + new Vector3(0f, -0.1f, -8f);
            AlarmPiece alarm = Piece<AlarmPiece>("ProbeAlarm", spot, new Vector3(0.5f, 1.8f, 0.5f));
            Power(spot + Vector3.right);
            GridTick();
            Check(alarm.Powered, "The alarm has power");
            director.DebugSetCountdown(115f);
            yield return WaitUntil(() => director.WarningActive, 3f, "alarm-warning");
            Check(director.WarningActive && director.Countdown > 100f, $"A powered alarm warns at 120 s (warned at {director.Countdown:F0} s left)");

            door.RestoreState(true);
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            Teleport(RoomSpot);
            GameAudio.PlayLog.Clear();
            director.DebugStartRaid(1);
            Check(!door.IsOpen, "A raid beginning shuts the open door");
            Check(GameAudio.PlayLog.Contains(Sfx.AlarmKlaxon), "And sounds the klaxon");
            Clean();
            yield return null;

            // Someone in the doorway: the door stays open.
            alarm = Piece<AlarmPiece>("ProbeAlarm", spot, new Vector3(0.5f, 1.8f, 0.5f));
            Power(spot + Vector3.right);
            GridTick();
            door.RestoreState(true);
            yield return new WaitForFixedUpdate();
            Teleport(new Vector3(72f, 0.1f, -223.8f));
            yield return new WaitForFixedUpdate();
            director.DebugStartRaid(1);
            Check(door.IsOpen, "It doesn't shut on someone in the doorway");
            GameAudio.PlayLog = null;
        }

        private IEnumerator CheckFarPlayer()
        {
            player.enabled = false;
            body.enabled = false;
            player.transform.position = FarAway;
            Physics.SyncTransforms();
            director.DebugSetCountdown(0.5f);
            yield return WaitUntil(() => director.CurrentPhase != RaidDirector.Phase.Countdown, 4f, "far");
            yield return new WaitForSeconds(3f);
            Check(director.CurrentPhase == RaidDirector.Phase.Pending && director.RaidersLeft == 0, $"Player {Vector3.Distance(player.transform.position, RaidRoute.StairHeadCenter):F0} m away: the raid waits ({director.CurrentPhase}, {director.RaidersLeft} raiders)");
            int number = director.RaidNumber;

            string saved = director.CaptureState();
            director.RestoreState(saved);
            Check(director.CurrentPhase == RaidDirector.Phase.Pending && director.RaidNumber == number, "A load keeps a waiting raid");

            body.enabled = true;
            player.enabled = true;
            Teleport(RoomSpot);
            yield return new WaitForSeconds(2f);
            Check(director.CurrentPhase == RaidDirector.Phase.Pending && director.BannerText.StartsWith("RAID INBOUND"), $"Back home: a short warning first ('{director.BannerText}')");
            yield return WaitUntil(() => director.CurrentPhase == RaidDirector.Phase.Active, RaidDirector.ReturnFuseSeconds + 3f, "return");
            Check(director.CurrentPhase == RaidDirector.Phase.Active && director.RaidersLeft > 0 && director.RaidNumber == number, $"Then it fires ({director.CurrentPhase}, {director.RaidersLeft} raiders, raid {director.RaidNumber})");
        }

        private IEnumerator CheckRestoreDuringRaid()
        {
            Teleport(RoomSpot);
            door.RestoreState(false);
            director.DebugStartRaid(4);
            int count = director.RaidersLeft;
            Check(count > 0, $"Raid 4 has {count} raiders");
            yield return new WaitForSeconds(6f);
            EnemyAI hurt = director.Actors[0].GetComponent<EnemyAI>();
            hurt.TakeDamage(5f);
            float hurtHealth = hurt.health;
            Vector3 position = hurt.transform.position;
            RaidVariant variant = director.Actors[0].Variant;
            string save = director.CaptureState();
            Check(Live() == count, $"Live raid actors before: {Live()}");
            yield return new WaitForSeconds(3f);
            director.RestoreState(save);
            director.RestoreState(save);
            Check(director.RaidersLeft == count && Live() == count, $"Restoring during a raid doesn't double it ({director.RaidersLeft}, {Live()} live, expected {count})");
            Check(director.CurrentPhase == RaidDirector.Phase.Active && director.RaidNumber == 4, "It is the same raid");
            RaidAgent same = director.Actors.FirstOrDefault(a => a.Variant == variant && Vector3.Distance(a.transform.position, position) < 0.3f);
            Check(same != null && Mathf.Abs(same.GetComponent<EnemyAI>().health - hurtHealth) < 0.01f, $"With saved position and health ({(same != null ? same.GetComponent<EnemyAI>().health : -1f):F1}, expected {hurtHealth:F1})");
            yield return new WaitForSeconds(4f);
            Check(director.Actors.Any(a => Vector3.Distance(a.transform.position, position) > 1f), "They carry on walking: " + string.Join("; ", director.Actors.Select(a => a.State)));

            director.RestoreState(null);
            Check(Live() == 0 && director.CurrentPhase == RaidDirector.Phase.Countdown && director.RaidNumber == 1, "Loading a save from before the raid system removes the raid");
        }

        private int Live() => FindObjectsByType<RaidAgent>(FindObjectsSortMode.None).Count(a => a.gameObject.activeInHierarchy);

        private IEnumerator CheckRaidEnds()
        {
            Teleport(RoomSpot);
            int bags = LootBagSystem.Bags.Count;
            director.DebugStartRaid(5);
            Check(director.Actors.Any(a => a.Variant == RaidVariant.Warlord), "Raid 5 has a Warlord");
            int count = director.RaidersLeft;
            yield return null;
            Check(director.BannerText == $"RAID 5: {count} left", $"Banner '{director.BannerText}'");
            foreach (RaidAgent agent in director.Actors.ToArray()) agent.GetComponent<EnemyAI>().TakeDamage(100000f);
            Check(LootBagSystem.Bags.Count == bags + count, $"Every raider dropped a bag ({LootBagSystem.Bags.Count - bags} of {count})");
            Check(LootBagSystem.Bags.Any(b => b.Tier == LootTier.Warlord && b.Contents.Any(p => p.Key == "expedition_parts" && p.Value == 3)), "One is the Warlord's cache");
            yield return null;
            yield return null;
            Check(director.CurrentPhase == RaidDirector.Phase.Countdown && director.RaidNumber == 6, $"The raid ended, next is {director.RaidNumber}");
            Check(Mathf.Abs(director.Countdown - RaidDirector.IntervalAfter(5)) < 2f, $"The next countdown is running ({director.Countdown:F0} s)");
            Check(director.BannerText == "", "The banner is gone");

            // A dead player pauses it all.
            director.DebugSetCountdown(50f);
            float clock = director.Countdown;
            float health = stats.health;
            stats.TakeDamage(stats.health + 1f);
            yield return new WaitForSecondsRealtime(1f); // the death screen may pause game time
            Check(stats.IsDead && Mathf.Abs(director.Countdown - clock) < 0.01f, $"A dead player stops the clock ({clock:F1} to {director.Countdown:F1})");
            stats.RestoreVitals(100f, 100f, health);
            Time.timeScale = Speedup;
        }

        // ---------- Patrols ----------

        private IEnumerator CheckPatrolUnchanged()
        {
            Teleport(YardSpot);
            GameObject actor = Instantiate(ProceduralWorld.Active.assets.raiderPrefab, YardSpot + new Vector3(0f, 1.1f, -9f), Quaternion.identity);
            actor.SetActive(true);
            placed.Add(actor);
            EnemyAI patrol = actor.GetComponent<EnemyAI>();
            Teleport(YardSpot + new Vector3(0f, 0f, 16f));
            yield return new WaitForSeconds(2f);
            Check(!patrol.IsChasing && !patrol.RaidMode, "A patrol outside its detection radius is idle");
            Vector3 idle = actor.transform.position;
            yield return new WaitForSeconds(1f);
            Check(Vector3.Distance(idle, actor.transform.position) < 0.2f, "And doesn't wander without a slot driving it");
            Teleport(YardSpot + new Vector3(0f, 0f, -3f));
            float health = stats.health;
            yield return WaitUntil(() => stats.health < health, 8f, "patrol");
            Check(patrol.IsChasing && stats.health < health, $"Inside it, it chases and hits ({health:F0} to {stats.health:F0})");
            Check(patrol.moveSpeed == 2.5f && patrol.maxHealth == 30f && actor.transform.localScale == Vector3.one, "Stats and scale are the prefab's own");
            Check(actor.GetComponent<RaidAgent>() == null, "It isn't a raid actor");
        }

        // ---------- Stand-ins ----------

        private sealed class ProbePower : MonoBehaviour, IPowerSource
        {
            public float Output;
            public Vector3 PowerPosition => transform.position;
            public float PowerOutput => Output;
            public float HeatOutput => 0f;
            public string PowerName => "Probe";
            private void OnEnable() => PowerGrid.Register(this);
            private void OnDisable() => PowerGrid.Unregister(this);
        }

        private sealed class ProbeStore : MonoBehaviour, IItemStore
        {
            private readonly Dictionary<string, int> items = new Dictionary<string, int>();
            public Vector3 StorePosition => transform.position;
            public int GetCount(string id) => items.TryGetValue(id, out int n) ? n : 0;
            public void Add(string id, int amount) { items[id] = GetCount(id) + amount; }
            public bool TryTake(string id, int amount)
            {
                if (GetCount(id) < amount) return false;
                items[id] -= amount;
                return true;
            }
            public IReadOnlyDictionary<string, int> Contents => items;
            private void OnEnable() => StorageRegistry.Register(this);
            private void OnDisable() => StorageRegistry.Unregister(this);
        }
    }
}
