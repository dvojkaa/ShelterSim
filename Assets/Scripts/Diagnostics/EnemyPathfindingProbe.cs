using System;
using System.Collections;
using System.Reflection;
using SheltreSim.Enemies;
using SheltreSim.Raids;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>Exercises the on-demand enemy route around a solid wall using real CharacterController movement.</summary>
    public sealed class EnemyPathfindingProbe : MonoBehaviour
    {
        private int failures;
        private GameObject floor, wall, leftWall, rightWall, actorObject;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--enemy-pathfinding-probe") >= 0)
                new GameObject("EnemyPathfindingProbe").AddComponent<EnemyPathfindingProbe>();
        }

        private IEnumerator Start()
        {
            // Let the opening modal finish its own Start, then restore time for this isolated drive.
            yield return null;
            yield return null;
            Time.timeScale = 1f;
            const float y = 50f;
            Vector3 origin = new Vector3(10000f, y, 10000f);
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "PathProbeFloor";
            floor.transform.position = origin + Vector3.down * .5f;
            floor.transform.localScale = new Vector3(50f, 1f, 50f);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "PathProbeWall";
            // A U-shaped enclosure: the direct north route is closed, but a valid path requires
            // backing out through the south opening and going around one of its long side walls.
            wall.transform.position = origin + Vector3.forward * 2f;
            wall.transform.localScale = new Vector3(4.8f, 2.6f, .35f);
            leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWall.name = "PathProbeLeftWall";
            leftWall.transform.position = origin + Vector3.left * 2.4f + Vector3.back * .5f;
            leftWall.transform.localScale = new Vector3(.35f, 2.6f, 5f);
            rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.name = "PathProbeRightWall";
            rightWall.transform.position = origin + Vector3.right * 2.4f + Vector3.back * .5f;
            rightWall.transform.localScale = new Vector3(.35f, 2.6f, 5f);
            actorObject = new GameObject("PathProbeEnemy");
            actorObject.transform.position = origin;
            CharacterController body = actorObject.AddComponent<CharacterController>();
            body.radius = .35f;
            body.height = 1.8f;
            body.center = Vector3.up * .9f;
            body.stepOffset = .3f;
            EnemyAI enemy = actorObject.AddComponent<EnemyAI>();
            enemy.gravity = -18f;
            Physics.SyncTransforms();
            yield return null;
            enemy.enabled = false; // Drive its real movement method deterministically without its ambient chase loop.

            Vector3 target = origin + Vector3.forward * 8f;
            for (int i = 0; i < 12000 && enemy.BlockedSecondsForPathfinding < 1.25f; i++)
            {
                enemy.BrainMove(Vector3.forward * 2.5f);
                yield return null;
            }
            Debug.Log($"[EnemyPathfindingProbe] stalled at {actorObject.transform.position}; blocked timer={enemy.BlockedSecondsForPathfinding:F2}; game time={Time.time:F2}");
            bool gotDetour = enemy.TryGetPathDirection(target, out Vector3 first);
            Check(gotDetour && Vector3.Dot(first, Vector3.forward) < .95f,
                $"a stalled enemy gets a detour around the closed end of the U ({first})");

            float farthestSide = 0f;
            for (int i = 0; i < 100000 && Vector3.Distance(Flat(actorObject.transform.position - target), Vector3.zero) > 1.2f; i++)
            {
                Vector3 delta = target - actorObject.transform.position;
                Vector3 moveDirection;
                if (!enemy.TryGetPathDirection(target, out moveDirection)) moveDirection = Flat(delta).normalized;
                enemy.BrainMove(moveDirection * 2.5f);
                farthestSide = Mathf.Max(farthestSide, Mathf.Abs(actorObject.transform.position.x - origin.x));
                yield return null;
            }
            Debug.Log($"[EnemyPathfindingProbe] generic final position={actorObject.transform.position}, blocked={enemy.BlockedSecondsForPathfinding:F2}, route={EnemyPathfinding.Describe(enemy)}, time={Time.time:F2}");
            Check(Vector3.Distance(Flat(actorObject.transform.position - target), Vector3.zero) < 1.2f,
                $"the real capsule makes route progress to the far side (position {actorObject.transform.position})");
            Check(farthestSide > 2.1f, $"the route clears the wall edge ({farthestSide:F1} m lateral travel)");

            Destroy(actorObject);
            yield return null;
            actorObject = new GameObject("PathProbeRaider");
            actorObject.transform.position = origin;
            body = actorObject.AddComponent<CharacterController>();
            body.radius = .35f;
            body.height = 1.8f;
            body.center = Vector3.up * .9f;
            body.stepOffset = .3f;
            enemy = actorObject.AddComponent<EnemyAI>();
            RaidAgent raider = actorObject.AddComponent<RaidAgent>();
            raider.Setup(RaidVariant.Raider, 1, 1234);
            enemy.enabled = false;
            Physics.SyncTransforms();
            for (int i = 0; i < 12000 && enemy.BlockedSecondsForPathfinding < 1.25f; i++)
            {
                enemy.BrainMove(Vector3.forward * 2.5f);
                yield return null;
            }
            MethodInfo walk = typeof(RaidAgent).GetMethod("Walk", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(walk != null && enemy.BlockedSecondsForPathfinding >= 1.05f,
                $"raid unit reaches the same measured stall threshold ({enemy.BlockedSecondsForPathfinding:F2}s)");
            Vector3 raidTarget = origin + Vector3.forward * 8f;
            EnemyPathfinding.Forget(enemy.GetEntityId());
            bool hasRaidRoute = enemy.TryGetPathDirection(raidTarget, out Vector3 raidFirst);
            Debug.Log($"[EnemyPathfindingProbe] raid direct path={hasRaidRoute}, direction={raidFirst}, body={body.radius * actorObject.transform.lossyScale.x:F2}m; {EnemyPathfinding.LastBuildDiagnostic}");
            float raidSideTravel = 0f;
            for (int i = 0; i < 100000 && Vector3.Distance(Flat(actorObject.transform.position - raidTarget), Vector3.zero) > 1.2f; i++)
            {
                if (walk != null) walk.Invoke(raider, new object[] { raidTarget, 0f });
                else enemy.BrainMove(Vector3.zero);
                if (i == 0)
                {
                    Vector3 steering = (Vector3)typeof(RaidAgent).GetField("steering", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(raider);
                    object attack = typeof(RaidAgent).GetField("attackCollider", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(raider);
                    Debug.Log($"[EnemyPathfindingProbe] raid first steering={steering}; attack={attack}; timer={enemy.BlockedSecondsForPathfinding:F2}");
                }
                raidSideTravel = Mathf.Max(raidSideTravel, Mathf.Abs(actorObject.transform.position.x - origin.x));
                yield return null;
            }
            Debug.Log($"[EnemyPathfindingProbe] raid final position={actorObject.transform.position}, blocked={enemy.BlockedSecondsForPathfinding:F2}, route={EnemyPathfinding.Describe(enemy)}, time={Time.time:F2}");
            Check(Vector3.Distance(Flat(actorObject.transform.position - raidTarget), Vector3.zero) < 1.2f,
                $"a stalled raid unit follows the shared route to the far side ({actorObject.transform.position})");
            Check(raidSideTravel > 2.1f, $"raid route gets around the obstruction ({raidSideTravel:F1} m lateral travel)");

            Debug.Log(failures == 0 ? "[EnemyPathfindingProbe] ALL CHECKS PASSED" : $"[EnemyPathfindingProbe] {failures} CHECKS FAILED");
            Cleanup();
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private void Check(bool okay, string message)
        {
            if (okay) Debug.Log("[EnemyPathfindingProbe] PASS: " + message);
            else { failures++; Debug.LogError("[EnemyPathfindingProbe] FAIL: " + message); }
        }

        private static Vector3 Flat(Vector3 value) { value.y = 0f; return value; }

        private void Cleanup()
        {
            if (floor != null) Destroy(floor);
            if (wall != null) Destroy(wall);
            if (leftWall != null) Destroy(leftWall);
            if (rightWall != null) Destroy(rightWall);
            if (actorObject != null) Destroy(actorObject);
            Time.timeScale = 1f;
        }

        private void OnDestroy() => Cleanup();
    }
}
