using System;
using System.Collections;
using System.Linq;
using SheltreSim.Enemies;
using SheltreSim.Player;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>Run with --raider-stair-probe. Verifies a real EnemyAI walks up generated house and town-building stairs.</summary>
    public sealed class RaiderStairProbe : MonoBehaviour
    {
        private int failures;
        private FirstPersonController player;
        private CharacterController playerBody;
        private Vector3 savedPlayerPosition;
        private Quaternion savedPlayerRotation;
        private float savedTimeScale;
        private GameObject actor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--raider-stair-probe") >= 0)
                new GameObject(nameof(RaiderStairProbe)).AddComponent<RaiderStairProbe>();
        }

        private IEnumerator Start()
        {
            yield return null;
            player = FindFirstObjectByType<FirstPersonController>();
            ProceduralWorld world = ProceduralWorld.Active;
            if (player == null || world == null || world.assets == null || world.assets.raiderPrefab == null)
            {
                Fail("player, procedural world, or raider prefab is missing");
                Debug.LogError("[RaiderStairProbe] CHECKS FAILED");
                Application.Quit(1);
                yield break;
            }

            playerBody = player.GetComponent<CharacterController>();
            savedPlayerPosition = player.transform.position;
            savedPlayerRotation = player.transform.rotation;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 2f;

            // Run the one-storey house first, then a multi-storey town building. Search near the
            // current world location so streaming does not unload the site while its raider walks.
            BuildingInterior house = FindBuilding(world, false);
            if (house == null) Fail("could not find a generated two-storey suburb house nearby");
            else yield return ClimbSite(world, house, false);

            BuildingInterior block = FindBuilding(world, true);
            if (block == null) Fail("could not find a generated multi-storey town building nearby");
            else yield return ClimbSite(world, block, true);

            RestorePlayer();
            Debug.Log(failures == 0 ? "[RaiderStairProbe] ALL CHECKS PASSED" : $"[RaiderStairProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private IEnumerator ClimbSite(ProceduralWorld world, BuildingInterior site, bool townBuilding)
        {
            if (!TryGetEndpoints(site, townBuilding, out Vector3 start, out Vector3 target, out float targetFloor))
            {
                Fail(site.name + " has no usable generated stair colliders");
                yield break;
            }

            TeleportPlayer(target + Vector3.up * 0.1f);
            CharacterController prefabBody = world.assets.raiderPrefab.GetComponent<CharacterController>();
            float halfHeight = prefabBody != null
                ? prefabBody.height * Mathf.Abs(world.assets.raiderPrefab.transform.lossyScale.y) * 0.5f
                : 1f;
            actor = Instantiate(world.assets.raiderPrefab, start + Vector3.up * (halfHeight + 0.05f), Quaternion.identity);
            actor.name = "RaiderStairProbeActor_" + site.name;
            EnemyAI enemy = actor.GetComponent<EnemyAI>();
            if (enemy == null)
            {
                Fail(site.name + " raider prefab has no EnemyAI");
                Destroy(actor);
                actor = null;
                yield break;
            }

            yield return null;
            bool hasWaypoint = BuildingStairNavigation.TryGetNextWaypoint(enemy.GetEntityId(), enemy.FeetPosition, player.transform.position, out Vector3 initialWaypoint);
            Debug.Log($"[RaiderStairProbe] {site.name}: initial feet={enemy.FeetPosition:F2} target={player.transform.position:F2} next={(hasWaypoint ? initialWaypoint.ToString("F2") : "none")}");
            Check(hasWaypoint, site.name + " has a generated stair waypoint between raider and player");
            enemy.TakeDamage(0.01f); // Starts the normal sight-chase state even though the floor blocks line of sight.
            float deadline = Time.realtimeSinceStartup + 35f;
            float nextTrace = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < deadline && enemy != null && enemy.FeetPosition.y < targetFloor - 0.35f)
            {
                if (Time.realtimeSinceStartup >= nextTrace)
                {
                    nextTrace = Time.realtimeSinceStartup + 1f;
                    bool guiding = BuildingStairNavigation.TryGetNextWaypoint(enemy.GetEntityId(), enemy.FeetPosition, player.transform.position, out Vector3 next);
                    Debug.Log($"[RaiderStairProbe] {site.name}: feet={enemy.FeetPosition:F2} next={(guiding ? next.ToString("F2") : "none")} player={player.transform.position:F2}");
                }
                yield return new WaitForSecondsRealtime(0.25f);
            }

            bool reached = enemy != null && enemy.FeetPosition.y >= targetFloor - 0.35f;
            Debug.Log($"[RaiderStairProbe] {site.name}: reached={reached}, raider feet={(enemy != null ? enemy.FeetPosition.ToString("F2") : "destroyed")}, target floor y={targetFloor:F2}");
            Check(reached, (townBuilding ? "town building" : "house") + " raider climbs to its upper occupied floor");
            if (actor != null) Destroy(actor);
            actor = null;
            yield return new WaitForSecondsRealtime(0.1f);
        }

        private static BuildingInterior FindBuilding(ProceduralWorld world, bool town)
        {
            Vector2Int center = WorldLayout.Coordinate(FindFirstObjectByType<FirstPersonController>().transform.position);
            BuildingInterior tallest = null;
            int tallestFloorGroups = town ? 2 : 1;
            for (int radius = 0; radius <= 4; radius++)
            {
                for (int z = -radius; z <= radius; z++)
                for (int x = -radius; x <= radius; x++)
                {
                    if (radius > 0 && Mathf.Abs(x) != radius && Mathf.Abs(z) != radius) continue;
                    Vector2Int coordinate = center + new Vector2Int(x, z);
                    if (town ? !WorldLayout.IsTownChunk(coordinate, world.Seed) : !WorldLayout.IsSuburbChunk(coordinate, world.Seed)) continue;
                    if (ProceduralLandmarks.HasLandmark(coordinate, world.Seed)) continue;
                    GameObject chunk = world.EnsureChunk(coordinate);
                    foreach (BuildingInterior building in chunk.GetComponentsInChildren<BuildingInterior>(true))
                    {
                        if (town && !building.name.StartsWith("Building_", StringComparison.Ordinal)) continue;
                        if (!town && !building.name.StartsWith("House_", StringComparison.Ordinal)) continue;
                        int floors = building.transform.Cast<Transform>().Count(t => t.name.StartsWith("Floor_", StringComparison.Ordinal));
                        if (town)
                        {
                            // Building height and lot plan are seeded per location. Exercise
                            // the longest real stair stack in the search area so a nearby
                            // short office cannot hide a taller block's route failure.
                            if (floors > tallestFloorGroups)
                            {
                                tallest = building;
                                tallestFloorGroups = floors;
                            }
                        }
                        else if (floors >= 2) return building;
                    }
                }
            }
            if (tallest != null)
                Debug.Log($"[RaiderStairProbe] selected tallest town building {tallest.name} with {tallestFloorGroups} floor groups");
            return tallest;
        }

        private static bool TryGetEndpoints(BuildingInterior site, bool townBuilding,
            out Vector3 start, out Vector3 target, out float targetFloor)
        {
            start = target = default;
            targetFloor = float.NegativeInfinity;
            Vector3 lowest = default, highest = default;
            Vector3 lowestDirection = default, highestDirection = default, upperFlightADirection = default;
            float lowestY = float.PositiveInfinity, highestY = float.NegativeInfinity;
            Vector3 upperFlightA = default;
            float upperFlightAY = float.NegativeInfinity;

            foreach (BoxCollider ramp in site.GetComponentsInChildren<BoxCollider>(true))
            {
                if (ramp == null || ramp.name != "Ramp") continue;
                Vector3 halfDepth = Vector3.forward * (ramp.size.z * 0.46f);
                Vector3 top = Vector3.up * (ramp.size.y * 0.5f);
                Vector3 a = ramp.transform.TransformPoint(ramp.center + top - halfDepth);
                Vector3 b = ramp.transform.TransformPoint(ramp.center + top + halfDepth);
                Vector3 low = a.y <= b.y ? a : b;
                Vector3 high = a.y <= b.y ? b : a;
                Vector3 direction = high - low;
                direction.y = 0f;
                direction.Normalize();
                if (low.y < lowestY) { lowestY = low.y; lowest = low; lowestDirection = direction; }
                if (high.y > highestY) { highestY = high.y; highest = high; highestDirection = direction; }
                if (townBuilding && ramp.transform.parent != null && ramp.transform.parent.name == "FlightA" && low.y > upperFlightAY)
                {
                    upperFlightAY = low.y;
                    upperFlightA = low;
                    upperFlightADirection = direction;
                }
            }

            if (float.IsPositiveInfinity(lowestY) || float.IsNegativeInfinity(highestY)) return false;
            if (lowestDirection.sqrMagnitude < 0.01f || highestDirection.sqrMagnitude < 0.01f) return false;
            start = lowest - lowestDirection * 0.8f;
            start.y = lowest.y;

            if (townBuilding)
            {
                if (float.IsNegativeInfinity(upperFlightAY)) return false;
                target = upperFlightA - upperFlightADirection * 0.8f;
                target.y = upperFlightAY;
                targetFloor = upperFlightAY;
            }
            else
            {
                target = highest + highestDirection * 0.7f;
                targetFloor = highest.y;
            }
            return targetFloor > start.y + 1.5f;
        }

        private void TeleportPlayer(Vector3 position)
        {
            if (playerBody != null) playerBody.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.identity);
            if (playerBody != null) playerBody.enabled = true;
            Physics.SyncTransforms();
        }

        private void RestorePlayer()
        {
            if (actor != null) Destroy(actor);
            actor = null;
            if (player != null && playerBody != null)
            {
                playerBody.enabled = false;
                player.transform.SetPositionAndRotation(savedPlayerPosition, savedPlayerRotation);
                playerBody.enabled = true;
            }
            Time.timeScale = savedTimeScale;
            Physics.SyncTransforms();
        }

        private void Check(bool condition, string message)
        {
            Debug.Log($"[RaiderStairProbe] {(condition ? "PASS" : "FAIL")}: {message}");
            if (!condition) failures++;
        }

        private void Fail(string message)
        {
            failures++;
            Debug.LogError("[RaiderStairProbe] FAIL: " + message);
        }
    }
}
