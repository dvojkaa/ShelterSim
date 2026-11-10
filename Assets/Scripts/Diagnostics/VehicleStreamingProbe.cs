using System;
using System.Collections;
using SheltreSim.Building;
using SheltreSim.Core;
using SheltreSim.Crafting;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Standalone player diagnostic: real W input in a mounted buggy over a 3 km straight road run.
    /// Checks streamed ground at the vehicle every frame and reports the worst observed frame time.
    /// Run with --vehicle-stream-probe.
    /// </summary>
    public sealed class VehicleStreamingProbe : MonoBehaviour
    {
        private const float TargetDistance = 3000f;
        private const float StartX = 0f; // Guaranteed continuous north/south road-grid corridor.
        private const float StartZ = -1536f;
        private const float DriveTimeoutSeconds = 180f;
        private const float WatchdogSeconds = 240f;

        private int failures;
        private bool finished;
        private Keyboard keyboard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--vehicle-stream-probe") >= 0)
                new GameObject(nameof(VehicleStreamingProbe)).AddComponent<VehicleStreamingProbe>();
        }

        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return null;
            yield return null;

            // The code-built Continue/New Game front door is part of the normal scene bootstrap.
            if (ModalUI.IsOpen) ModalUI.DismissCurrent();
            Time.timeScale = 1f;

            FirstPersonController player = FindFirstObjectByType<FirstPersonController>();
            ProceduralWorld world = ProceduralWorld.Active;
            HomeBase home = HomeBase.Active;
            if (player == null || world == null || home == null)
            {
                Fail("scene is missing the player, procedural world, or bunker");
                Finish();
                yield break;
            }

            Inventory inventory = player.GetComponent<Inventory>();
            BuildModeController build = player.GetComponentInChildren<BuildModeController>();
            if (inventory == null || build == null)
            {
                Fail("player is missing inventory or build mode");
                Finish();
                yield break;
            }

            home.DebugStartSurvival();
            BuildPieceDefinition definition = build.availablePieces.Find(p => p != null && p.id == "piece_scout_buggy");
            if (definition == null)
            {
                Fail("no Scout Buggy build definition");
                Finish();
                yield break;
            }
            foreach (CraftingCost cost in definition.craftingCost)
                if (cost.resource != null) inventory.Add(cost.resource.id, Mathf.Max(1, cost.amount));

            GameObject vehicle = null;
            string placementReason = "no valid yard spot";
            Vector3[] yardSpots =
            {
                new Vector3(58f, 0f, -236f), new Vector3(70f, 0f, -250f),
                new Vector3(86f, 0f, -250f), new Vector3(75f, 0f, -262f)
            };
            CharacterController body = player.GetComponent<CharacterController>();
            foreach (Vector3 spot in yardSpots)
            {
                body.enabled = false;
                player.transform.position = new Vector3(spot.x, Mathf.Max(7f, player.transform.position.y), spot.z - 5f);
                body.enabled = true;
                yield return new WaitForSeconds(.25f);
                Vector3 rayOrigin = new Vector3(spot.x, player.transform.position.y, spot.z) + Vector3.up * 3f;
                vehicle = build.DebugPlaceAt("piece_scout_buggy", new Ray(rayOrigin, Vector3.down), 0f, out placementReason);
                if (vehicle != null) break;
            }
            if (vehicle == null)
            {
                Fail("could not place the probe buggy: " + placementReason);
                Finish();
                yield break;
            }

            RideableScoutBuggy buggy = vehicle.GetComponent<RideableScoutBuggy>();
            if (buggy == null)
            {
                Fail("placed buggy prefab has no RideableScoutBuggy");
                Finish();
                yield break;
            }
            if (RideableScoutBuggy.FuelRangePerCan < TargetDistance)
            {
                Fail($"one fuel can only covers {RideableScoutBuggy.FuelRangePerCan:0} m, below the {TargetDistance:0} m probe route");
                Finish();
                yield break;
            }

            // The world road grid has a continuous north/south corridor at x=0. Staying on its
            // centerline avoids random roadside trees and scrap piles, so this measures streaming.
            Vector3 startGround = new Vector3(StartX, WorldLayout.HeightAt(StartX, StartZ, world.Seed), StartZ);
            Quaternion heading = Quaternion.identity;
            VehicleTerrain.SamplePose(startGround, heading, 1.05f, 2.8f, world.Seed,
                out Vector3 vehicleStart, out Quaternion vehicleRotation, out _, out _);
            body.enabled = false;
            player.transform.SetPositionAndRotation(new Vector3(StartX, startGround.y + 1.1f, StartZ - 4f), heading);
            vehicle.transform.SetPositionAndRotation(vehicleStart, vehicleRotation);
            body.enabled = true;
            Physics.SyncTransforms();

            float readyDeadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < readyDeadline &&
                (!world.HasGroundAt(vehicleStart) || !world.HasGroundAt(vehicleStart + Vector3.forward * 240f)))
                yield return null;
            Check(world.HasGroundAt(vehicleStart), "start chunk streamed before mounting");
            Check(world.HasGroundAt(vehicleStart + Vector3.forward * 240f), "road ahead is streamed before the drive");
            if (failures > 0) { Finish(); yield break; }

            // Mount and refuel through the same interaction/input path used in play.
            inventory.Add("fuel", 1);
            IInteractable interactable = vehicle.GetComponent<IInteractable>();
            if (interactable == null)
            {
                Fail("buggy has no interaction component");
                Finish();
                yield break;
            }
            interactable.Interact(player.gameObject);
            yield return new WaitForSeconds(.75f);
            Check(buggy.IsMounted, "buggy mounts successfully at the streamed route start");
            if (!buggy.IsMounted) { Finish(); yield break; }

            keyboard = InputSystem.AddDevice<Keyboard>("VehicleStreamingProbeKeyboard");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Check(buggy.FuelRemainingMeters >= TargetDistance,
                $"one real H refuel supplies at least {TargetDistance:0} m ({buggy.FuelRemainingMeters:0} m loaded)");
            if (buggy.FuelRemainingMeters < TargetDistance) { Finish(); yield break; }

            Vector3 origin = vehicle.transform.position;
            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            float furthest = 0f;
            float worstFrameMs = 0f;
            float started = Time.realtimeSinceStartup;
            int samples = 0;
            int missingGroundSamples = 0;
            float stalledSince = started;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return null;
            Check(player.MoveInput.y > .5f, "the actual player W action is held for the streaming run");

            while (!finished && furthest < TargetDistance && Time.realtimeSinceStartup - started < DriveTimeoutSeconds)
            {
                yield return null;
                float frameMs = Time.unscaledDeltaTime * 1000f;
                if (frameMs > worstFrameMs) worstFrameMs = frameMs;
                samples++;

                if (!world.HasGroundAt(vehicle.transform.position))
                {
                    missingGroundSamples++;
                    Fail($"streaming lost ground below the buggy at {vehicle.transform.position:F1} after {furthest:0} m");
                    break;
                }

                Vector3 delta = vehicle.transform.position - origin;
                delta.y = 0f;
                float distance = Vector3.Dot(delta, forward);
                if (distance > furthest + .05f)
                {
                    furthest = distance;
                    stalledSince = Time.realtimeSinceStartup;
                }
                else if (Time.realtimeSinceStartup - stalledSince > 18f)
                {
                    Fail($"buggy stopped making forward progress at {furthest:0} m; position={vehicle.transform.position:F1}; blocker={buggy.LastBlockingCollider}");
                    break;
                }
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard = null;

            Check(missingGroundSamples == 0, "no sampled frame lost the terrain chunk under the buggy");
            Check(furthest >= TargetDistance, $"real W drive covered 3 km (measured {furthest:0} m)");
            Check(buggy.FuelRemainingMeters > 0f, $"one fuel can finished with {buggy.FuelRemainingMeters:0} m remaining");
            Debug.Log($"[VehicleStreamingProbe] route={StartZ:0} to {StartZ + furthest:0} m, distance={furthest:0} m, samples={samples}, worst-frame={worstFrameMs:F1} ms, fuel-left={buggy.FuelRemainingMeters:0} m");
            Finish();
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(WatchdogSeconds);
            if (finished) yield break;
            Fail("timed out after " + WatchdogSeconds.ToString("0") + " seconds");
            Finish();
        }

        private void Check(bool condition, string message)
        {
            if (condition) Debug.Log("[VehicleStreamingProbe] PASS: " + message);
            else Fail(message);
        }

        private void Fail(string message)
        {
            failures++;
            Debug.LogError("[VehicleStreamingProbe] FAIL: " + message);
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            Debug.Log(failures == 0 ? "[VehicleStreamingProbe] ALL CHECKS PASSED" : $"[VehicleStreamingProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
