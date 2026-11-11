# SheltreSim — Setup & Playtest Guide

**Tech:** Unity 6000.6.8f1 · Built-in Render Pipeline · Linux target · `M1_BunkerShell.unity`
**Compile check (2 s, no Unity needed):** `./tools/compile_check.sh` → `COMPILE OK (180 runtime + 29 editor files)`

---

> **Run probes silent:** `tools/run_probe_silent.sh Builds/Linux --smoke-test /tmp/smoke.log` starts the player with no audio device so tests make no noise. Do not run `--audio-probe` unless you need the audio mix.

## Quick Start

### Standalone (Linux testers — no Unity)

```sh
./Builds/Linux/SheltreSim.x86_64
```

> [!WARNING]
> The Linux binary in `Builds/` may lag behind working-tree scripts if a new milestone was merged
> without rebuilding. Run `./tools/compile_check.sh` to confirm the script count matches.

### From Unity Editor

```sh
/home/dvojkaa/Unity/Hub/Editor/6000.6.8f1/Editor/Unity -projectpath /home/dvojkaa/git/ShelterSim
```

Then open `Assets/Scenes/M1_BunkerShell.unity` and use:
- **`Tools > SheltreSim > ▶ START GAME`** — rebuilds the full scene and enters Play Mode in one click.
- **`Tools > SheltreSim > Verify Current Game`** — scene wiring, recipe accounting, loot-visibility checks.

> [!IMPORTANT]
> Never call `EditorApplication.isPlaying = true` under `-batchmode -nographics` — it hangs forever.
> Always use the standalone probes listed below for headless regression.

---

## Diagnostic Probes (Headless, Standalone Binary)

```sh
# Core systems regression
./Builds/Linux/SheltreSim.x86_64 -batchmode -nographics --smoke-test     -logFile /tmp/smoke.log
./Builds/Linux/SheltreSim.x86_64 -batchmode -nographics --crafting-probe -logFile /tmp/crafting.log
./Builds/Linux/SheltreSim.x86_64 -batchmode -nographics --world-probe    -logFile /tmp/world.log
./Builds/Linux/SheltreSim.x86_64 -batchmode -nographics --m28-probe      -logFile /tmp/m28.log
./Builds/Linux/SheltreSim.x86_64 -batchmode -nographics --place-probe    -logFile /tmp/place.log   # turrets and the ceiling light can be previewed and placed

# Check for PASS/FAIL summary at end of each log
grep -E "(PASSED|FAILED|ERROR)" /tmp/smoke.log
```

---

## Current Playtest Checklist

Work through each section in a fresh standalone build or Editor Play session. Items marked
**[AUTO]** are covered by the headless probes above; items marked **[HUMAN]** require eyes.

---

### Pre-Apocalypse Phase

- [HUMAN] Spawn at the laptop on the suburban lot with **$70,000**. Streets and houses visible; no enemies, loot nodes, or animals present yet.
- [HUMAN] Sign the mandatory $20,000 bunker contract. Verify the $50,000 remainder, starter pack, and that all other purchases are locked before signing.
- [HUMAN] Check the starter room has no free bed or workbench; those are paid room/shop choices. A zero-purchase run still has the starter pack and can scavenge/craft outdoors.
- [HUMAN] Stages 1–3 run automatically; buy selected rooms, kits and supplies while they progress. Delivery goes into the fixed Supply Cage and survives save/load.
- [HUMAN] Seal the bunker only after stage 3; verify the two-click warning shows savings lost, closes the pausing terminal, resumes time and starts life support. The shop stays closed during sealing. A zero-purchase run must still be survivable.
- [HUMAN] Complete Stage 4 → 45 s sirens → flash/quake → world switches to Ruined state.
- [HUMAN] Being caught above ground during bombs deals damage; being underground does not.

---

### Survival & World

- [HUMAN] Walk the ruined wasteland: 6 biomes, roads, rivers, abandoned vehicles, ruined buildings.
- [HUMAN] 6 landmarks visible on atlas (`M`): hospital, factory, fuel station, farm, military outpost, bunker.
- [HUMAN] 20-min day/night cycle, weather changes (clear → fog → rain → snow). Rain stays outside roofed areas.
- [HUMAN] HUD: health (red), hunger (brown), thirst (blue), radiation (purple) bars all tick correctly.
- [HUMAN] Passive decay starts only after the apocalypse triggers. Raider damage is active from the start.
- [AUTO]  Save (`F5`) and load (`F9`) round-trip preserves position, stats, inventory, placed pieces.
- [AUTO]  Save format v5/v6/v7 supported (v7 current, migrated from older); saves v0–v3 are rejected with a clear error.

---

### Gathering & Foraging

- [HUMAN] Trees, rocks, berry bushes: `E` to gather, 3 charges per node, then depleted.
- [HUMAN] Tall grass (`node_tall_grass`) and river reeds (`node_reeds`): 1–2 plant fiber per hand harvest. Axe/machete clears instantly with occasional seed drops. Grasses must not block building entrances.
- [HUMAN] Empty bottle at a river/lake: interact prompt → collect Dirty Water (`WaterSource`).
- [HUMAN] Water Filter crafted at Chem Lab converts Dirty → Clean Water.
- [HUMAN] Hunt deer and boars for raw meat and leather.

---

### Crafting & Inventory

- [HUMAN] `C` = Hand crafting. `I` / `Tab` = Inventory. `E` on stations (Workbench, Stove, Forge, Chem Lab, Electronics Bench, Fabricator).
- [AUTO]  96 items and 71 recipes registered (ItemCatalog / RecipeBook).
- [HUMAN] Crafting UI shows **"in pack + in storage"** totals when base crates are within 100 m.
- [HUMAN] Materials drawn from player pack first, then from nearby crates (100 m radius). Crates >100 m must not contribute.
- [HUMAN] Quick-stack at a storage crate: matching items move from pack to crate without touching unrelated slots.
- [HUMAN] Quick consumables: assign medicine/food/drink to `3`/`4`/`5` from Inventory and use each key; verify counts and effects. Unassigned slots use their default item.
- [HUMAN] Craft Backpack Frames (Workbench, tiers 1–3). Each frame adds +20 kg (up to 120 kg). Load HUD and sprint penalty update; tier 4 cannot be installed; tier survives save/load.

---

### Building & Base

- [HUMAN] `B` = build mode. Scroll = cycle 20+ pieces. `R` = rotate. Left-click = place. `Escape` = exit build mode (does NOT also open pause menu).
- [HUMAN] Ghost tints green (clear) / red (blocked) / yellow (can't afford). Counts update immediately after spending.
- [HUMAN] Build a small shelter: foundation → half-walls → window walls → steel walls → door → roof. Rain stays outside.
- [HUMAN] `V` = repair a damaged piece (requires Metal Repair Kit). `X` = dismantle (50% material refund; 100% within 30 s of placing).
- [HUMAN] Structural damage visuals: `< 65%` HP → `_damaged` model. `< 30%` HP → `_critical` model. Visuals restore after repair.

---

### Power Grid

- [HUMAN] Place Solar Panels and/or Generator (fuel required). Connect via proximity (25 m wireless range).
- [HUMAN] Two generators without a Cooling Unit → overheat after a while. Add Cooling Unit to prevent.
- [HUMAN] Battery banks store excess and discharge during night/low-sun.
- [HUMAN] Automated devices (water pump, irrigator, cell charger, fabricator, repair station) activate when grid has power.
- [HUMAN] Ceiling lights and floodlights (`LightPiece`): turn off when power drops, smooth fade.
- [HUMAN] Save and load while machines are running; states must restore correctly.

---

### Combat & Weapons

- [HUMAN] Melee: `1` → left-click to swing. No friendly fire on turrets.
- [HUMAN] Firearms/energy: `2` → aim → shoot. `R` = reload. Hold for auto (SMG, assault rifle). Laser overheats; plasma bursts. All 8 weapons: pistol, SMG, shotgun, hunting rifle, assault rifle, laser pistol, laser rifle, plasma rifle.
- [HUMAN] Ammo types: 9 mm, rifle, shell, energy cells. Heat dissipates when not firing.

---

### Player Kit

- [HUMAN] Flashlight (`F`): toggles on/off, drains Energy Cell, auto-recharges, click sound.
- [HUMAN] Crouch (hold `Left Ctrl`): camera drops to 1.1 m height. Can move under low cover. Release to stand — blocked if ceiling overhead. Enemy detection radius halves while crouching.
- [HUMAN] Directional damage indicator: arc appears in HUD pointing toward attacker on each hit, then fades.
- [HUMAN] Settings (`Escape` → Settings): sensitivity slider (0.03–0.4), FOV (60–100°), FPS counter toggle. Values persist after close/reopen.

> [!WARNING]
> Bicycle controls avoid the flashlight binding: `P` opens panniers and `Space` dismounts.

---

### Radiation

- [HUMAN] Enter a radiation zone (craters, city ruins): Geiger counter clicks accelerate (1.5–22 clicks/s). Rad accumulation shown in HUD.
- [HUMAN] Radiation caps max health over time. Rad-Away reduces accumulated dose. Gas mask + hazmat clothing reduce intake rate.
- [HUMAN] Confirm zones are seeded and consistent across sessions.

---

### Enemies & Bestiary

- [HUMAN] Exit the 200 m safe ring: enemies spawn at a sensible rate. Each of the 12 species looks and behaves distinctly.
  - 6 humanoids: Wanderer, Runner, Scavenger, Screamer, Plated, Breacher.
  - 3 creatures: Rootwalker, Mirelurker, Iron Boar.
  - 3 landmark bosses: Gatekeeper (factory), Root Colossus (farm), Shift Commander (military outpost).
- [HUMAN] FBX animations: Idle, Walk, Attack, Hit, Death. No procedural limb swinging.
- [HUMAN] Attack telegraphs: humanoids ~0.5 s, bosses ~0.65 s. Step sideways to avoid; there must be a window to counter.
- [HUMAN] Screamer: interrupt inhale to prevent group call. Let it finish → nearby enemies are alerted.
- [HUMAN] Plated: front takes far less damage than sides/back.
- [HUMAN] Mirelurker: at mid-range, watch for the inhale before an acid bolt; dodge during the windup or sidestep the visible bolt in flight. Walls and terrain must block it; a hit should apply damage once.
- [HUMAN] Scavenger: at range, watch its flask wind-up and sidestep the visible projectile; close in to force it back to the melee jab. Solid walls/terrain block the flask, which applies damage once.
- [HUMAN] Landmark boss defeated → sealed storeroom unlocks.
- [HUMAN] Raider camps: locked chests keyed to camp boss. Kill boss, open chest.
- [HUMAN] Save/load mid-fight and after kill: no duplicate loot bag, no revived boss.

---

### Raids & Defense

- [HUMAN] First raid ~15 min after bombs; every 10 min then 6 min; every 5th wave spawns a Warlord.
- [HUMAN] Warning siren before each raid. Raiders approach from outside, break through doors.
- [HUMAN] Blast door: break-in takes ~1 min. Repair with `V` + Metal Repair Kit.
- [HUMAN] Defenses: sandbags, barricades, spike traps, shock traps, MG turrets (ammo-fed), laser turrets (power-fed). Turrets must not shoot the player.
- [HUMAN] Raiders drop loot bags on death.
- [HUMAN] Save and load during an active raid; raid state must restore.

---

### Scrap Bicycle

- [HUMAN] Craft at Workbench (2 steel bars, 4 scrap metal, 2 duct tape, 1 rope) and place as a build piece.
- [HUMAN] `E` to mount → 10.5 m/s walk / 13 m/s sprint speed. Silent (no vehicle noise, no enemy alerts).
- [HUMAN] `P` opens persistent 35 kg panniers while riding; `Space` dismounts. Supplies stored on the bicycle can support crafting within the nearby-storage radius.
- [HUMAN] Crouch and jump must be blocked while riding. Bicycle position and pannier contents persist after save/load; transferring beyond 35 kg is prevented.
- [HUMAN] The bicycle stays visible during mounting. The camera eases to the `Seat`, then `Space` eases back to a clear exit without moving the parked bike. Check the pose still persists after save/load.
- [HUMAN] Dismount shares `Space` with jump. `FirstPersonController` suppresses jump for 0.15 s after dismount; verify a dismount does not launch the player and jump works normally shortly afterward.

### Scout Buggy

- [HUMAN] Craft at Workbench (10 steel bars, 8 metal plates, 1 electric motor, 4 duct tape) and place the build piece.
- [HUMAN] `E` mounts; walk/sprint speeds are 26/34 m/s (Scout Buggy) and 20/27 m/s (Armored Van). `H` adds 4 km from a fuel can or `J` adds 1.5 km from an energy cell (tank capacity 12 km; the HUD shows the remaining range in km); empty range stops the vehicle. Fuel/cells are only consumed when there is room for their full range. `L` toggles headlights, `P` opens the trunk, and `Space` dismounts.
- [HUMAN] The buggy remains visible through the mount transition. The camera eases to its `Seat`; dismount eases to a capsule-clear exit and leaves the buggy parked at the same transform.
- [HUMAN] Trunk holds up to 120 kg and participates in nearby-storage crafting. Fuel, headlight state, trunk contents and vehicle placement persist after save/load.
- [HUMAN] Driving alerts nearby wildlife and raiders. Check mounted movement, controls, low-fuel and empty-tank warnings, refueling at a nearly full tank, storage transfer capacity, and save/load. Collision sweeps should stop on blocking geometry; fast contacts should impart a capped impulse to dynamic props without repeated damage while blocked.
- [HUMAN] At 5.5 m/s forward speed or faster, buggy bumper sweep damages enemies, animals and placed structures once per target every 1.2 s; reverse, sideways and stationary motion must not ram.

### Player Building Damage

- [HUMAN] With a hammer, axe, pickaxe or equipped melee weapon, hit a placed build piece that has `StructureHealth`. HP should decrease and the existing damaged/critical model stage should update; destroying the piece should remove it once and spawn one loot bag.
- [HUMAN] Compare tool effectiveness: axes are strongest against wood, pickaxes against stone/metal, and weapons are weaker than tools. Bare hands should not damage structures. Damage and dropped bag contents must survive save/load; looting the bag must not duplicate its materials. Unplaced world props are outside this first pass.
- [HUMAN] Firearms should chip placed wooden structures more than stone/metal (18% / 8% / 4% of normal weapon damage); procedural ruins and scenery must remain unaffected.
- [HUMAN] Destroy a piece and confirm one salvage bag only. Save/load with the bag unopened, then loot it; its contents must not reroll or duplicate.

### Structural Tiers & Armored Van

- [AUTO] `--m28-probe`: both motor vehicles stop before dynamic obstacles and impart an impact impulse. This does not cover player input, world bounds, ram damage balance or garage behavior.

- [HUMAN] Reinforced and concrete foundations accept walls and satisfy the same support rules as wood foundations. Reinforced/concrete walls count as full walls; repair and damage stages work.
- [HUMAN] Craft the Armored Van (24 steel bars, 18 metal plates, 4 electric motors, 6 reinforced frames, 2 energy cells, 8 duct tape); cargo capacity is 300 kg, `H` adds 4 km from fuel, `J` adds 1.5 km from an energy cell, headlights toggle with `L`, cargo opens with `P`, `T` fires the turret, and `Space` dismounts. Neither fuel source should be consumed when there is insufficient tank room for its full range.
- [HUMAN] The van stays visible while mounting; the camera eases to `Seat`. `Space` finds a capsule-clear `Exit_L`/`Exit_R` and leaves the van parked. Its position survives save/load.
- [HUMAN] Collision sweeps should stop the van against blocking geometry, keep the rider seated, and apply a throttled impulse to dynamic props without adding duplicate HP damage. Turning in tight spaces should remain responsive.
- [HUMAN] Forward ram at speed damages enemies, wildlife and placed structures once per target cooldown; reversing/standing contact must not repeatedly damage targets. Turret uses one `ammo_rifle` per shot, aims from the camera to 70 m, fires every 0.75 s and is blocked by the first solid obstacle.
- [HUMAN] Destroy a lower foundation under a multi-piece structure; unsupported pieces collapse bottom-up, while pieces with another valid support remain. Confirm each destroyed piece follows the normal single salvage/drop path.

### Garage Workshop

- [HUMAN] Place the 6×6 m Garage Workshop and connect a source. Its panel assembles a buggy or van from the stored `PieceCosts`; inputs come from pack plus stores within 8 m. Assembly takes 45 powered seconds and spawns one vehicle only at an unblocked adjacent spot.
- [HUMAN] Repair toggle consumes one Metal Repair Kit from stores within 8 m for up to 150 HP, repairing the damaged buggy/van within 12 m at 4 HP/s only when the grid grants 2 kW. Outages pause; save/load preserves assembly progress and repair credit. Dismantling with work/credit outstanding is refused.

---

### Furnished Buildings, Stairs & Interiors

- [AUTO] `--building-probe`: generated house/block layouts, loot slots, room openings and ruined/normal layout invariants. `--raider-stair-probe`: raider capsule reaches the target floor in a house and a multi-storey block. `--interior-audio-probe`: room tone inside, drawer/cabinet sounds, an opened door stays on its hinge position. `--interior-shots <folder>` (with graphics) writes PNGs for a look.
- [HUMAN] Walk into a house and an apartment block: furniture should look like furniture (sofa, bookcase, fridge, kitchen run), not boxes; stairs should be walkable, the handrail not block the climb; upper floors dark but playable with the flashlight (`F`).
- [HUMAN] Search a wardrobe, dresser, kitchen run, fridge: drawers should slide out, doors swing on their hinges (not slide to the middle), each with a matching sound; searched state survives save/load.
- [HUMAN] Apartment roof: reach it via the stair hut; AC unit and water tank stand there; about 40% of flats are sealed by rubble after the bombs.
- [HUMAN] Raiders: verify in play that they follow you up house and apartment-block stairs and still attack at the upper floors. The automated stair probe has reached the target floor in both building types.

### Vehicle Sound, Slopes & Horn

- [AUTO] `--vehicle-probe` exercises placement, mounting, driving, dismounting, audio and generated slopes. Claude traced the prior Armored Van clearance failure to the spinning wheel's rotated renderer AABB (the tyre stays at ground height); the probe now measures body clearance separately and checks wheel bounds at rest. Rerun it after the vehicle mount/collision updates.
- [HUMAN] Buggy/van: starter and catch on mounting, idle loop pitching up with speed, stop sound on dismount. Bicycle: soft chain/ratchet only while moving. No footsteps while riding.
- [AUTO/HUMAN] Vehicle horn is bound to K on the Buggy and Van, plays the prepared clip, and alerts nearby threats; verify playback and alert range in a runtime build. Drive up and down a hill: body tilts with the ground (24° cap), speed drops uphill.
- [HUMAN] Van from the driver's seat: wide windscreen, dashboard, steering wheel; the buggy shows its cage, dashboard and wheel.

### Held Tools & Melee Weapons

- [AUTO] `--held-tool-probe`: with an iron axe, iron pickaxe and hammer in the pack and nothing equipped, aiming at a tree shows the axe, at a rock the pickaxe, at the sky the plain bat.
- [HUMAN] Each melee weapon (makeshift blade, spiked club, machete, fire axe, steel sword) has its own model held upright; the swing has an anticipation, a fast strike, a short hit pause and a return.
- [HUMAN] Doors ease open and closed over about a third of a second but collide (or not) immediately.

### Bunker Fittings

- [HUMAN] Supply Cage is a wire-mesh cage; the delivery pallet stands by the stair head from signing the contract until the bunker is sealed; the vault door has a frame and a leaf that swings; the Workshop wing has tool walls, the Hydroponics wing tanks and glowing grow lights; a purchased reinforced door shows a plated leaf.

### Radio Beacon & Rescue Finale

- [HUMAN] Craft and place Radio Beacon. It draws 3 kW from the power grid.
- [HUMAN] Activate → 5-min countdown + 3-wave assault begins. Timer pauses if power is lost.
- [HUMAN] Survive all 3 waves → rescue triggered. Stats ledger shown (days survived, raids beaten, kills, structures intact).
- [HUMAN] Select "Continue" → endless mode resumes normally. Save/load after victory must keep the win recorded.

---

### Atlas Map

- [HUMAN] `M` opens the procedural atlas. Shows terrain, biome borders, roads, town districts, discovered landmarks, and home direction indicator.

---

### Latest Pass (2026-11-10): Menu, Pacing, Raids, Landmarks — HUMAN PLAYTEST FIRST
Nothing here has been played by a person yet; probes only prove logic. Play ~45 minutes from `Builds/Linux/SheltreSim.x86_64` and report what feels wrong.
- [HUMAN] Start screen offers New Game (Easy/Normal/Hard) and Continue (manual save or rotating autosave); Esc pause menu can return to it. Display settings panel changes resolution/fullscreen.
- [HUMAN] Opening: pick a Homesteader/Fortress/Roamer offer highlight in the bunker terminal, sign the $20k contract; five pre-bomb steps and five post-bomb steps guide you (drink, craft, place a piece, enter a town, save). The paper map is in the starter pack and gates the world map. The radio deadline starts at 600 s after signing.
- [HUMAN] Survival pacing: hunger/thirst drain is slower than before; farms need power/daylight; the bed has a cooldown. Does a day feel like pressure or like a chore?
- [HUMAN] Raids: first raid is at 9 minutes with four attackers; attack side varies, a breach warning appears, raiders can target surface assets, 25% of destroyed pieces are salvaged. Do raids threaten a prepared base?
- [HUMAN] After rescue: hordes recur every 15 game-minutes with a 90 s warning, a Warlord every fifth; the Expedition Workshop trader appears after all three upgrades.
- [HUMAN] Loot and wildlife get richer with distance from home; habitat groups mix species from ring 1 outward.
- [HUMAN] Inventory (I/Tab): icon grid with tabs (All/Weapons/Armor/Tools/Medicine/Food/Materials/Build), click a tile to inspect it (big icon, description, effect, quick-slot key), per-tile Eat/Use/Equip button; every item has an icon (`Assets/Art/Resources/Icons`, sources `ArtSource/icons`). `--inventory-shots <folder>` captures it.
- [HUMAN] Crafting window: search box, pin/unpin, pinned-only and can-craft filters.
- [HUMAN] Landmarks (hospital, ironworks, fuel station, farm, military outpost, bunker): facade on the front wall, signage, props, clear entrance and ramp, caches reachable, boss arenas lock and wave in phases. Look for anything floating, buried or blocking the entrance.
- [HUMAN] Car controls (Scout Buggy, Armored Van): mouse only looks around (limited, returns to centre when idle), W/S drive along the vehicle, A/D steer while moving (reversed when reversing), the car never slides sideways. The buggy has no windscreen pane any more; check the driver's view is clear.
- [HUMAN] New Game from the start menu starts a fresh run directly (no return to the menu); melee swings go forward through the target, not back at you.
- [HUMAN] Vehicles: fuel can adds 4 km, tank 12 km; a 3 km drive along a road shows no pop-in holes and no hitch you notice.
- [AUTO] 32 silent probes pass on the committed tree (see `DEV_COORDINATION.md`); `--landmark-shots <folder>` writes landmark screenshots.

## Save System Notes

| Version | Status |
|---|---|
| v7 | Current (bunker shop, autosave) |
| v6 | Supported (migrated) |
| v5 | Supported |
| v0–v3 | Rejected — incompatible world format |

SaveManager captures and restores searched state for non-procedural loot containers and charge/depletion state for
non-procedural resource nodes using stable transform IDs. The save coverage probe also verifies a searched furniture
container's inventory and open pose. Generated chunks use a separate deterministic snapshot keyed by chunk coordinate
and relative hierarchy path: it records changed resource-node charges/regrowth, searched generated containers, and
encounter health/defeat, then reapplies that state when the chunk is rebuilt. A dedicated end-to-end F5/F9 probe for
those generated objects is still missing; current `--save-coverage-probe` covers non-procedural furniture and placed
objects plus chunk unload persistence.

---

## Build & Release

```sh
# Compile check only (no Unity):
./tools/compile_check.sh

# Full rebuild + verify + Linux binary (batch, closes Editor first):
/home/dvojkaa/Unity/Hub/Editor/6000.6.8f1/Editor/Unity \
  -batchmode -nographics \
  -projectPath /home/dvojkaa/git/ShelterSim \
  -executeMethod SheltreSim.EditorTools.ReleaseVerification.RebuildVerifyAndBuildLinux \
  -quit
```

Art rebuild: `ArtSource/build.sh ArtSource/assets/<id>.py` → re-run setup chain.
Blender: `/mnt/Apps/blender-5.2.3-linux-x64/blender`
