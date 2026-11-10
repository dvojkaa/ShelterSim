using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SheltreSim.Player;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>Run with --building-culling-probe to verify streamed building interior renderer culling.</summary>
    public sealed class BuildingCullingProbe : MonoBehaviour
    {
        private const int TargetFloor = 2;
        private const float CullingWait = .28f;
        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--building-culling-probe") >= 0)
                new GameObject(nameof(BuildingCullingProbe)).AddComponent<BuildingCullingProbe>();
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null;

            ProceduralWorld world = ProceduralWorld.Active;
            FirstPersonController player = FindFirstObjectByType<FirstPersonController>();
            if (world == null || player == null)
            {
                Check(false, "procedural world and player exist");
                Finish();
                yield break;
            }

            BuildingInterior building = FindTestBuilding(world);
            if (building == null)
            {
                Check(false, "found a generated building with at least four interior storeys and renderers");
                Finish();
                yield break;
            }

            Transform[] floors = GetInteriorFloors(building.transform);
            Check(floors.Length > TargetFloor + 1,
                $"test building has floors on both sides of storey {TargetFloor} ({floors.Length} groups)");
            Renderer[][] renderers = floors.Select(f => f.GetComponentsInChildren<Renderer>(true)).ToArray();
            bool hasTestRenderers = renderers.Length > TargetFloor + 1 &&
                                    renderers.Take(TargetFloor + 2).All(floor => floor.Length > 0);
            Check(hasTestRenderers, "test floors have interior renderers");
            if (!hasTestRenderers)
            {
                Finish();
                yield break;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            Vector3 savedPosition = player.transform.position;
            Quaternion savedRotation = player.transform.rotation;
            bool savedControllerEnabled = controller != null && controller.enabled;
            float savedTimeScale = Time.timeScale;

            try
            {
                Time.timeScale = 1f;
                if (controller != null) controller.enabled = false;

                // At floor two, floors one through three are visible and floor zero is distant.
                SetPlayerLocal(player, building.transform, new Vector3(0f, TargetFloor * 3f + 1.4f, 0f));
                yield return WaitForCulling();
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i].Length == 0) continue;
                    // The ground floor is intentionally revealed within 25 m, even when the
                    // player is upstairs inside the building.
                    bool expected = Mathf.Abs(i - TargetFloor) <= 1 || i == 0;
                    Check(AllEnabled(renderers[i]) == expected,
                        $"inside at storey {TargetFloor}: floor {i} renderers {(expected ? "visible" : "hidden")}");
                }
                CheckFloorCollidersRemainEnabled(building.transform, "inside");

                // Outside the building's inside bounds but within 25 m, the ground floor stays visible.
                SetPlayerLocal(player, building.transform, new Vector3(24f, 1.4f, 0f));
                yield return WaitForCulling();
                Check(renderers[0].Length > 0 && AllEnabled(renderers[0]),
                    "outside within 25 m: ground-floor interior remains visible");
                Check(UpperFloorsHidden(renderers), "outside within 25 m: every upper-floor interior is hidden");
                CheckFloorCollidersRemainEnabled(building.transform, "near outside");

                // Beyond the ground-floor reveal radius, all upper floors remain hidden.
                SetPlayerLocal(player, building.transform, new Vector3(27f, 1.4f, 0f));
                yield return WaitForCulling();
                Check(UpperFloorsHidden(renderers), "outside beyond 25 m: every upper-floor interior is hidden");
                CheckFloorCollidersRemainEnabled(building.transform, "far outside");
            }
            finally
            {
                if (controller != null) controller.enabled = false;
                player.transform.SetPositionAndRotation(savedPosition, savedRotation);
                if (controller != null) controller.enabled = savedControllerEnabled;
                Time.timeScale = savedTimeScale;
                Physics.SyncTransforms();
            }

            Finish();
        }

        private IEnumerator WaitForCulling() => new WaitForSecondsRealtime(CullingWait);

        private void CheckFloorCollidersRemainEnabled(Transform building, string phase)
        {
            Collider[] colliders = building.GetComponentsInChildren<Collider>(true);
            Check(colliders.Length > 0 && colliders.All(c => c != null && c.enabled),
                $"floor/building colliders remain enabled {phase} ({colliders.Length} colliders)");
        }

        private static bool UpperFloorsHidden(Renderer[][] renderers)
        {
            for (int floor = 1; floor < renderers.Length; floor++)
                if (renderers[floor].Any(r => r != null && r.enabled)) return false;
            return true;
        }

        private static bool AllEnabled(Renderer[] renderers) =>
            renderers.Length > 0 && renderers.All(r => r != null && r.enabled);

        private static void SetPlayerLocal(FirstPersonController player, Transform building, Vector3 localPosition)
        {
            player.transform.position = building.TransformPoint(localPosition);
            Physics.SyncTransforms();
        }

        private static Transform[] GetInteriorFloors(Transform building)
        {
            var floors = new List<Transform>();
            for (int i = 0; i < 32; i++)
            {
                Transform floor = building.Find("Floor_" + i + "/Interior");
                if (floor == null) break;
                floors.Add(floor);
            }
            return floors.ToArray();
        }

        private static BuildingInterior FindTestBuilding(ProceduralWorld world)
        {
            BuildingInterior FindInLoadedWorld() => world.GetComponentsInChildren<BuildingInterior>(true)
                .Where(b => b != null && b.name.StartsWith("Building_", StringComparison.Ordinal))
                .OrderByDescending(b => GetInteriorFloors(b.transform).Length)
                .FirstOrDefault(b => HasTestRenderers(b.transform));

            BuildingInterior found = FindInLoadedWorld();
            if (found != null) return found;

            // A few deterministic town chunks are enough to find a multi-storey block without changing world state.
            for (int radius = 1; radius <= 12; radius++)
                for (int z = -radius; z <= radius; z++)
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Abs(x) != radius && Mathf.Abs(z) != radius) continue;
                        var coordinate = new Vector2Int(x, z);
                        if (!WorldLayout.IsTownChunk(coordinate, world.Seed) ||
                            ProceduralLandmarks.HasLandmark(coordinate, world.Seed)) continue;
                        world.EnsureChunk(coordinate);
                        found = FindInLoadedWorld();
                        if (found != null) return found;
                    }
            return null;
        }

        private static bool HasTestRenderers(Transform building)
        {
            Transform[] floors = GetInteriorFloors(building);
            return floors.Length > TargetFloor + 1 &&
                   floors.Take(TargetFloor + 2).All(f => f.GetComponentsInChildren<Renderer>(true).Length > 0);
        }

        private void Check(bool condition, string message)
        {
            if (condition) Debug.Log("[BuildingCullingProbe] PASS: " + message);
            else { failures++; Debug.LogError("[BuildingCullingProbe] FAIL: " + message); }
        }

        private void Finish()
        {
            Debug.Log(failures == 0 ? "[BuildingCullingProbe] ALL CHECKS PASSED" : $"[BuildingCullingProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
