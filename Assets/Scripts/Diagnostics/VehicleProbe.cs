using System;
using System.Collections;
using System.Reflection;
using SheltreSim.Audio;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Run the built player with <c>--vehicle-probe</c>. Places each vehicle through the real build path, mounts it, moves the
    /// rider several metres, dismounts, and checks after each step that the vehicle is still drawn, rides above the ground
    /// (not sunk into it) and that the seat camera is above the terrain. Playtest bug: "the vehicle is placed well, but once
    /// I get in I fall under the map".
    /// </summary>
    public sealed class VehicleProbe : MonoBehaviour
    {
        private int failures;
        private int stepsAtMount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--vehicle-probe") >= 0)
                new GameObject(nameof(VehicleProbe)).AddComponent<VehicleProbe>();
        }

        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            yield return null;
            yield return null;
            FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
            Inventory inventory = player.GetComponent<Inventory>();
            BuildModeController build = player.GetComponentInChildren<BuildModeController>();
            SheltreSim.Core.HomeBase.Active.DebugStartSurvival();
            yield return new WaitForSeconds(1f);

            CheckGeneratedSlopeHandling(VehicleTerrain.CurrentSeed, player.transform.position);
            Check(RideableScoutBuggy.FuelRangePerCan == 4000f && RideableScoutBuggy.TankRange == 12000f &&
                RideableScoutBuggy.BatteryRangePerCell == 1500f, "buggy fuel and battery range match long-range tuning");
            Check(RideableArmoredVan.FuelRangePerCan == 4000f && RideableArmoredVan.TankRange == 12000f &&
                RideableArmoredVan.BatteryRangePerCell == 1500f, "van fuel and battery range match long-range tuning");

            string[] ids = { "piece_scout_buggy", "piece_armored_van", "piece_bicycle" };
            for (int i = 0; i < ids.Length; i++)
            {
                BuildPieceDefinition def = build.availablePieces.Find(p => p != null && p.id == ids[i]);
                if (def == null) { Fail(ids[i], "no build definition"); continue; }
                foreach (CraftingCost cost in def.craftingCost) if (cost.resource != null) inventory.Add(cost.resource.id, cost.amount * 2);
                // The first free spot of a few candidates in and around the yard (the vans need 5 x 6.5 m).
                GameObject vehicle = null;
                string reason = "no candidate spot";
                Vector3[] spots = { new Vector3(58f, 0f, -236f), new Vector3(70f, 0f, -250f), new Vector3(86f, 0f, -250f), new Vector3(86f, 0f, -236f), new Vector3(58f, 0f, -262f), new Vector3(75f, 0f, -262f) };
                foreach (Vector3 spot in spots)
                {
                    Vector3 origin = new Vector3(spot.x, player.transform.position.y, spot.z);
                    player.GetComponent<CharacterController>().enabled = false;
                    player.transform.position = new Vector3(origin.x, player.transform.position.y, origin.z - 5f);
                    player.GetComponent<CharacterController>().enabled = true;
                    yield return new WaitForSeconds(0.3f);
                    vehicle = build.DebugPlaceAt(ids[i], new Ray(origin + Vector3.up * 3f, Vector3.down), 0f, out reason);
                    if (vehicle != null) break;
                }
                if (vehicle == null) { Fail(ids[i], "could not place: " + reason); continue; }
                yield return null;
                float ground = GroundAt(vehicle.transform.position);
                Look(ids[i], "placed", vehicle, player, ground);

                IInteractable interactable = vehicle.GetComponent<IInteractable>();
                if (interactable == null) { Fail(ids[i], "no IInteractable on the placed vehicle"); continue; }
                stepsAtMount = PlayerAudio.StepsPlayed;
                Transform driverDoor = FindNamed(vehicle.transform, "Door_L");
                Quaternion closedDoorRotation = driverDoor != null ? driverDoor.localRotation : Quaternion.identity;
                if (ids[i] == "piece_armored_van" && driverDoor == null)
                    Fail(ids[i], "authored Door_L pivot is missing, so mount/dismount door motion cannot run");
                else if (ids[i] == "piece_scout_buggy" && driverDoor == null)
                    Debug.Log("[VehicleProbe] piece_scout_buggy uses an open-frame body and has no door to animate");
                Vector3 parkedPosition = vehicle.transform.position;
                Quaternion parkedRotation = vehicle.transform.rotation;
                CharacterController playerController = player.GetComponent<CharacterController>();
                float walkingSlopeLimit = playerController.slopeLimit;
                float walkingStepOffset = playerController.stepOffset;
                GameAudio.PlayLog = new System.Collections.Generic.List<string>();
                interactable.Interact(player.gameObject);
                yield return new WaitForSeconds(0.23f);
                Check(playerController.slopeLimit >= 65f && playerController.stepOffset >= .4f,
                    $"{ids[i]} mount enables steep-ground controller support (slope {playerController.slopeLimit:F0}°, step {playerController.stepOffset:F2} m)");
                if (driverDoor != null)
                    Check(Quaternion.Angle(driverDoor.localRotation, closedDoorRotation) > 20f,
                        $"{ids[i]} mount swings the driver door open");
                yield return new WaitForSeconds(1.97f); // total 2.2 s: ignition sound covers 1.5 s, then the idle loop takes over
                if (driverDoor != null)
                    Check(Quaternion.Angle(driverDoor.localRotation, closedDoorRotation) < 2f,
                        $"{ids[i]} mount closes the driver door at the seat");
                Check(Vector3.Distance(vehicle.transform.position, parkedPosition) < 0.15f &&
                    Quaternion.Angle(vehicle.transform.rotation, parkedRotation) < 1f,
                    $"{ids[i]} mount preserves its parked pose");
                if (ids[i] != "piece_bicycle" && !GameAudio.PlayLog.Contains(Sfx.EngineStart)) Fail(ids[i], "mounted: the ignition sound did not play");
                Look(ids[i], "mounted", vehicle, player, GroundAt(vehicle.transform.position));
                CheckAudio(ids[i], "mounted", player, ids[i] == "piece_bicycle" ? null : (ids[i] == "piece_armored_van" ? Sfx.MotorVan : Sfx.MotorBuggy));
                if (ids[i] != "piece_bicycle") yield return CheckHorn(ids[i]);

                if (ids[i] != "piece_bicycle")
                {
                    inventory.Add("fuel", 1);
                    Keyboard refuelKeyboard = InputSystem.AddDevice<Keyboard>("VehicleRefuelProbeKeyboard");
                    InputSystem.QueueStateEvent(refuelKeyboard, new KeyboardState(Key.H));
                    yield return null;
                    InputSystem.QueueStateEvent(refuelKeyboard, new KeyboardState());
                    yield return null;
                    InputSystem.RemoveDevice(refuelKeyboard);
                    float fuel = ids[i] == "piece_armored_van"
                        ? vehicle.GetComponent<RideableArmoredVan>().FuelFraction01
                        : vehicle.GetComponent<RideableScoutBuggy>().FuelFraction01;
                    Check(fuel > 0f, $"{ids[i]} refuels through H before the drive-input check");
                }

                // Drive through the player's real W binding before the deterministic pose step below.
                Vector3 beforeInputDrive = vehicle.transform.position;
                Vector3 riderBeforeInputDrive = player.transform.position;
                Keyboard driveKeyboard = InputSystem.AddDevice<Keyboard>("VehicleDriveProbeKeyboard");
                InputSystem.QueueStateEvent(driveKeyboard, new KeyboardState(Key.W));
                yield return null;
                Check(player.MoveInput.y > 0.5f, $"{ids[i]} receives the real W drive input");
                yield return new WaitForSeconds(0.75f);
                InputSystem.QueueStateEvent(driveKeyboard, new KeyboardState());
                yield return null;
                InputSystem.RemoveDevice(driveKeyboard);
                Vector3 inputTravel = vehicle.transform.position - beforeInputDrive;
                inputTravel.y = 0f;
                Vector3 riderInputTravel = player.transform.position - riderBeforeInputDrive;
                riderInputTravel.y = 0f;
                Debug.Log($"[VehicleProbe] {ids[i]} real W drive: input={player.MoveInput}, riderDelta={riderInputTravel:F2}, vehicleDelta={inputTravel:F2}, forward={player.transform.forward:F2}");
                Check(Vector3.Dot(inputTravel, player.transform.forward) > 0.35f,
                    $"{ids[i]} moves forward under real player input (delta {inputTravel:F2})");
                float measuredDriveSpeed = inputTravel.magnitude / .75f;
                float minimumDriveSpeed = ids[i] == "piece_scout_buggy" ? 12f : ids[i] == "piece_armored_van" ? 9f : 0f;
                if (minimumDriveSpeed > 0f)
                    Check(measuredDriveSpeed >= minimumDriveSpeed,
                        $"{ids[i]} reaches a useful real-input cruise speed (measured {measuredDriveSpeed:F1} m/s, minimum {minimumDriveSpeed:F1})");

                if (ids[i] != "piece_bicycle")
                {
                    // Real W + D: the rider turns (A/D steer, no strafing) and the vehicle must follow the new heading.
                    float riderYaw0 = player.transform.eulerAngles.y, vehicleYaw0 = vehicle.transform.eulerAngles.y;
                    Vector3 sideStart = player.transform.position;
                    // Back away from whatever the straight drive ran into (the yard fence) so the steering run has room.
                    Keyboard backKeyboard = InputSystem.AddDevice<Keyboard>("VehicleBackProbeKeyboard");
                    InputSystem.QueueStateEvent(backKeyboard, new KeyboardState(Key.S));
                    yield return new WaitForSeconds(1.4f);
                    InputSystem.QueueStateEvent(backKeyboard, new KeyboardState());
                    yield return new WaitForSeconds(0.4f);
                    InputSystem.RemoveDevice(backKeyboard);
                    riderYaw0 = player.transform.eulerAngles.y; vehicleYaw0 = vehicle.transform.eulerAngles.y;
                    sideStart = player.transform.position;
                    Keyboard steerKeyboard = InputSystem.AddDevice<Keyboard>("VehicleSteerProbeKeyboard");
                    InputSystem.QueueStateEvent(steerKeyboard, new KeyboardState(Key.W, Key.D));
                    yield return new WaitForSeconds(1.2f);
                    InputSystem.QueueStateEvent(steerKeyboard, new KeyboardState());
                    yield return null;
                    InputSystem.RemoveDevice(steerKeyboard);
                    float riderTurn = Mathf.DeltaAngle(riderYaw0, player.transform.eulerAngles.y);
                    float vehicleTurn = Mathf.DeltaAngle(vehicleYaw0, vehicle.transform.eulerAngles.y);
                    Debug.Log($"[VehicleProbe] {ids[i]} steer W+D 1.2 s: riderTurn={riderTurn:F1} vehicleTurn={vehicleTurn:F1} travel={(player.transform.position - sideStart).magnitude:F1} m");
                    Check(riderTurn > 15f, $"{ids[i]} W+D steers right (rider turned {riderTurn:F1} deg)");
                    Check(Mathf.Abs(vehicleTurn - riderTurn) < 12f, $"{ids[i]} chassis follows the steering (vehicle {vehicleTurn:F1} vs rider {riderTurn:F1} deg)");
                }
                // Deterministic pose step checks the full movement path without frame-rate dependence.
                CharacterController cc = player.GetComponent<CharacterController>();
                for (int step = 0; step < 6; step++)
                {
                    cc.enabled = false;
                    player.transform.position += player.transform.forward * 1.0f;
                    cc.enabled = true;
                    yield return null;
                }
                yield return new WaitForSeconds(0.3f);
                Look(ids[i], "driven 6 m", vehicle, player, GroundAt(vehicle.transform.position));
                CheckAudio(ids[i], "driven 6 m", player, ids[i] == "piece_bicycle" ? Sfx.BikeChain : (ids[i] == "piece_armored_van" ? Sfx.MotorVan : Sfx.MotorBuggy));
                if (ids[i] == "piece_scout_buggy") yield return CheckSteepRampDrive(vehicle, player);

                MethodInfo dismount = null;
                foreach (MonoBehaviour behaviour in vehicle.GetComponents<MonoBehaviour>())
                {
                    foreach (MethodInfo m in behaviour.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                        if (m.Name == "Dismount") { dismount = m; break; }
                    if (dismount != null)
                    {
                        // Optional parameters (the buggy has Dismount(bool force = false)) need Type.Missing through reflection.
                        object[] args = new object[dismount.GetParameters().Length];
                        for (int a = 0; a < args.Length; a++) args[a] = Type.Missing;
                        dismount.Invoke(behaviour, BindingFlags.Default | BindingFlags.OptionalParamBinding, null, args, null);
                        break;
                    }
                }
                if (dismount == null) Fail(ids[i], "no Dismount method");
                if (driverDoor != null && dismount != null)
                {
                    yield return new WaitForSeconds(0.23f);
                    Check(Quaternion.Angle(driverDoor.localRotation, closedDoorRotation) > 20f,
                        $"{ids[i]} dismount swings the driver door open");
                    yield return new WaitForSeconds(0.40f);
                    Check(Quaternion.Angle(driverDoor.localRotation, closedDoorRotation) < 2f,
                        $"{ids[i]} dismount closes the driver door after exiting");
                    yield return new WaitForSeconds(0.57f);
                }
                else yield return new WaitForSeconds(1.2f);
                Check(Mathf.Approximately(playerController.slopeLimit, walkingSlopeLimit) &&
                    Mathf.Approximately(playerController.stepOffset, walkingStepOffset),
                    $"{ids[i]} dismount restores normal player slope and step settings");
                if (ids[i] == "piece_bicycle")
                    Check(!vehicle.GetComponent<RideableBicycle>().IsMounted,
                        "bicycle finds a capsule-clear exit after driving with a nearby parked vehicle");
                Look(ids[i], "dismounted", vehicle, player, GroundAt(vehicle.transform.position));
            }

            Debug.Log(failures == 0 ? "[VehicleProbe] ALL CHECKS PASSED" : $"[VehicleProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        /// <summary>End-to-end incline test: real W input, CharacterController, vehicle support sampling and chassis sweep on a 52° ramp.</summary>
        private IEnumerator CheckSteepRampDrive(GameObject vehicle, FirstPersonController player)
        {
            CharacterController body = player.GetComponent<CharacterController>();
            if (body == null || vehicle == null) { Fail("piece_scout_buggy", "steep-ramp test has no rider/body"); yield break; }

            Vector3 parkedPlayer = player.transform.position;
            Quaternion parkedPlayerRotation = player.transform.rotation;
            Vector3 parkedVehicle = vehicle.transform.position;
            Quaternion parkedVehicleRotation = vehicle.transform.rotation;
            ProceduralWorld world = ProceduralWorld.Active;
            bool worldWasEnabled = world != null && world.enabled;
            if (world != null) world.enabled = false; // Keep streaming/buildings out of this isolated physics lane.

            const float angle = 52f, run = 30f, width = 12f, thickness = .4f;
            Vector3 toe = new Vector3(10000f, 0f, 10000f);
            float ground = WorldLayout.HeightAt(toe.x, toe.z, VehicleTerrain.CurrentSeed);
            toe.y = ground;
            Quaternion yaw = Quaternion.Euler(0f, parkedPlayerRotation.eulerAngles.y, 0f);
            Vector3 forward = yaw * Vector3.forward;
            // Begin already on the ramp so the chassis sweep tests the upward plane,
            // not the intentionally vertical cut face at the ramp's foot.
            Vector3 start = toe + forward * 2f + Vector3.up * (2f * Mathf.Tan(angle * Mathf.Deg2Rad));
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "VehicleSlopeProbePlatform";
            platform.GetComponent<Renderer>().enabled = false;
            platform.transform.SetPositionAndRotation(toe - forward * 2f + Vector3.up * (ground - .2f), yaw);
            platform.transform.localScale = new Vector3(width, .4f, 8f);

            float radians = angle * Mathf.Deg2Rad;
            float length = run / Mathf.Cos(radians);
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "VehicleSlopeProbeRamp";
            ramp.GetComponent<Renderer>().enabled = false;
            // Give this collider the same support marker as streamed heightfield terrain.
            ramp.AddComponent<ProceduralTerrain>();
            Quaternion rampRotation = yaw * Quaternion.Euler(-angle, 0f, 0f);
            Vector3 rampCenter = toe + forward * (run * .5f) + Vector3.up * (run * .5f * Mathf.Tan(radians) - thickness * .5f * Mathf.Cos(radians));
            ramp.transform.SetPositionAndRotation(rampCenter, rampRotation);
            ramp.transform.localScale = new Vector3(width, thickness, length);

            body.enabled = false;
            player.transform.SetPositionAndRotation(start, yaw);
            body.enabled = true;
            VehicleTerrain.SamplePose(start - forward * 1.55f, yaw, 1.05f, 2.8f, VehicleTerrain.CurrentSeed,
                out Vector3 vehicleStart, out Quaternion vehicleRotation, out _, out _);
            vehicle.transform.SetPositionAndRotation(vehicleStart, vehicleRotation);
            SetPrivate(vehicle.GetComponent<RideableScoutBuggy>(), "lastRiderPosition", start);
            SetPrivate(vehicle.GetComponent<RideableScoutBuggy>(), "ramSweepReady", false);
            Physics.SyncTransforms();
            body.Move(Vector3.down * .08f); // settle the capsule onto the custom support plane before sending input
            yield return null;

            float playerY = player.transform.position.y;
            float vehicleY = vehicle.transform.position.y;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("VehicleSteepRampKeyboard");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(.8f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.RemoveDevice(keyboard);

            float climb = player.transform.position.y - playerY;
            float forwardTravel = Vector3.Dot(Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up), forward);
            float chassisClimb = vehicle.transform.position.y - vehicleY;
            Debug.Log($"[VehicleProbe] piece_scout_buggy 52° ramp: forward={forwardTravel:F2} m, rider climb={climb:F2} m, chassis climb={chassisClimb:F2} m");
            Check(forwardTravel > 4f && climb > 1.5f && chassisClimb > .4f,
                $"buggy climbs a real 52° collider ramp using W (forward {forwardTravel:F2} m, rider +{climb:F2} m, vehicle +{chassisClimb:F2} m)");

            Vector3 crestPosition = player.transform.position;
            float crestVehicleY = vehicle.transform.position.y;
            keyboard = InputSystem.AddDevice<Keyboard>("VehicleSteepRampReverseKeyboard");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
            yield return new WaitForSeconds(.3f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.RemoveDevice(keyboard);
            float downhillTravel = Vector3.Dot(Vector3.ProjectOnPlane(player.transform.position - crestPosition, Vector3.up), forward);
            float downhillDrop = player.transform.position.y - crestPosition.y;
            float chassisDrop = vehicle.transform.position.y - crestVehicleY;
            Debug.Log($"[VehicleProbe] piece_scout_buggy 52° descent: reverse={downhillTravel:F2} m, rider drop={downhillDrop:F2} m, chassis drop={chassisDrop:F2} m");
            Check(downhillTravel < -1f && downhillDrop < -4f && chassisDrop < -4f,
                $"buggy reverses down the ramp under S (reverse {downhillTravel:F2} m, rider {downhillDrop:F2} m, vehicle {chassisDrop:F2} m)");

            body.enabled = false;
            player.transform.SetPositionAndRotation(parkedPlayer, parkedPlayerRotation);
            body.enabled = true;
            vehicle.transform.SetPositionAndRotation(parkedVehicle, parkedVehicleRotation);
            SetPrivate(vehicle.GetComponent<RideableScoutBuggy>(), "lastRiderPosition", parkedPlayer);
            SetPrivate(vehicle.GetComponent<RideableScoutBuggy>(), "ramSweepReady", false);
            Destroy(ramp);
            Destroy(platform);
            Physics.SyncTransforms();
            if (world != null) world.enabled = worldWasEnabled;
            yield return null;
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            FieldInfo field = target != null ? target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic) : null;
            if (field != null) field.SetValue(target, value);
        }

        private IEnumerator CheckHorn(string vehicleId)
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("VehicleProbeKeyboard");
            GameAudio.PlayLog = new System.Collections.Generic.List<string>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K));
            for (int frame = 0; frame < 6 && !GameAudio.PlayLog.Contains(Sfx.VehicleHorn); frame++) yield return null;
            bool played = GameAudio.PlayLog.Contains(Sfx.VehicleHorn);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.RemoveDevice(keyboard);
            if (!played) Fail(vehicleId, "K horn input did not play vehicle_horn");
            else Debug.Log($"[VehicleProbe] {vehicleId} K horn input played {Sfx.VehicleHorn}; vehicle noise alert path invoked");
        }

        private void CheckGeneratedSlopeHandling(int seed, Vector3 around)
        {
            // Find a deterministic traversable slope near the current start area so this probe
            // exercises generated terrain without teleporting a vehicle through the live world.
            Vector3 sample = default;
            float selectedGrade = 0f;
            float selectedSlope = 0f;
            bool found = false;
            for (int radius = 128; radius <= 1024 && !found; radius += 128)
            {
                for (int x = -radius; x <= radius && !found; x += 16)
                {
                    for (int z = -radius; z <= radius; z += 16)
                    {
                        Vector3 candidate = new Vector3(around.x + x, 0f, around.z + z);
                        VehicleTerrain.SamplePose(candidate, Quaternion.identity, 1f, 4f, seed,
                            out Vector3 pose, out _, out float grade, out float slope);
                        if (Mathf.Abs(grade) < .08f || slope < 3f || slope > VehicleTerrain.MaximumBodySlope + .1f)
                            continue;
                        sample = pose;
                        selectedGrade = grade;
                        selectedSlope = slope;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                Fail("terrain", "could not find a generated slope for the vehicle grade probe");
                return;
            }

            float uphill = VehicleTerrain.GradeSpeedMultiplier(Mathf.Abs(selectedGrade), 1f);
            float downhill = VehicleTerrain.GradeSpeedMultiplier(-Mathf.Abs(selectedGrade), 1f);
            float reverseUphill = VehicleTerrain.GradeSpeedMultiplier(-Mathf.Abs(selectedGrade), -1f);
            bool poseGrounded = Mathf.Abs(sample.y - (WorldLayout.HeightAt(sample.x, sample.z, seed) + 1f)) < .01f;
            Debug.Log($"[VehicleProbe] generated slope at {sample:F1}: grade={selectedGrade:F3}, slope={selectedSlope:F1}°, speed scale uphill={uphill:F2} downhill={downhill:F2}");
            if (!poseGrounded) Fail("terrain", "slope pose root height does not follow generated terrain");
            if (!(uphill < 1f && downhill > 1f && reverseUphill < 1f))
                Fail("terrain", "uphill/downhill speed scaling has the wrong direction");

            CheckSlopeProfile("piece_bicycle", sample, seed, .65f, 1.4f);
            CheckSlopeProfile("piece_scout_buggy", sample, seed, 1.05f, 2.8f);
            CheckSlopeProfile("piece_armored_van", sample, seed, 1.25f, 4.05f);
        }

        private void CheckSlopeProfile(string vehicleId, Vector3 centerPose, int seed, float rootHeight, float wheelBase)
        {
            Vector3 requestedCenter = centerPose + Vector3.up * (rootHeight - 1f);
            VehicleTerrain.SamplePose(requestedCenter, Quaternion.identity, rootHeight, wheelBase, seed,
                out Vector3 center, out Quaternion centerRotation, out float grade, out float slope);
            Vector3 startRequest = requestedCenter - Vector3.forward * wheelBase;
            Vector3 endRequest = requestedCenter + Vector3.forward * wheelBase;
            VehicleTerrain.SamplePose(startRequest, Quaternion.identity, rootHeight, wheelBase, seed,
                out Vector3 start, out _, out _, out _);
            VehicleTerrain.SamplePose(endRequest, Quaternion.identity, rootHeight, wheelBase, seed,
                out Vector3 end, out _, out _, out _);
            Quaternion crossYaw = Quaternion.Euler(0f, 90f, 0f);
            VehicleTerrain.SamplePose(requestedCenter, crossYaw, rootHeight, wheelBase, seed,
                out Vector3 crossPose, out Quaternion crossRotation, out _, out _);

            float expectedCenterY = WorldLayout.HeightAt(center.x, center.z, seed) + rootHeight;
            float expectedDelta = WorldLayout.HeightAt(end.x, end.z, seed) - WorldLayout.HeightAt(start.x, start.z, seed);
            bool followsGround = Mathf.Abs(center.y - expectedCenterY) < .03f &&
                Mathf.Abs((end.y - start.y) - expectedDelta) < .06f;
            bool traversesGrade = Mathf.Abs(end.y - start.y) > .05f && Mathf.Abs(grade) > .03f;
            float bodyTilt = Vector3.Angle(centerRotation * Vector3.up, Vector3.up);
            float crossTilt = Vector3.Angle(crossRotation * Vector3.up, Vector3.up);
            bool stableTilt = bodyTilt <= VehicleTerrain.MaximumBodySlope + .1f && crossTilt > .5f &&
                Mathf.Abs(crossPose.y - (WorldLayout.HeightAt(crossPose.x, crossPose.z, seed) + rootHeight)) < .03f;

            if (!followsGround) Fail(vehicleId, "terrain pose does not track generated ground across the test path");
            if (!traversesGrade) Fail(vehicleId, "terrain test path did not produce both ascent and descent grade samples");
            if (!stableTilt) Fail(vehicleId, "terrain pose does not follow the cross-slope within the body tilt limit");
            if (grade != 0f && !(VehicleTerrain.GradeSpeedMultiplier(Mathf.Abs(grade), 1f) < 1f &&
                                  VehicleTerrain.GradeSpeedMultiplier(-Mathf.Abs(grade), 1f) > 1f))
                Fail(vehicleId, "grade scaling does not slow uphill travel and accelerate downhill travel");
            else if (followsGround && traversesGrade && stableTilt)
                Debug.Log($"[VehicleProbe] PASS {vehicleId} terrain ascent/descent and cross-slope: grade={grade:F3}, body tilt={bodyTilt:F1}°, cross tilt={crossTilt:F1}°");
        }

        /// <summary>The right motor loop plays while riding (null = must be silent) and no footstep was played since mounting.</summary>
        private void CheckAudio(string id, string phase, FirstPersonController player, string expectedKey)
        {
            VehicleAudio audio = player.GetComponent<VehicleAudio>();
            if (audio == null) { Fail(id, phase + ": the player has no VehicleAudio"); return; }
            string playing = audio.PlayingKey;
            Debug.Log($"[VehicleProbe] {id} {phase} audio: playing={playing ?? "nothing"} expected={expectedKey ?? "nothing"}");
            if (playing != expectedKey) Fail(id, $"{phase}: vehicle audio plays '{playing ?? "nothing"}', expected '{expectedKey ?? "nothing"}'");
            if (PlayerAudio.StepsPlayed != stepsAtMount) Fail(id, $"{phase}: the player's body played {PlayerAudio.StepsPlayed - stepsAtMount} footstep(s)/landing(s) while riding");
        }

        private static float GroundAt(Vector3 position)
        {
            // Highest solid surface at or below a point above the vehicle, ignoring the vehicle itself.
            RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 30f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<IInteractable>() != null) continue;
                if (hit.collider.GetComponentInParent<FirstPersonController>() != null) continue;
                if (hit.point.y > best && hit.point.y < position.y + 2f) best = hit.point.y;
            }
            return best;
        }

        private void Look(string id, string phase, GameObject vehicle, FirstPersonController player, float ground)
        {
            Renderer[] renderers = vehicle.GetComponentsInChildren<Renderer>();
            int visible = 0;
            Bounds bounds = new Bounds(vehicle.transform.position, Vector3.zero);
            bool hasClearanceRenderer = false;
            float parkedWheelBottom = float.PositiveInfinity;
            Renderer lowestRenderer = null;
            foreach (Renderer r in renderers)
            {
                if (r.enabled && r.gameObject.activeInHierarchy) visible++;
                if (r.name.StartsWith("Wheel_", StringComparison.Ordinal))
                {
                    if (phase == "placed") parkedWheelBottom = Mathf.Min(parkedWheelBottom, r.bounds.min.y);
                    continue; // a rotated wheel's world AABB extends below the real tyre contact patch
                }
                if (!hasClearanceRenderer) { bounds = r.bounds; lowestRenderer = r; hasClearanceRenderer = true; }
                else
                {
                    bounds.Encapsulate(r.bounds);
                    if (r.bounds.min.y < lowestRenderer.bounds.min.y) lowestRenderer = r;
                }
            }
            Camera cam = player.GetComponentInChildren<Camera>();
            float camY = cam != null ? cam.transform.position.y : float.NaN;
            string lowest = lowestRenderer != null
                ? $"{lowestRenderer.name} localRot={lowestRenderer.transform.localEulerAngles:F1} boundsBottom={lowestRenderer.bounds.min.y:F2}"
                : "none";
            Debug.Log($"[VehicleProbe] {id} {phase}: ground={ground:F2} bodyBottom={(hasClearanceRenderer ? bounds.min.y : float.NaN):F2} parkedWheelBottom={parkedWheelBottom:F2} top={bounds.max.y:F2} cameraY={camY:F2} visibleRenderers={visible}/{renderers.Length} root={vehicle.transform.position:F2} yawPitchRoll={vehicle.transform.eulerAngles:F1} lowest={lowest}");
            if (visible == 0) Fail(id, phase + ": no visible renderer");
            if (hasClearanceRenderer && bounds.min.y < ground - 0.25f) Fail(id, $"{phase}: vehicle body is sunk into the ground (bottom {bounds.min.y:F2}, ground {ground:F2})");
            if (phase == "placed" && !float.IsPositiveInfinity(parkedWheelBottom) && parkedWheelBottom < ground - 0.08f)
                Fail(id, $"placed: resting wheel mesh is sunk into the ground (bottom {parkedWheelBottom:F2}, ground {ground:F2})");
            if (!(camY > ground + 0.4f)) Fail(id, $"{phase}: camera is not above the ground (camera {camY:F2}, ground {ground:F2})");
        }

        private void Fail(string id, string message)
        {
            failures++;
            Debug.LogError($"[VehicleProbe] FAIL {id}: {message}");
        }

        private void Check(bool passed, string message)
        {
            if (passed) Debug.Log($"[VehicleProbe] PASS {message}");
            else Fail("mount", message);
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child != root && child.name == objectName) return child;
            return null;
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(150f);
            Debug.LogError("[VehicleProbe] FAIL: timed out");
            Application.Quit(2);
        }
    }
}
