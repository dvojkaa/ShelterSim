# SheltreSim - Design Notes (compact)

Current state, conventions, lessons and open work. Full dated build log, bunker-shop proposal and price list,
art audit, buildings/animation proposal and the complete Fable review:
**Full history: docs/archive/DESIGN_NOTES_full_2026-11-10.md** (verbatim copy, 727 lines). Append new dated entries
here, short; move old ones to the archive when this file grows past ~250 lines.

---

## 1. Pillars and core loop

First-person bunker-builder + survival game in a seeded, streamed wasteland (Fallout-inspired, small scope).
1. **Pre-apocalypse:** suburban lot, **$70,000**. Mandatory **$20,000 bunker contract** (stages 1-3 run automatically,
   stage 4 = the two-click SEAL button), fixed starter pack (`hammer`, 4 `canned_food`, 4 `bottled_water`, 2 `bandage`),
   remaining $50k spent in the terminal on rooms, kits, supplies, defence, vehicles. No enemies/hazards/loot yet.
2. **Bombs:** 45 s sirens, flash/quake, world switches to its Ruined version.
3. **Survive and scavenge:** 20-min day/night, weather (clear/fog/rain/snow), hunger/thirst/health, radiation,
   foraging, water, hunting, ruined towns and landmarks.
4. **Base and power:** 20+ build pieces, wireless power grid (25 m), farms, crafting stations.
5. **Raids:** first 900 s after the bombs, then every 10 min, then 6 min; every 5th wave has a Warlord.
6. **Endgame:** Radio Beacon (3 kW), 5-minute 3-wave assault, `VictoryLedger`, endless mode (raids currently stop after victory).

## 2. Feature set per milestone (what exists now)

* **M1-M7:** starter bunker, 2 m grid building, needs ticking, JSON save/load, scavenging, crafting, raiders.
* **M8-M12:** modal UI, melee (`MeleeAttacker`), `EnemyAI` on `CharacterController` with door-breaking (`ForceOpen`), 24,000 m2 map (superseded by M18).
* **M13-M17:** game-over reload, world persistence, raider respawn, bed, Blender art pass (M16), uncapped-FPS fix (`minMoveDistance = 0`).
* **M18-M19:** infinite streamed 128 m chunks (`loadRadius` 3 = up to 49 chunks, one `Generate` per frame), 6 biomes, roads/bridges, shallow rivers, 6 landmarks (hospital, factory, fuel station, farm, military outpost, abandoned bunker) with caches and expedition console.
* **M20 audio:** procedural generator `ArtSource/audio/gen_audio.py` (161+ clips at M20, more since), pause-menu volume sliders.
* **M20-M21 opening/crafting:** `HomeBase` + `SiteTerminal`; `ItemCatalog`/`RecipeBook` 96 items, 71 recipes, 6 stations (Workbench, Stove, Forge, Chem Lab, Electronics Bench, Fabricator); armor = flat reduction (`MaxDamageReduction`).
* **M22:** `PowerGrid` (solar 1.5 kW peak/0 at night, generator 6 kW/120 s per `fuel`, battery, cooling, pump 1 kW, irrigator 0.5 kW); 8 guns (pistol, SMG, shotgun, hunting rifle, assault rifle, laser pistol/rifle, plasma rifle, reload `R`); `HomesteadCatalog` 20+ pieces (repair `V`, dismantle `X`: 100% refund within 30 s, 50% after); `RaidDirector` with sandbags, spike/shock traps, alarm, MG turret (ammo) and laser turret (power).
* **M23:** 12 species (`SpeciesSpec`): Wanderer, Runner, Scavenger, Screamer, Plated, Breacher, Rootwalker, Mirelurker, Iron Boar + bosses Gatekeeper (factory), Root Colossus (farm), Shift Commander (military). Camps with locked chests, boss lairs with sealed storerooms, FBX takes Idle/Walk/Attack/Hit/Death.
* **M24:** flashlight `F`, crouch (`LeftCtrl`, 1.1 m), damage indicator, quick-slots 3/4/5, settings (sensitivity 0.03-0.4, FOV 60-100, FPS counter), `RadiationSystem` (Geiger, gas mask/hazmat/Rad-Away), `LightPiece`, damage visual stages (<65% damaged, <30% critical), `StorageTransfer`, Radio Beacon finale.
* **M25-M26:** crafting/build draw from pack then any crate within `NearbyCrateRadius` (100 m), fibre foraging, dirty-water filling, Scrap Bicycle (10.5/13 m/s, `P` 35 kg panniers), backpack frames (+20 kg each, max 120).
* **M27-M28:** telegraphed ranged enemies (Mirelurker acid 14 m / 1.1 s inhale / 65% dmg; Scavenger flask 11 m / 0.85 s / 55%); **Scout Buggy** (26/34 m/s, 120 kg trunk, `H` +700 m fuel can, `J` +500 m energy cell, `L` lights, `P` trunk, `K` horn); **Armored Van** (20/27 m/s, 300 kg, turret `T`: 45 dmg hitscan 70 m, 0.75 s, `ammo_rifle`); powered 2 kW **Garage Workshop** (45 s assembly, repair 150 HP at 4 HP/s within 12 m); brick/reinforced/concrete foundations and walls; structure damage from melee/firearms (18% wood, 8% stone, 4% metal for guns), 50% salvage bag, bottom-up collapse; vehicles are controller-driven box sweeps, not rigidbodies.
* **Bunker shop (implemented, playtest pending):** $70k budget, $20k contract, 30-offer catalogue ($800-$12,000, limits, wing exclusivity), Supply Cage deliveries (never in backpack), `kit_<pieceId>` kits paying a piece's whole cost, kit refund in the 30 s window, rooms (annex $4k, workshop or hydroponics wing $6k exclusive, vault $3.5k needs annex), reinforced door $3k, save version 7 with migration. Buggy $12k, MG turret $7.5k, workbench $1,200 are shop offers (no free station at start).
* **M29-M30 vehicles/audio/interiors (code+art done, human playtest pending):** `VehicleMountView` (eased camera to `Seat`, `Exit_L/R`, never hides renderers), terrain-following vehicles (rider slope limit 65 deg while mounted, snap-down 1.25 m), vehicle audio (`motor_buggy/van`, `bike_chain`, ignition, horn), no footsteps while riding; furnished two-storey houses and multi-floor apartment blocks with stairs (visual steps + sloped box ramp), authored furniture with animated doors/drawers (`LootContainer`), 8 `bld_door_*` entrance/room doors, `BuildingInterior` floor culling, interior room-tone audio; procedural melee swing curves, head bob, eased doors; raider stair navigation; suburb houses queued through per-frame building work (block stream 9.2 ms median).
* **Art wired:** vans, garage, projectile FX, tiered wall/foundation models + damage pairs, 5 held melee weapons (`MeleeViewmodel`), bunker props (cage, vault door+frame, pallet, tool walls, hydro tanks, emissive grow light), hanging `piece_light_ceiling`, 30 `furn_*`/`bld_*` prefabs. Landmark set-piece kit (35 `lmk_*`) and four ground-item models exist; `--landmark-shots` renders all six kinds. Animal/camp/`lm_*` models exist but wiring to spawners is partly open.

## 3. Architecture and conventions

* **Engine:** Unity 6000.6.8f1, Built-in RP, Linux target, single scene `M1_BunkerShell.unity` (no second scene, no `DontDestroyOnLoad`). New Input System with every `InputActionMap` hand-built in code (no `.inputactions`).
* **Determinism:** only seeded `System.Random` with literal seeds, rolled at editor-tool/generation time; never `UnityEngine.Random`. Worlds are built twice per chunk (normal, then ruined, `ProceduralWorld.Ruined`): anything drawn only in the ruined pass must not consume a random number another object depends on (use per-lot `SlotRandom`; always draw, then AND with `ruined`). Nothing lootable, gatherable or alive in the normal pass. Generation version is 4 (distance-ring enemy rosters and loot tiers changed the generated world).
* **Prefabs:** `ProceduralContent.Prop(id)` loads `Resources/Props/<id>`; `M22SystemsSetup` makes a prefab for every `furn_*`/`bld_*` FBX, `M21CraftingSetup.BuildPrefab` loads `Assets/Art/Models/<pieceId>.fbx` else a coloured cube. **A MonoBehaviour serialised into a prefab/scene needs its own file named after the class** (`grep -rlE 'm_Script: \{fileID: 0\}' Assets` must stay empty).
* **Art:** goes through `ArtSource/` (Blender scripts per `ArtSource/ASSET_SPEC.md`, palette atlas `SS_Atlas.png`) and `M16ArtPass`, never into milestone builders (M10/M12 derive placement from renderer bounds). Model origin = footprint floor centre, front -Y (animals/vehicles front +Y). Only one process may hold the Unity project; art agents run Blender only. Blender path: see memory `blender-install`.
* **Audio:** `GameAudio` + `ArtSource/audio/` (`gen_audio.py`, `build_audio.sh <section>`, clips in `Sfx.cs`). Gameplay makes a one-line `GameAudio.Play2D/PlayAt`, or `PlayerAudio`/`RaiderAudio`/`VehicleAudio`/`InteriorAudio` observe public state. Probes must not touch PlayerPrefs (use `GameAudio.DebugEffectsVolume`).
* **Messages:** player-facing feedback via `SheltreSim.UI.GameMessageBus.Post(string)`, not bare `Debug.Log`.
* **Physics sizes:** player capsule r 0.35 h 1.8; raiders/species r 0.45-0.5 (1.16 m with skin). Doorways **>= 1.8 m clear**, corridors >= 1.4 m, stair flights >= 1.4 m wide, <= 36 deg. Stairs: visual steps without collider + one sloped `BoxCollider`. `CharacterController` is a Collider (EnemyAI uses it; no extra `CapsuleCollider`).
* **HUD:** passive HUD canvas has no `GraphicRaycaster`; HUD graphics `raycastTarget = false`.
* **Crafting reach:** 25 m crate radius, enough for the home footprint while keeping remote outposts self-sufficient.
* **Verification commands:** `./tools/compile_check.sh` (Roslyn, ~2 s). Batch build: `<Unity>/Editor/Unity -batchmode -nographics -projectPath <repo> -executeMethod SheltreSim.EditorTools.BatchSetupAll.RunAll -quit -logFile <f>`; grep `error CS` and `Exception`. Unity is at `/home/dvojkaa/Unity/Hub/Editor/`. **Never** force Play Mode under `-batchmode -nographics` (hangs). Probes are standalone-player flags (`--smoke-test`, `--crafting-probe`, `--home-probe`, `--world-probe`, `--building-probe`, `--vehicle-probe`, `--vehicle-stream-probe`, `--m28-probe`, `--raid-probe`, `--place-probe`, `--homestead-probe`, `--weapons`, `--bestiary`, `--storage`, `--power`, `--encounters`, `--objectives`, `--structural-verification-probe`, `--raider-stair-probe`, `--worldgen-performance-probe`, `--interior-audio-probe`, `--held-tool-probe`, `--weather-probe`, `--interior-shots`, `--landmark-shots`). **Run silently:** `tools/run_probe_silent.sh <player-dir> --<probe> <log>`; never run `--audio-probe` on the user's speakers. Screenshots: `-batchmode` without `-nographics`, `ArtCapture.RebuildVerifyAndCapture`, read `ArtSource/previews/ingame_*.png`. Probes sometimes need a scratch copy of the project (check `DEV_COORDINATION.md`; don't run Unity while the user's editor holds the project).
* **Git:** no global identity: `git -c user.name="vojta" -c user.email="vojta@localhost" commit`. Roles: GPT = programming; Claude = Blender art, audio, verification.

## 4. Lessons / bug categories

* **Batch mode proves compilation only.** Real bugs it passed: enemy HP 0 from assignment order; death check behind wrong flag (alive at 0 HP); melee hit-detection broken by a review "fix"; unclickable inventory button (cursor locked).
* **Lifecycle/init ordering** (HP, shop reserve, stage resume) and **two systems half-managing shared state** (vehicle vs rider both owning position: vanishing car, under the map, cannot climb; bunker economy `ReservedForBunker`; Supply Cage vs raiders). Fixes so far are per-symptom; see open item 8.1 in the archive.
* **Misread Unity semantics:** `RootHeight` of a prefab is the footprint centre (vehicles sank 1.3-1.9 m); default `slopeLimit` 45 deg blocked uphill driving; scripts bundled in one file lose their prefab reference (turret/trap pieces were invisible); `FindObjectsByType`-style scans on gameplay paths; glass quads need `normal_update()`; closed hull boxes are back-face culled from the seat.
* **UI that compiles but cannot be used:** locked cursor, HUD raycast overlap on modal panels, clipped/ignored warnings.
* **Geometry reasoning is wrong; measure it.** A throwaway `_TempProbe.cs` with `Physics.OverlapCapsule`/`SphereCastAll` found buried crates (M10), sunk Water Collector (M11), 1.04 m doorway (now 1.8 m). Giving an actor a collider changes what it fits through.
* **Ruined-vs-normal pass drift:** a roll drawn only when ruined (house burn roll) shifts sheds/cars at the bombs.
* **Fork/sub-agent hygiene:** don't trust completion summaries; read files on disk; give narrow file ownership; wire shared files (`BatchSetupAll`, `M8MenusSetup`) yourself.
* **Probe pitfalls:** stale checks after design changes (workbench is now a shop offer; sealed flats drop `C##` slots by design); raider capsule tangent to floor (lift 2 cm); chunk replaced in the same frame still exists until end of frame (match by position); never infer state from behaviour when a real property exists (patrols held chase music on).
* **Skeptical review finds the integration bugs** (paused terminal stalling the apocalypse, legacy save resume, raiders stealing from the cage, vehicle height H1/H2, `LootContainer` doors sliding to origin: fixed). `CODE_REVIEW_2026-11-10.md` lists the 8 findings.

## 5. Current state (2026-11-10)

**Latest isolated-player verification (2026-11-10; not human-played):** Linux build succeeds (129,709,192 bytes). Current `--world-probe` passes across all six biomes and resource/revisit checks; `--building-clearance-probe`, `--structural-verification-probe`, `--raid12-perf-probe`, `--difficulty-probe`, `--seal-readiness-probe`, `--display-settings-probe`, `--enemy-pathfinding-probe`, and the full `--raid-probe` pass. Raid-12 spawned 14/14; frame time median 1.68 ms, P95 2.29 ms, max 31.70 ms. Compile is 230 runtime + 29 editor files. The raid probe logged an autosave write denied by the sandbox profile; its persistence checks therefore still need a normal user-profile run. `git diff --check` reports trailing whitespace in concurrent Claude animator and bunker-scene YAML edits. 0 prefabs/scenes with a missing script on the last Unity build.

**Not verified / open:** any human Play Mode session since interiors, shop, vehicles and doors landed; vehicle handling on real generated hills; Play Mode of garage outages, turret/ram cooldowns, same-frame dismount; audio levels (all generated, never heard); melee hold pose in motion; van cab view (headliner ~1/4 of view); stair/apartment feel; subjective hitch at vehicle speed. The isolated `--worldgen-performance-probe` now measures peak stream frames at median 11.38 ms (block) and 10.11 ms (suburb); the 3 km drive's worst sampled frame was 21.8 ms.

**Known limitations / deliberately unfixed:** one hand-laid raid route; raiders do not use each species' full combat mechanics; raids pause when the player is > 200 m (`HomeRange`); UI English-only; human testing of vehicle feel, balance and audio is still open; two historical build folders and many uncommitted paths remain; flash-damage on pre-bomb surface pieces is undecided. Outdoor enemy pathfinding and Easy/Normal/Hard selection now pass isolated runtime probes; human playtesting remains open.

## 6. Open work (Fable review 2026-11-10, condensed)

Verdict: more systems than tension. Early purchases solve survival, raids cannot threaten a prepared player, victory removes pressure. Gains are in tuning and connecting, not new features. Nothing below was played; owner GPT = code and behaviour, Claude = Blender art, audio, verification.

**Ranked Top 10**
| # | Change | Owner |
|---|---|---|
| 1 | One clean committed build, then a 45-minute human session with a short form; delete second build folder, show commit hash on pause menu | Claude (build/test pack) + human |
| 2 | Needs/supply retune: about a third of drain (hunger 0.30/s, thirst 0.42/s now; target ~0.10/0.14), scale `WaterCollectorPiece.secondsPerUnit` (90 s) and `FarmPlot.growSeconds` (240 s); indoor plots need powered grow light; bed 40 HP per press -> one rest per night/cooldown; night effects | GPT |
| 3 | Raid scaling: Raider 30 HP/5 dmg per 1.5 s, Gunner 35/8, Brute 90/10, Warlord 240/18 never scale (count 2+n, cap 14); add ~8%/raid growth, first raid at 8-10 min with 4 attackers, gunner range > 15 m | GPT |
| 4 | Vehicle correctness: close code-review H1/H2 (height from floor, buggy stop vs rider), fuel range to match speeds (can 700 m = 20-27 s of driving; target ~4 km/can, 12 km tank, km on HUD), long-drive streaming probe (implemented and passing) | GPT |
| 5 | Autosave (seal, dawn, after raid, rotating file) and main menu Continue/New | GPT |
| 6 | Opening checklist (5 steps pre-bomb, 5 after) and three preset loadouts (Homesteader/Fortress/Roamer); seal confirm lists red dependency warnings | GPT owns behaviour; later |
| 7 | Multi-direction raids targeting surface assets (solar, collectors, generator, vehicle), power sabotage, alarm direction call-out | GPT |
| 8 | Wire finished art: wildlife, camps, landmark kit, ground items, hands (gloved forearm on viewmodels) | Claude models/screens; GPT hooks |
| 9 | Distance rings for loot/enemies (<600 m, <1.5 km, beyond), mixed groups, species in raids (from raid 4); boss arena lock + add wave; phase roar/gate sound and model | GPT behaviour; Claude art/audio parts |
| 10 | Pressure after victory, widen expedition tree (3 upgrades at 3/4/5 `expedition_parts` now), radio trader sink | later, GPT |

**Implementation update (2026-11-10):** #2 needs/farm/bed pacing, #3 raid scaling, #5 autosave/main menu, #6 state-derived opening/survival checklist plus editable loadout presets, #7 multi-side surface-target raids, #10 post-victory hordes/trader, and the core of #9 (distance-ring loot/enemy groups) are implemented in source. #4 fuel now gives 4 km per can / 12 km per tank (1.5 km per energy cell) with mounted HUD range and unit updates; `--vehicle-stream-probe` drives 3 km and checks streamed ground each frame. #8's landmark and ground-loot hooks are wired. Boss lairs have phase adds, arena barriers, and a Root Colossus lunge. Generated sealed apartment entrances now take tool/firearm damage and retain their destroyed state when chunks unload or saves reload. Kill and melee feedback now uses the actual enemy name. The current isolated Linux build succeeds; `--world-probe`, `--building-clearance-probe`, `--structural-verification-probe`, `--raid12-perf-probe`, `--vehicle-stream-probe`, and `--encounters-probe` pass. Claude still needs landmark visual sign-off. Existing raid variants already provide mixed enemy rosters.

**Other Fable items worth keeping (GPT unless noted):** shop Tool Set (iron tools + machete) skips the early ladder; crafting UI search/pin and "can craft now" filters are implemented and pass `--crafting-probe`; raid-destroyed salvage is 25%; message priority (banner vs feed, per-hit text off); exposure/shelter status in HUD; inventory pauses the world (design call); sealed flats (40%) are breakable and persist as destroyed across chunk streaming/saves; atlas "cleared" building markers persist; paper map is in the signed-contract starter pack and gates the world map; radio news countdown starts at 10 minutes after contract and passes `--radio-news-probe`; gameplay scene-wide scans in `RaidAgent`, `BuildingStairNavigation`, `BedPiece`, and `AlarmPiece` now use registries (recurring enemy/wildlife audio scans remain Claude-owned in `GameAudio`); raid-12 frame-time probe passes in the built player; generated resource/container depletion, chunk revisits, and F5/F9 restoration pass `--procedural-save-probe`; hand-test bombs while inside a house, save mid-seal, load mid-raid, load while mounted. Claude: window light quads, hit-reaction check, one human listening session on audio levels.

**Not recommended now:** more build pieces, weapons, recipes, a second scene, full rigidbody vehicles.

**Open playtester requests (not done):** landmark/ground-loot art is hooked to generation and has a six-type screenshot pass; verify it in the isolated built player and resolve the reported facade/deck/terrain gaps. Animal collision-aware movement is implemented in source (boar gore preserved); verify in the built game. Vehicle feel beyond the current pass (acceleration/steering lag, visible steering wheel/wheel animation, bicycle pedals/handlebar, authored interior) and confirmation of speeds (26/34 and 20/27 m/s); human confirmation that vehicles stay visible when mounted and parked after dismount, plus exits on slopes; bunker budget balance pass (buggy dominance, MG turret strength, workshop vs hydroponics); distinct acid/flask projectile visuals (Claude) and dodge-window balance.

## Fable detail review — wrong details and the work split (2026-11-11)

Read-only pass over the player-facing flow (menus, opening, HUD, inventory/crafting, combat, vehicles, raids, landmarks) plus the screenshots in the Claude job tmp folder (`ishots`, `ishots2`, `seat2`, `lshots3`) and `ArtSource/previews`. Nothing was run. Every item was seen in code or in a screenshot unless marked **[unverified]**; "[coords]" means the overlap follows from the anchored rectangles, not from a screenshot. 40 defects, no blocker, 15 major, 25 minor. Effort: S = 1, M = 2, L = 3 (no L found).

### Defects

1. **D01** (major, S, GPT) Game Over "New game" lands on the start menu again, the same bug as the fixed New Game button. `GameOverScreen.RestartGame` (`Core/GameOverScreen.cs:82-92`) reloads the scene without setting `MainMenuController.startNewGameImmediately` (private, `Core/MainMenuController.cs:119`).
2. **D02** (major, S, GPT) Pause > "Main menu" has no way back to the running game. The only buttons are Continue, which loads the last save and silently discards unsaved progress, and New Game (`MainMenuController.cs:108-117`, `PauseMenu.cs:168-172`). Needs a Resume button when a run is live, or a confirm.
3. **D03** (major, S, GPT) F5 saves under any modal, including the start menu that sits over a freshly booted world: one press replaces the manual save with an empty new run (the old one survives only as `.bak` until the next F5). `SaveManager.cs:77`, `122-128`, `211-224` has no `ModalUI`/menu guard.
4. **D04** (major, S, GPT) A pending autosave (dawn, raid survived, sealing) retries every 5 s and posts "Dismount before saving..." to the HUD feed each time while the player drives, and "Cannot save after death..." into the Game Over status line. `SaveManager.cs:115-119` + `215-224` post regardless of `showMessage`.
5. **D05** (major, M, GPT) If the 600 s radio deadline starts the bombs before the player seals, stage 4 never completes: `BunkerComplete` stays false, so `RaidDirector.SystemLive` (`Raids/RaidDirector.cs:151-152`) is false for the whole run (no raids), the laptop is "dead" (`CanShop`), and the delivery pallet and `removedWhenComplete` props stay forever. `RadioNewsCountdown.cs:77-84`, `HomeBase.cs:75`, `142-152`. Finish the bunker (or treat it as complete) when the bombs fall.
6. **D06** (major, S, GPT) Mouse left/right does nothing in the buggy and van. `FirstPersonController.HandleLook` writes `carLookYaw` to the camera pivot (`Player/FirstPersonController.cs:213-229`), then `VehicleMountView.LateUpdate` (`World/VehicleMountView.cs:212-216`) overwrites the pivot rotation with seat rotation and pitch only. Once it is applied, also fix the lag term: `carLookYaw -= turn * 0.35` (line 225) accumulates and only recentres after 1.5 s of zero mouse movement, so the view would drift outward in a long turn [the drift is reasoned from code, not seen].
7. **D07** (major, S, GPT) Steering depends on the key, not on speed: with an empty tank or the nose against a wall, W + A/D rotates the car on the spot, and the turn rate is the same at 1 m/s and 34 m/s (`FirstPersonController.cs:219-226`). Scale by actual vehicle speed.
8. **D08** (minor, S, GPT) Reverse runs at full forward speed, 26/34 m/s buggy and 20/27 m/s van (`FirstPersonController.cs:248-249` uses the same `speed` for negative input).
9. **D09** (major, M, GPT) The held melee weapon stays in the driver's view (both `seat2` shots: a large bat across the right third of the windscreen), and melee swings, gunfire, weapon switch, build mode and E-interact all still work from the seat. No `IsRiding` check in `Combat/MeleeAttacker.cs:123-132`, `Combat/MeleeViewmodel.cs:50-71`, `Combat/RangedWeaponController.cs:246`, `BuildModeController`, `PlayerInteractor`. Hide both viewmodels and gate attacks while mounted (van turret `T` stays).
10. **D10** (major, S, GPT) The held melee pose is too big and too central on foot: the default bat runs from the bottom centre up past crosshair height at about 65-78 % of the screen width (`lshots3/*_entrance.png`). Compare the older, smaller pose in `ArtSource/previews/ingame_first_person.png`. `MeleeViewmodel.HeldRotation` (`MeleeViewmodel.cs:18`) with the grip at (0.35, -0.25, 0.55) from `Assets/Editor/M8MenusSetup.cs:254`.
11. **D11** (major, S, GPT) The encumbrance warning is drawn on top of the checklist panel: both anchor at top-right (-24, -274) (`Storage/Encumbrance.cs:217-218`, `Objectives/ObjectiveTracker.cs:544-545`). Visible in `seat2/piece_armored_van_seat.png` ("Overloaded: no sprint" over "SURVIVAL 1/5").
12. **D12** (minor, S, GPT) The raid warning banner (top centre, y -86..-140, `RaidDirector.cs:132`) overlaps the build-mode readout (y -78..-206, `Survival/SurvivalHUD.cs:94-95`), exactly when the player builds defences during the warning [coords].
13. **D13** (minor, S, GPT) Passive HUD canvases stay on under modals: the red "Overloaded" label shows through the inventory and terminal backdrops (`ishots/*.png`, `ishots2/*.png`). `Encumbrance.cs:214`, `World/RadiationSystem.cs:204`, `RaidDirector.cs:131`, `Core/RadioNewsCountdown.cs:174` never test `ModalUI.IsOpen` (SurvivalHUD, PlayerStatusHUD and ObjectiveTracker do).
14. **D14** (minor, S, GPT) Stale or wrong menu texts: pause advice still says "Water Collector before using the red bunker console... raiders can still hurt you" (`PauseMenu.cs:150-152`; the console is gone and there are no enemies before the bombs); Victory hint says "Raids are over" while hordes return every 15 minutes (`Objectives/VictoryLedger.cs:36`); Game Over says "F9 loads the manual F5 save" when only an autosave exists (`GameOverScreen.cs:35-40`).
15. **D15** (minor, S, GPT) HUD guidance uses stale data: the survival hint counts only `food_berries` and `water`, so it says "Find berries" with canned food in the pack and "Collect water" with bottled water (`SurvivalHUD.cs:243-244`, `270-273`); the pre-bomb card says "Next: Excavation, $5,000" although the $20,000 contract pays every stage and sealing is free (`SurvivalHUD.cs:261-263`).
16. **D16** (minor, S, GPT) Control hints are incomplete: on the bicycle the bottom strip still shows on-foot controls (`SurvivalHUD.cs:193-203` handles buggy and van only); the pause list has no vehicle keys (`PauseMenu.cs:132-146`); "No capsule-clear space to leave the buggy/van/bicycle" is developer wording (`RideableScoutBuggy.cs:550`, `RideableArmoredVan.cs:573`, `RideableBicycle.cs:185`).
17. **D17** (minor, S, GPT) The start menu has no Quit button; pause "Quit" and "Main menu" act at once with unsaved progress, and Quit sits between Load and Main menu (`MainMenuController.cs:155-166`, `PauseMenu.cs:123-126`).
18. **D18** (minor, S, GPT) Melee feedback: every air swing posts "Swing missed — move closer and aim at the raider." (`MeleeAttacker.cs:167`), and because the starter pack contains a hammer, any stray left click at your own crate, bed or wall damages it (`MeleeAttacker.cs:175-210`). Drop the miss text; require an equipped tool or a deliberate input for own structures (design call).
19. **D19** (minor, M, GPT) Biome ground colours change along straight, axis-aligned edges: bright orange and green rectangles meet without blending in `lshots3/MilitaryOutpost_threequarter.png`. `World/ProceduralTerrain.cs` [seen; cause not read].
20. **D20** (minor, S, GPT) Two guidance panels disagree: the SurvivalHUD card says "Build a Water Collector... press B" while the checklist below says "SURVIVAL 1/5 Drink clean water" at 100/100 water, when drinking is refused; step 2 needs a station, which a zero-purchase run does not have (`SurvivalHUD.cs:270-277`, `ObjectiveTracker.cs:482-486`). Let the checklist own the card while it is active.
21. **D21** (minor, S, GPT) The title reads "SHELTER SIM" (`MainMenuController.cs:151`, `VictoryLedger.cs:26`) while the product, window and binary are "SheltreSim". Pick one.
22. **D22** (minor, S, Claude*) Terminal tab "POWER & WATER" is clipped to "POWER &" (`Core/BunkerTerminalUI.cs:25`, `151`, tab 126 px wide; `ishots2/all.png`).
23. **D23** (minor, S, Claude*) Terminal stage list shows "LOCKED $5,000 / $8,000 / $3,000 / $4,000" under a contract text that says it pays all four stages, and stage 4 stays "LOCKED $4,000" after signing (`BunkerTerminalUI.cs:368`; `ishots2/all.png`). The room header says "NEEDS CONCRETE SHELL" although Vault and Reinforced Door need stage 3.
24. **D24** (major, S, Claude*) The seal readiness text overflows its column: label at y -736, 102 high, inside a 744-high column (`BunkerTerminalUI.cs:138-139`), so its lines run across the status line at card y -864 (`:164`) and below the card edge [coords].
25. **D25** (minor, S, Claude*) The seal confirmation lasts 3 s, too short to read up to four warnings; after it expires the button reverts but the warnings and "Click again within 3 seconds" stay (`BunkerTerminalUI.cs:263-275`, `413-415`).
26. **D26** (minor, S, Claude*) Offer rows print raw ids ("anti rad", "first aid", "kit piece turret mg") instead of `ItemCatalog.NameOf`, and "or craft:" shows the recipe of the first grant only as if it were the bundle (Pantry Bundle: "2 cooked meat, 1 metal plate, 1 chemicals"; Builder Pallet: "2 wood") (`BunkerTerminalUI.cs:188-232`; `ishots2/all.png`).
27. **D27** (minor, S, Claude*) Crafting window: the active category filter is not highlighted (`Crafting/CraftingUI.cs:161-166`), ingredients read "Planks 4:2" (owned:required, `:303-305`), and pins are a static set that is not saved and survives New Game (`:51`).
28. **D28** (minor, S, Claude*) The ammo card (lower right, y 96..192, 400 wide, `Combat/RangedWeaponHud.cs:33`) overlaps the flashlight card (y 116..166) and quick slot 5 (y 174..214) (`UI/PlayerStatusHUD.cs:70-83`) [coords]. It also stays visible under modals (see D13).
29. **D29** (minor, S, Claude) The hospital ambulance stands on the entrance axis at (0, 0, -8), 6 m in front of the door (`World/ProceduralLandmarks.cs:267`); the comment at `:140` says the axis stays clear. `lshots3/Hospital_threequarter.png` [door reachability around it not measured].
30. **D30** (major, M, Claude) The inventory Build tab lists only the five legacy pieces (Wall Segment, Floor Tile, Workbench, Bed, Storage Crate) of 20+ (`ishots/build.png`; `Items/InventoryUI.cs:38`, `209-219`), and counts the pack only (`:513-521`) although building also draws from nearby crates.
31. **D31** (minor, S, Claude) The inventory shows no carry load and no item weight, while the player can be at 294/60 kg (`ishots/selected_machete.png`; `InventoryUI.cs:418`, `444`).
32. **D32** (minor, S, Claude) The inventory header says "WEAPON Fists  DAMAGE 20" while the player visibly holds a spiked bat (`InventoryUI.cs:449`), and the Build tab uses the same "Planks 4:2" format (`:522`).
33. **D33** (major, S, Claude) The last `--inventory-shots` run (`ishots2`, 21:12) captured the bunker terminal in all six frames, five of them byte-identical; the redesigned inventory and the recipe icons were therefore not screenshot-checked on the final tree. `Diagnostics/InventoryShotProbe.cs` ignores the result of `ui.Open()` [what opened the terminal is unverified].
34. **D34** (major, S, Claude) `--seat-shots` never shows the van: its "dismount" calls `Interact`, which returns at once when mounted (`RideableScoutBuggy.cs:210`), so the second frame has the buggy HUD line, no "Van controls" message and an open cage unlike the closed van model. It also leaves build mode on (build readout in both frames). `Diagnostics/SeatShotProbe.cs:59-65`.
35. **D35** (minor, M, Claude) The van driver view is therefore unchecked; this file already lists "headliner ~1/4 of view" as open. Re-shoot after D34 and adjust `ArtSource/assets/piece_armored_van.py` (seat eye point, windscreen) **[unverified]**.
36. **D36** (major, M, Claude) Every landmark stands on a 0.8 m slab carried by four 2 m footings with an open underside and its shadow on the ground below: it reads as a floating platform (all six `lshots3/*_threequarter.png`; `ProceduralLandmarks.cs:126-133`). Add skirt walls down to the terrain.
37. **D37** (minor, S, Claude) Landmark materials: main-building roofs use the brick wall material (`ProceduralLandmarks.cs:236`), the ramp is brick against the asphalt deck (`:136-137`), and the farm's crop beds stand on asphalt.
38. **D38** (minor, S, Claude) `--landmark-shots` entrance frames include the held bat, and the Hospital entrance camera sits inside the ambulance bonnet (`lshots3/Hospital_entrance.png`): hide viewmodels and move the camera in `Diagnostics/LandmarkShotProbe.cs`.
39. **D39** (minor, M, Claude) Stale numbers in docs: this file's section 1 (raids "900 s", code 540 s on Normal with 600 - 30n, minimum 360) and section 2 (buggy $12k and MG $7.5k, code $18,000 with Annex and $9,000 with Workshop; price range to $18,000; "100 m" crate radius, code 25 m); `SETUP.md:98-100` (96 items / 71 recipes, 100 m), `:176` (first raid ~15 min); `README.md:40` (96 items, 71 recipes; the catalogue has 97 + 19 kits).
40. **D40** (minor, S, Claude) `GameAudio` still scans the whole scene for enemies and wildlife (`Audio/GameAudio.cs:467`, `651`); GPT moved the gameplay scans to registries and left these two [how often they run was not read].

Checked and fine: `ModalUI` ownership and Escape ordering between terminal, crafting and pause; quick slots, flashlight, build and interact are gated under modals; the legacy `ApocalypseTrigger` console is no longer in the scene; shop presets fit the Normal budget.

### The split

Rule: by file ownership, with no file on both sides. Folders not named in the standing roles (`Storage`, `UI`, `Interaction`, `Power`, `Player`, `Assets/Editor`) are treated as GPT gameplay code. By ownership alone the load is GPT 31 / Claude 16, so three GPT-owned files are handed to Claude for this round, marked `Claude*`: `Core/BunkerTerminalUI.cs`, `Crafting/CraftingUI.cs`, `Combat/RangedWeaponHud.cs`. GPT has no task in them. This and the `ProceduralLandmarks.cs` visual work need GPT's OK in `DEV_COORDINATION.md`; if GPT declines, D22-D28 go back and the totals are 31 / 16.

**GPT — 21 tasks, effort 24**

| Order | ID | Task | Effort | Running total | Files GPT may touch |
|---|---|---|---|---|---|
| 1 | D03 | Refuse F5 under a modal or menu | 1 | 1 | `Saving/SaveManager.cs` |
| 2 | D04 | Silent autosave retries | 1 | 2 | `Saving/SaveManager.cs` |
| 3 | D01 | Game Over "New game" starts a run | 1 | 3 | `Core/GameOverScreen.cs`, `Core/MainMenuController.cs` |
| 4 | D02 | Resume or confirm on the start menu from pause | 1 | 4 | `Core/MainMenuController.cs`, `Core/PauseMenu.cs` |
| 5 | D17 | Quit on the start menu, confirm on pause Quit/Main menu | 1 | 5 | `Core/MainMenuController.cs`, `Core/PauseMenu.cs` |
| 6 | D05 | Bombs without a seal still complete the bunker and start raids | 2 | 7 | `Core/HomeBase.cs`, `Core/RadioNewsCountdown.cs`, `Core/GameStateManager.cs`, `Raids/RaidDirector.cs` |
| 7 | D06 | Car free look applied, lag term bounded | 1 | 8 | `World/VehicleMountView.cs`, `Player/FirstPersonController.cs` |
| 8 | D07 | Steering scaled by real speed | 1 | 9 | `Player/FirstPersonController.cs`, `World/RideableScoutBuggy.cs`, `World/RideableArmoredVan.cs` |
| 9 | D08 | Slower reverse | 1 | 10 | `Player/FirstPersonController.cs` |
| 10 | D09 | No held weapon, attacks, build or interact while mounted | 2 | 12 | `Combat/MeleeAttacker.cs`, `Combat/MeleeViewmodel.cs`, `Combat/RangedWeaponController.cs`, `Combat/RangedViewmodel.cs`, `Building/BuildModeController.cs`, `Interaction/PlayerInteractor.cs` |
| 11 | D10 | Smaller, lower held pose on foot | 1 | 13 | `Combat/MeleeViewmodel.cs`, `Combat/MeleeAttacker.cs`, `Assets/Editor/M8MenusSetup.cs` |
| 12 | D11 | Move the encumbrance label off the checklist | 1 | 14 | `Storage/Encumbrance.cs`, `Objectives/ObjectiveTracker.cs` |
| 13 | D13 | Hide passive HUD canvases under modals | 1 | 15 | `Storage/Encumbrance.cs`, `World/RadiationSystem.cs`, `Raids/RaidDirector.cs`, `Core/RadioNewsCountdown.cs` |
| 14 | D12 | Raid banner clear of the build readout | 1 | 16 | `Raids/RaidDirector.cs`, `Survival/SurvivalHUD.cs` |
| 15 | D15 | HUD hints from real food/water counts and contract price | 1 | 17 | `Survival/SurvivalHUD.cs` |
| 16 | D20 | One guidance owner while the checklist is active | 1 | 18 | `Survival/SurvivalHUD.cs`, `Objectives/ObjectiveTracker.cs` |
| 17 | D16 | Bicycle strip, vehicle keys in pause, plain exit wording | 1 | 19 | `Survival/SurvivalHUD.cs`, `Core/PauseMenu.cs`, `World/Rideable*.cs` |
| 18 | D14 | Stale pause, victory and game-over texts | 1 | 20 | `Core/PauseMenu.cs`, `Objectives/VictoryLedger.cs`, `Core/GameOverScreen.cs` |
| 19 | D18 | Miss text off, own structures need intent | 1 | 21 | `Combat/MeleeAttacker.cs` |
| 20 | D21 | One game title | 1 | 22 | `Core/MainMenuController.cs`, `Objectives/VictoryLedger.cs` |
| 21 | D19 | Blend biome colours across chunk edges | 2 | 24 | `World/ProceduralTerrain.cs` (bump `WorldLayout.GenerationVersion` only if geometry changes) |

**Claude — 19 tasks, effort 23**

| Order | ID | Task | Effort | Running total | Files Claude may touch |
|---|---|---|---|---|---|
| 1 | D33 | Inventory shots fail loudly and own the modal | 1 | 1 | `Diagnostics/InventoryShotProbe.cs` |
| 2 | D34 | Seat shots really dismount and mount the van, build mode off | 1 | 2 | `Diagnostics/SeatShotProbe.cs` |
| 3 | D38 | Landmark shots without viewmodel, hospital camera moved | 1 | 3 | `Diagnostics/LandmarkShotProbe.cs` |
| 4 | D24 | Seal readiness text inside the card | 1 | 4 | `Core/BunkerTerminalUI.cs`* |
| 5 | D25 | Longer seal confirm, consistent reset | 1 | 5 | `Core/BunkerTerminalUI.cs`* |
| 6 | D22 | Tab label fits | 1 | 6 | `Core/BunkerTerminalUI.cs`* |
| 7 | D23 | Stage list without per-stage prices, correct room header | 1 | 7 | `Core/BunkerTerminalUI.cs`* |
| 8 | D26 | Item names and honest craft hint in offers | 1 | 8 | `Core/BunkerTerminalUI.cs`* |
| 9 | D30 | Build tab from the real piece list, pack + crates | 2 | 10 | `Items/InventoryUI.cs` |
| 10 | D31 | Load and item weight in the inventory | 1 | 11 | `Items/InventoryUI.cs` |
| 11 | D32 | Weapon label matches the hand, "4/2" format | 1 | 12 | `Items/InventoryUI.cs` |
| 12 | D27 | Filter highlight, "4/2" format, pins reset per run | 1 | 13 | `Crafting/CraftingUI.cs`* |
| 13 | D28 | Ammo card above the quick slots, hidden under modals | 1 | 14 | `Combat/RangedWeaponHud.cs`* |
| 14 | D36 | Skirt walls under the landmark deck | 2 | 16 | `World/ProceduralLandmarks.cs` (visual only) |
| 15 | D37 | Roof, ramp and farm ground materials | 1 | 17 | `World/ProceduralLandmarks.cs` (visual only) |
| 16 | D29 | Ambulance off the entrance axis | 1 | 18 | `World/ProceduralLandmarks.cs` (visual only) |
| 17 | D35 | Van cab view re-shot and fixed | 2 | 20 | `ArtSource/assets/piece_armored_van.py`, `ArtSource/assets/_car_common.py`, `Assets/Art/Models/piece_armored_van.fbx` |
| 18 | D40 | Audio enemy/wildlife lookups without scene scans | 1 | 21 | `Assets/Scripts/Audio/GameAudio.cs` |
| 19 | D39 | Docs numbers | 2 | 23 | `DESIGN_NOTES.md`, `SETUP.md`, `README.md` |

Interfaces between the sides: D09 (GPT) is checked by D34's fixed `--seat-shots` (Claude): no viewmodel in either seat frame. D10 (GPT) is checked by D38's `--landmark-shots` only after that probe stops hiding the viewmodel for one dedicated frame, `held_pose.png`. D27/D32 share the "owned/required" text format: `4/2`, red when short. D30 reads `BuildModeController.availablePieces` and `CraftingSystem.CountInCrates`, both existing public members, so GPT changes nothing for it. D36/D37/D29 keep every collider, room box and `GenerationVersion` as they are.
