using System;
using System.Collections;
using System.Reflection;
using SheltreSim.Building;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Run the built player with <c>--m28-probe</c> to check the M28 vehicle collision sweeps and dynamic-body impacts.
    /// Invokes each vehicle's real collision resolver against a physics body far above the streamed world. This does not
    /// simulate player input or verify in-game handling, ramming damage, garage services, or structure collapse.
    /// </summary>
    public sealed class M28BacklogProbe : MonoBehaviour
    {
        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--m28-probe") >= 0)
                new GameObject(nameof(M28BacklogProbe)).AddComponent<M28BacklogProbe>();
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            Time.timeScale = 1f;

            BuildModeController build = FindAnyObjectByType<BuildModeController>();
            if (build == null)
            {
                Check(false, "The scene has a BuildModeController");
            }
            else
            {
                yield return CheckVehicle(build, "piece_scout_buggy", typeof(RideableScoutBuggy), "ResolveVehicleMotion", false, 400f);
                yield return CheckVehicle(build, "piece_armored_van", typeof(RideableArmoredVan), "ResolveVehicleMotion", true, 420f);
            }

            Debug.Log(failures == 0 ? "[M28BacklogProbe] ALL CHECKS PASSED" : $"[M28BacklogProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private IEnumerator CheckVehicle(BuildModeController build, string pieceId, Type vehicleType, string resolverName, bool van, float z)
        {
            BuildPieceDefinition definition = FindDefinition(build, pieceId);
            if (definition == null || definition.prefab == null)
            {
                Check(false, $"{pieceId} has a build definition and prefab");
                yield break;
            }

            GameObject vehicle = null;
            GameObject obstacle = null;
            Rigidbody impactedBody = null;
            try
            {
                Vector3 rootPosition = new Vector3(0f, 500f, z);
                vehicle = Instantiate(definition.prefab, rootPosition, Quaternion.identity);
                vehicle.name = $"M28Probe_{pieceId}";
                Component driver = vehicle.GetComponent(vehicleType);
                Check(driver != null, $"{pieceId} prefab has {vehicleType.Name}");

                if (driver != null)
                {
                    BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    Type type = driver.GetType();
                    FieldInfo centerField = type.GetField("vehicleLocalCenter", flags);
                    FieldInfo extentsField = type.GetField("vehicleLocalHalfExtents", flags);
                    MethodInfo resolver = type.GetMethod(resolverName, flags);
                    Check(centerField != null && extentsField != null && resolver != null,
                        $"{pieceId} exposes the expected collision resolver and measured footprint");

                    if (centerField != null && extentsField != null && resolver != null)
                    {
                        Vector3 localCenter = (Vector3)centerField.GetValue(driver);
                        Vector3 localHalfExtents = (Vector3)extentsField.GetValue(driver);
                        Check(localHalfExtents.z > 0f, $"{pieceId} has a usable forward collision extent");

                        if (localHalfExtents.z > 0f)
                        {
                            Vector3 worldCenter = vehicle.transform.TransformPoint(localCenter);
                            obstacle = new GameObject($"M28Probe_{pieceId}_DynamicObstacle");
                            obstacle.transform.position = worldCenter + Vector3.forward * (localHalfExtents.z + .31f);
                            impactedBody = obstacle.AddComponent<Rigidbody>();
                            impactedBody.useGravity = false;
                            impactedBody.mass = 10f;
                            BoxCollider collider = obstacle.AddComponent<BoxCollider>();
                            collider.size = Vector3.one * .5f;
                            Physics.SyncTransforms();

                            Vector3 desiredPosition = vehicle.transform.position + Vector3.forward * 2f;
                            object[] arguments = van
                                ? new object[] { desiredPosition, 20f, vehicle.transform.rotation } // the van resolver also takes the target rotation since the terrain pass
                                : new object[] { desiredPosition, Quaternion.identity };
                            Vector3 resolvedPosition = (Vector3)resolver.Invoke(driver, arguments);

                            Check(resolvedPosition.z < desiredPosition.z - .05f,
                                $"{pieceId} collision sweep blocks movement before a dynamic obstacle");
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Check(false, $"{pieceId} collision probe completed without exception: {exception.GetBaseException().Message}");
            }
            finally
            {
                if (vehicle != null) Destroy(vehicle);
            }

            // AddForceAtPosition with ForceMode.Impulse is consumed by the next physics simulation step.
            yield return new WaitForFixedUpdate();
            Check(impactedBody != null && impactedBody.linearVelocity.sqrMagnitude > .01f,
                $"{pieceId} fast contact gives the dynamic obstacle an impulse");
            if (obstacle != null) Destroy(obstacle);
            yield return null;
        }

        private static BuildPieceDefinition FindDefinition(BuildModeController build, string id)
        {
            if (build == null || build.availablePieces == null) return null;
            foreach (BuildPieceDefinition definition in build.availablePieces)
                if (definition != null && definition.id == id) return definition;
            return null;
        }

        private void Check(bool condition, string description)
        {
            if (condition)
            {
                Debug.Log("[M28BacklogProbe] PASS: " + description);
                return;
            }

            failures++;
            Debug.LogError("[M28BacklogProbe] FAIL: " + description);
        }
    }
}
