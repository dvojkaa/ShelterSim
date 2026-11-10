# Code review: work committed on 2026-11-10

Reviewer: Claude (skeptical read-only pass). Scope: every runtime `.cs` file changed by today's commits
`c60cdd2..88212c4`, read in full together with the code it calls. I also read the uncommitted working-tree edits
to `MeleeAttacker.cs`, `MeleeViewmodel.cs`, `PlayerGear.cs` and `BunkerShop.cs`. Those edits are GPT's G2/G3 work in
progress (see `DEV_COORDINATION.md`). Nothing was edited, and Unity was not run.
`bash tools/compile_check.sh` reports: `COMPILE OK (203 runtime + 29 editor files)`.

Counts: **3 high, 3 medium, 2 low.**

---

## HIGH

### H1. A ridden vehicle's height comes only from the analytic heightmap, never from the floor the rider is on
- **Where:** `World/VehicleTerrain.cs:56` (`position = new Vector3(x, centerHeight + rootHeight, z)` with
  `centerHeight = WorldLayout.HeightAt(...)`). Callers: `RideableBicycle.cs:101,111,160-162`,
  `RideableScoutBuggy.cs:141-143,183-186`, `RideableArmoredVan.cs:215-217`.
- **What is wrong:** Commit `765fa14` replaced `follow.y = rider.transform.position.y + RootHeight` with the terrain
  formula, which only knows about the procedural terrain mesh. The rider is a CharacterController walking on real colliders.
  `VehicleMountView.LateUpdate` (line 157) places the camera on the vehicle's `Seat`, so the camera follows the
  vehicle's height and ignores the rider's.
- **Failure scenario:**
  1. The shop's Bicycle or Buggy kit arrives in the Supply Cage at `(67, 0.5, -222.4)`. That is inside the bunker:
     the bunker floor is at y=0 and the ground above it is `HomeGroundY = 6`.
  2. The player places the bicycle in the bunker and presses E.
  3. `Interact` snaps the bike to y≈6.65, which is above the 3 m of earth over the bunker roof. The mount transition
     flies the camera up through the roof.
  4. The rider's capsule stays in the bunker and drives around blind.

  The same thing happens when riding the bicycle (a 0.7 m capsule) through a 1.8 m doorway and up the house or apartment
  stairs. The camera stays at ground-floor seat height while the body climbs. On any player-built foundation, the
  vehicle sinks into the floor by the foundation's height.
- **Fix:** Get the support height from a downward `Physics.Raycast` under the follow point. Start it at the rider's
  feet plus about 1 m, and exclude the vehicle and the rider. Fall back to `HeightAt` only when nothing is hit.
  Alternatively, take Y from `rider.position.y + RootHeight` and use the terrain only for the tilt.

### H2. The Scout Buggy body separates from its rider; the camera stays with the buggy
- **Where:** `World/RideableScoutBuggy.cs:152-154`. Compare `RideableArmoredVan.cs:136-147`.
- **What is wrong:** `ResolveVehicleMotion` stops the 3.8 m-wide buggy at the first blocker. Unlike the van, the buggy
  never moves the rider back by `correction`. The rider is the thing actually being driven: it is a 0.7 m capsule at
  18–22 m/s, and the vehicle colliders are disabled. Meanwhile the camera is locked to the buggy's seat.
- **Failure scenario:**
  1. Drive the buggy at a 1.8 m house door, or between two trees, or past a lamp post.
  2. The rider capsule passes through the gap. The buggy hits the wall and stays where it is.
  3. The player keeps steering, but the view is frozen at the wall while the invisible body drives on. Interaction,
     raider aggro, radiation and chunk streaming all follow the body.
  4. Once the straight line from the buggy to its follow point is clear again, the buggy jumps the whole distance in one
     frame. Line 271 has no distance cap.
- **Fix:** Copy the van's block. When `resolved != follow`, call `riderBody.Move(resolved - follow)`, then re-sample.
  Optionally, force a dismount if the rider and the vehicle are ever more than about 1 m off the seat offset.

### H3. Searched cabinets: the doors and lids jump to the furniture's origin
- **Where:** `Items/LootContainer.cs:83-85` (detection) and `:100` (`openPosition = definition.openLocalPosition`).
- **What is wrong:** For the auto-detected `Door`, `Door_L`, `Door_R` and `Lid` parts, only `openLocalEulerAngles` is
  set. `openLocalPosition` keeps its default `(0,0,0)`, and `ApplyOpenPose` lerps the part's position from its hinge to
  that zero point. The parts are not at the origin. For example, `ArtSource/assets/furn_wardrobe.py:19,41-42` builds
  `Door_L`/`Door_R` with pivots at `(±0.7, FY-0.015, hz)`.
- **Failure scenario:** Search any wardrobe, linen closet, lockers, fridge, kitchen run, medicine cabinet or relay
  cabinet. The doors swing and also slide into the middle of the cabinet's base, so the cabinet looks broken.
  `RestoreSearchedState(true)` reapplies the same pose after a load, and so do `OnDisable` and chunk reloads.
  Drawers are not affected because they set their own `openLocalPosition`.
- **Fix:** In `CacheOpenParts`, set `openLocalPosition = child.localPosition` for the rotation-only kinds. As a safer
  alternative, have `OpenPose` use `closedPosition` whenever `openLocalPosition == Vector3.zero` and the part only
  rotates.

---

## MEDIUM

### M1. The van and buggy sweeps treat terrain they already overlap as a wall at distance 0
- **Where:** `RideableArmoredVan.cs:259-276` and `RideableScoutBuggy.cs:249-272`, with the box sizes set at
  `RideableArmoredVan.cs:248` and `RideableScoutBuggy.cs:238`.
- **What is wrong:** The cast box extends to 0.44 or 0.46 of the measured height around the bounds centre. That leaves
  only about 0.08 m (buggy) to 0.15 m (van) of clearance under the body. The box is tilted to a normal sampled ±2 m
  around the centre, but the body is 4.3–6.1 m long. `BoxCastNonAlloc` reports colliders it already overlaps at the
  start as hits with `distance = 0` (Unity's documented behaviour), and `IsVehicleBlocker` accepts the terrain
  MeshCollider. The result is `nearest = 0`, so the vehicle does not move.
- **Failure scenario:**
  1. Drive onto a concave crease between two of the 16 m terrain triangles, onto the town-pad blend ring (the SmoothStep
     between 215 and 252 m), or onto a player-built foundation.
  2. The front or rear of the box dips below the surface.
  3. The van pulls its rider back every frame, so it cannot climb out. The buggy freezes while its rider leaves (see H2).
- **Not verified at runtime:** `VehicleProbe.CheckGeneratedSlopeHandling` only tests the `SamplePose` and grade maths.
  It never drives a box across terrain. A sweep with a `_TempProbe` along a slope, in the style of `StallSweepProbe`,
  would settle how often this happens.
- **Fix:** Ignore hits where `distance <= 0` and `normal.y > 0.5`, which are ground. Alternatively, raise the bottom of
  the cast box by the step height, about 0.35 m (shift the centre up and shrink the Y half-extent).

### M2. Mounting teleports and turns the vehicle to match the player's view
- **Where:** `RideableArmoredVan.cs:187-192`, `RideableScoutBuggy.cs:181-187`, `RideableBicycle.cs:159-162`.
- **What is wrong:** On E, the vehicle is moved to 3 m, 1.55 m or 0.8 m behind the rider along the camera's yaw. The
  rider is not moved to the vehicle's seat.
- **Failure scenario:** Walk up to the van's nose and press E while facing it. The van rotates 180° and moves about 6 m
  until it sits behind you.
  - The van checks the rotation only at the old position (line 189).
  - The sweep at line 191 is a translation only, so turning the long box in place can push it into a garage wall or a
    neighbouring car before the cast runs.
  - A buggy parked nose-in inside a garage gets swung across the garage doorway.
- **Fix:** Keep the vehicle's pose and teleport the rider instead. Disable the CharacterController, set the rider to
  `vehicle.position + vehicle.forward * offset` with a feet height derived from `RootHeight`, set its yaw to the
  vehicle's yaw, then re-enable the controller.

### M3. Saving while driving: loading puts the player inside the vehicle's collider
- **Where:** `Saving/SaveManager.cs` captures `playerPosition` from the rider capsule. No mount state is saved.
  `ApplySnapshot` (around lines 553-585) recreates the vehicle with its colliders enabled.
- **Failure scenario:**
  1. Save from the pause menu while in the van.
  2. The rider capsule is 3 m ahead of the van's root, which is inside the 6.1 m body. For the buggy it is 1.55 m ahead,
     inside a 4.3 m body.
  3. On load, the old van's `OnDisable` correctly force-dismounts. The new van is then placed with its box collider
     enabled around the restored player, and the CharacterController's overlap recovery shoves the player out. The
     player ends up on the roof or pushed sideways, possibly into a garage wall.
- **Fix:** If `IsRiding`, capture the pose `VehicleMountView.TryChooseExit` would pick. Or refuse to save while mounted
  and post a message through `GameMessageBus`.

---

## LOW

### L1. The mount captures the camera pivot with CameraFeel's head-bob still applied, and restores it permanently
- **Where:** `World/VehicleMountView.cs:43-44`, with the restore at `:91` and `:143`. `Player/CameraFeel.cs:91`.
- **What is wrong:** `Interact` runs inside an `InputAction.performed` callback, which fires before any `Update`. So
  `BeginMount` runs before `CameraFeel.Update` (`[DefaultExecutionOrder(-10000)]`) has undone the previous frame's bob.
  `originalCameraLocalPosition` therefore includes up to ±0.02 m of X bob, and a landing dip if the player had just
  landed. The dismount writes that value back. `HandleCrouch` rewrites only Y every frame, so the X error stays.
- **Failure scenario:** Mount while walking. After the dismount, the eye sits up to 2 cm to the side. The error adds up
  over repeated mounts, so the eye drifts sideways.
- **Fix:** Store `new Vector3(0, standing eye Y, 0)`, or zero X/Z when restoring. Alternatively, expose
  `CameraFeel.Undo()` and call it in `BeginMount`.

### L2. (Uncommitted G2 work) The axe shows when aiming at a tree, but a swing says "Swing blocked."
- **Where:** `Combat/MeleeAttacker.cs:388-393` (`ContextualToolId`) against `:154-155`, and `Items/ResourceNode.cs:49`.
- **What is wrong:** `FindSwingTarget` returns a node that is not `cutOnHit` as the "nearest obstacle".
  `ContextualToolId` accepts any `ResourceNode` and shows the best axe or pickaxe for it. `TryAttack`, however, only
  strikes nodes where `cutOnHit` is true; only grass has that. Everything else falls through to "Swing blocked."
- **Failure scenario:** With nothing equipped and an iron axe in the pack, aim at a tree or rock. The axe appears in
  hand, and left-click prints "Swing blocked." Gathering still requires E.
- **Fix:** Show a tool only when `node.CanStrike(...)`, or make a melee swing with the matching tool call
  `node.Interact`. GPT owns these files right now, so pass this to G2 rather than editing them.

---

## Checked and found correct (no finding)
- **Normal/ruined random-number split in `ProceduralContent`:**
  - House, Town and Suburb draws all happen in both passes, including the `burnRoll`, shed and parking rolls. Loot
    `Crate()` draws come last.
  - Furniture loot uses its own `SlotRandom(10000+lot*128+slot)`, only in the ruined pass.
  - The `sealedRoll` and roof-loot rolls are drawn in both passes.
  - The slot ranges (forage 4000+, loot 10000–11123, layout rolls 20000–21123) never collide.
  - Loot only appears in the ruined pass.
- **`DoorVisualTween`:** The logical collider snaps to its final pose immediately and only the mesh copies animate.
  Loads snap, because `animate` is false and the staging parent is inactive. `OnDisable` and `Finish` restore
  `forceRenderingOff`. There is no clash with `BuildingInterior`, which toggles `enabled` instead.
- **Mount state machine:**
  - Double mounts are rejected by `IsBusy`.
  - Death, controller disable, piece destruction and the save/load `SetActive(false)` all reach `ForceDismount`, which
    restores walk/sprint speeds and the colliders.
  - Pausing freezes the transitions, which use `Time.deltaTime`.
  - `TryChooseExit` re-enables the vehicle colliders for its capsule check and restores them in `finally`.
- **Melee swing:**
  - Damage stays synchronous.
  - The coroutine is cancelled on suppression, `OnDisable`, death and build mode.
  - The hit-stop only holds the pose curve.
  - The total time is shorter than the 0.6 s cooldown, except for `fire_axe` (0.635 s), which just snaps to rest on the
    next swing.
- **`CameraFeel`:** Suppressed while mounted, paused or dead. The `Undo` approximate-match guard copes with
  `RestoreView` and crouch writes.
- **Bunker economy v7:**
  - Validation enforces exclusive wings, Vault requiring Annex, room bits matching purchases, purchase counts within
    limits, and no purchases without a contract.
  - Migration is ordered before validation.
  - `TryBuyOffer` checks the limit, stage, rooms, exclusivity and money before spending, so money cannot go negative or
    be spent twice.
  - The Supply Cage re-resolves correctly after a load, because the old pieces are disabled before
    `FindFirstObjectByType` runs.
- **Audio:**
  - `VehicleAudio`: its source is parented to the vehicle and dies with it. It pauses when paused and stops on dismount.
  - `InteriorAudio`: pauses at level 0.
  - `SpeciesProjectile`: model colliders are destroyed before its first `Update`, it frees its material, and it has a
    3 s lifetime.
- **New MonoBehaviours in shared files** (`WaterSourceTarget`, `LabelRefresher`, the probe helpers) are only ever added
  at runtime, never serialised into a prefab.

## Not covered (out of time or out of scope)
- GPT's uncommitted stair navigation (`BuildingStairNavigation.cs`, and the `EnemyAI`/`RaidAgent` hooks). It is still
  being iterated and is not part of today's commits.
- Large M27/M28 files from `b8ca8f4` reviewed only at a glance: `RaidDirector.cs` (+331), `GarageWorkshopPiece.cs`
  (+453; the material rollback looked correct), `BunkerTerminalUI.cs` (+317, runtime usability of the controls not
  checked), `QuickSlots`, `Flashlight`, `RadiationSystem`/`RadiationZones`, `WaterSource`, the turret and trap pieces,
  `StructureDamageVisual`, `BuildModeController`, `HomesteadWorldGuard`, and all editor builders.
- Asset-side facts I could not see from code:
  - The FBX axis of `Drawer_*` parts. `LootContainer` slides drawers along the part's local +Z.
  - Whether door rotation under `FurnitureVisual`'s non-uniform `localScale` visibly shears the door.
- The runtime memory cost of `StaticBatchingUtility.Combine` in `ProceduralContent.FinishStructure` (new today) for
  chunks that stream in and out and are rebuilt at the bombs. Whether the combined meshes are freed on chunk destroy
  should be measured with the Profiler or `Resources.FindObjectsOfTypeAll<Mesh>().Length` in a probe.
- Streaming speed against a vehicle at 18–22 m/s. If a chunk's collider is not loaded yet, the rider falls through
  while the vehicle stays on the analytic height (see H1).
