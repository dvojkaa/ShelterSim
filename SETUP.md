# SheltreSim — Setup & Playtest Guide

**Tech:** Unity 6000.6.8f1 · Built-in Render Pipeline · Linux target · `M1_BunkerShell.unity`
**Compile check (2 s, no Unity needed):** `./tools/compile_check.sh` → `COMPILE OK (174 runtime + 29 editor files)`

---

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

# Check for PASS/FAIL summary at end of each log
grep -E "(PASSED|FAILED|ERROR)" /tmp/smoke.log
```

---

## Full Playtest Checklist (M1 → M26)

Work through each section in a fresh standalone build or Editor Play session. Items marked
**[AUTO]** are covered by the headless probes above; items marked **[HUMAN]** require eyes.

---

### Pre-Apocalypse Phase

- [HUMAN] Spawn at the laptop on the suburban lot with **$25,000**. Streets and houses visible; no enemies, loot nodes, or animals present yet.
- [HUMAN] Use the site terminal to buy supplies and contract the 4 bunker stages ($20,000 total). Attempting to overspend must be blocked.
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
- [AUTO]  Save format v5/v6 supported; saves v0–v3 are rejected with a clear error.

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

---

### Radio Beacon & Rescue Finale

- [HUMAN] Craft and place Radio Beacon. It draws 3 kW from the power grid.
- [HUMAN] Activate → 5-min countdown + 3-wave assault begins. Timer pauses if power is lost.
- [HUMAN] Survive all 3 waves → rescue triggered. Stats ledger shown (days survived, raids beaten, kills, structures intact).
- [HUMAN] Select "Continue" → endless mode resumes normally. Save/load after victory must keep the win recorded.

---

### Atlas Map

- [HUMAN] `M` opens the procedural atlas. Shows terrain, biome borders, roads, town districts, discovered landmarks, and home direction indicator.

---

## Save System Notes

| Version | Status |
|---|---|
| v6 | Current (M22+) |
| v5 | Supported |
| v0–v3 | Rejected — incompatible world format |

Loot crate searched-state and resource-node depletion persist within a session but **not** across
save/load — this is a known gap (needs stable per-object IDs).

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
