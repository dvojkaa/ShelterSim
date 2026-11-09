# SheltreSim — Working Design Notes & Architecture

Persistent design, architecture decisions, and milestone history for developers and AI agents. Keep updated and compact.

---

## Elevator Pitch & Core Loop

First-person bunker-builder + survival game set in a seeded, streamed wasteland.
1. **Pre-Apocalypse:** Start on a suburban lot with **$25,000**. Contract 4 bunker construction stages via the site laptop, buy initial supplies. No enemies, hazards, or loot exist yet.
2. **Apocalypse:** Stage 4 triggers 45s sirens. Bombs strike: surface burns, flash/quake damage above ground, and the world switches to its Ruined state.
3. **Survive & Scavenge:** 20-minute day/night cycle, weather (clear, fog, rain, snow), hunger/thirst/health, radiation hot spots. Scavenge ruins, forage tall grass/reeds for plant fiber, collect water from rivers/lakes, hunt deer and boars.
4. **Base & Power:** Build 20+ structural pieces, automate wireless power grids (25m range; solar/generators/batteries/cooling), farm plots, and crafting stations.
5. **Raid Defense:** Periodic attacks on the bunker with alarms. Defend using barricades, traps, and automated MG/Laser turrets against 12 enemy species and bosses.
6. **Endgame:** Power the Radio Beacon (3 kW), survive a 5-minute 3-wave assault, view the survival ledger, and continue in endless mode.

---

## Confirmed Architecture & Development Rules

* **Engine:** Unity 6000.6.8f1, Built-in Render Pipeline, Linux target. Single-scene architecture (`M1_BunkerShell.unity`).
* **Input:** New Input System; all `InputActionMap` instances are hand-coded in C# (no `.inputactions` assets).
* **Movement & Physics:** `CharacterController` driven with gravity accumulation; `minMoveDistance = 0f` to prevent high-FPS freezes. Doorways must be ≥1.8m wide to accommodate 1.16m enemy collision capsules.
* **Seeding & Determinism:** Always use seeded `System.Random(seed)` with explicit constants; never `UnityEngine.Random` or runtime-chaotic RNG.
* **Audio Engine:** 100% procedurally synthesized in Python (`ArtSource/audio/gen_audio.py`) using NumPy; no external licensed assets.
* **Asset Pipeline:** Low-poly models built via Blender Python scripts (`ArtSource/assets/`) using a shared palette atlas (`SS_Atlas.png`).
* **Verification Rules:**
  - Fast script compilation: `./tools/compile_check.sh` (2 seconds via Roslyn).
  - Headless batch verification: Use standalone diagnostic probes (e.g. `--smoke-test`, `--crafting-probe`). Never force `EditorApplication.isPlaying = true` under `-batchmode -nographics` (hangs indefinitely).
  - Git identity in sandbox: `git -c user.name="vojta" -c user.email="vojta@localhost" commit`.

---

## Milestone Summary (M1–M19 Foundation)

* **M1–M7 (Core Loop MVP):** Hand-built starter bunker, grid-snapped building (2m), hunger/thirst/health ticking, JSON save/load, outdoor scavenging, basic crafting, raider pressure, apocalypse trigger button.
* **M8–M12 (Combat & Map Expansion):** Modal UI (`PauseMenu`, `InventoryUI`), melee combat (`MeleeAttacker`), `EnemyAI` converted to `CharacterController` with door-breaking (`ForceOpen`), doorway bottleneck widened (1.2m → 1.8m), 24,000m² expanded wasteland with 12 ruined buildings and 15 cars, deterministic loot rolling.
* **M13–M17 (Polish & Art Pass):** `GameOverScreen` scene reload, world state persistence across loads, raider respawns and damage flash, functional bed (sleep heals at vital cost), M16 Blender low-poly asset replacement (510 scene colliders preserved), M17 uncapped FPS movement fix (`minMoveDistance = 0`).
* **M18–M19 (Streamed Wasteland & Expeditions):** Infinite streamed 128m chunks, 6 biomes, road grids and bridges, shallow walkable waterways, 6 specialized landmarks (hospital, factory, fuel station, farm, military outpost, abandoned bunker) with access ramps and unique loot tables, bunker expedition console.
* **M20 (Audio Engine):** Procedural audio generator (`gen_audio.py`): 161+ WAV clips, surface footsteps, raider vocals, combat, ambient loops, day/night weather audio, volume sliders in pause menu.

---

## Modern Milestone Evolution (M20–M26)

### M20–M21: Opening Economy, Real Town & Deep Crafting
* **Opening (`HomeBase`, `SiteTerminal`):** Replaced legacy starter area. Player spawns at suburban town edge with $25,000 to purchase supplies and contract 4 bunker stages ($20,000 total). Completing stage 4 triggers 45s sirens, bomb flash/quake, and converts nearest chunks to Ruined variants (wrecked cars, rubble, nodes, patrols).
* **Crafting Expansion (`ItemCatalog`, `RecipeBook`):** Expanded to 96 items and 71 recipes across 6 stations (Workbench, Stove, Forge, Chem Lab, Electronics Bench, Fabricator). Tools gather faster, armor grants flat damage reduction (`ItemCatalog.MaxDamageReduction`), hunting deer/boars for raw meat and leather, farm plots for crops.

### M22: Power Grid, Firearms, Homesteading & Raids
* **Power & Automation (`PowerGrid`):** Proximity-based wireless electrical network (25m range). Solar panels (weather-dependent), fuel generators, battery banks, cooling units (prevents heat shutdown). Automated devices: water pump, irrigator, cell charger, fabricator, repair station.
* **Firearms & Energy Weapons (`RangedWeaponController`, `RangedCombat`):** 8 weapons (pistol, SMG, shotgun, hunting rifle, assault rifle, laser pistol, laser rifle, plasma rifle) with magazine reloads (`R`), ammo management (9mm, rifle, shell, energy cells), and heat dissipation.
* **Homesteading (`HomesteadCatalog`, `StructureHealth`):** 20+ pieces (foundations, half-walls, window walls, steel walls, wooden/steel doors, roofs, ramps, stairs, palisades). Pieces take damage and can be repaired (`V`) or dismantled for a 50% refund (`X` with hammer, 100% within 30s).
* **Raids & Defense (`RaidDirector`):** Periodic base assaults after 15m countdown (every 10m then 6m; every 5th wave spawns a Warlord). Warning sirens, sandbags, barricades, spike/shock traps, automated MG turrets (ammo-fed) and laser turrets (power-fed). Raiders drop loot bags on death.

### M23: Bestiary, Camps & Landmark Bosses
* **12 Species (`SpeciesSpec`):** 6 humanoids (Wanderer, Runner, Scavenger, Screamer, Plated, Breacher), 3 creatures (Rootwalker, Mirelurker, Iron Boar), 3 landmark bosses (Gatekeeper at factory, Root Colossus at farm, Shift Commander at military outpost).
* **Camps & Lairs:** Raider camps feature locked chests keyed to camp bosses. Landmark boss lairs contain sealed storerooms that unlock upon boss defeat.
* **FBX Animations (`EnemyAnimationDriver`):** 12 models imported with 5 Generic takes (`Idle`, `Walk`, `Attack`, `Hit`, `Death`). Attack damage synchronizes to take impact frames (~0.5s for humanoids/creatures, ~0.65s for bosses) to ensure readable, fair telegraphs.

### M24: Player & Base Kit, Radiation, Beacon Finale
* **Player Kit:** Rechargeable flashlight (`F`), crouch stealth stance (hold `LeftCtrl`, 1.1m height, 50% enemy detection radius, ceiling raycast check), directional damage indicator, consumable quick-slots (`3`, `4`, `5`).
* **Settings:** Pause menu settings panel for mouse look sensitivity (`0.03–0.4`), camera FOV (`60–100°`), and toggleable FPS counter.
* **Radiation (`RadiationSystem`, `RadiationZones`):** Seeded rad pockets around craters/ruins. Ticking rad accumulation caps max health and triggers procedural Geiger clicks; mitigated by gas mask, hazmat coats, and Rad-Away.
* **Base Usability:** Powered ceiling and floodlights (`LightPiece`), structural damage visual stages (`StructureDamageVisual`: `<65%` damaged, `<30%` critical), storage quick-stacking into open/nearby crates (`StorageTransfer`).
* **Rescue Finale (`RadioBeaconPiece`, `VictoryLedger`):** Activating the bunker beacon draws 3 kW and starts a 5-minute, 3-wave assault. Surviving triggers rescue, opens the stats ledger (days survived, raids beaten, enemies killed, structures intact), and allows endless continuation.

### M25–M26: Shared Storage, Foraging, Bicycle & Backpack
* **Base-Wide Crafting (`CraftingSystem.NearbyCrateRadius = 100f`):** Crafting stations and build-mode piece placement draw materials from player inventory first, then from any base crate within 100m. UI displays total available resources (`in pack + in storage`).
* **Water & Foraging:** Fill empty bottles with dirty water directly at rivers/lakes (`WaterSource`). Dry tall grass (`node_tall_grass`) and river reeds (`node_reeds`) provide plant fiber (1-2 fiber per hand harvest; instantly cleared by blade/axe swings with wild seed drops).
* **Scrap Bicycle (`RideableBicycle`):** Craftable build piece (2 steel bars, 4 scrap metal, 2 duct tape, 1 rope). Mount with `E` for 10.5 m/s walk and 13 m/s sprint speeds; silent propulsion avoids alerting enemies. While mounted, `P` opens persistent 35 kg panniers (usable for nearby crafting) and `Space` dismounts. Pannier transfers use weight-aware capacity checks.
* **Backpack Frames (`Encumbrance`):** Craftable at Workbench (tiers 1–3). Each frame permanently increases carry capacity by +20 kg (up to 120 kg maximum).

### M27–M28: Projectiles, Vehicles & Structural Progression
* **Telegraphed ranged enemies:** Mirelurker telegraphs with a 1.1 s inhale before launching acid (14 m, 16 m/s, 65% damage). Scavenger throws a flask after a 0.85 s wind-up (11 m, 19 m/s, 55% damage) and uses its jab up close. World colliders block projectiles.
* **Scout Buggy:** Craftable mid-game vehicle (10 steel bars, 8 metal plates, 1 electric motor, 4 duct tape). Mount with `E`; while mounted, `WASD` movement uses 18 m/s walk and 22 m/s sprint, `H` adds 700 m from a fuel can, `J` adds 500 m from an energy cell, `L` toggles headlights, `P` opens the persistent 120 kg trunk, and `Space` dismounts. Fuel can/cell is not consumed unless the tank has room for its full range. Low fuel and an empty tank produce one-time warnings; engine movement alerts nearby enemies. Forward movement above 5.5 m/s sweeps a 0.72 m bumper capsule and deals speed-scaled damage (1.2 s per-target cooldown). Range, lights and trunk contents persist with the placed piece.
* **Armored Van:** Craftable late-game, 300 kg cargo vehicle. `E` mounts; `H` adds 700 m from fuel, `J` adds 500 m from an energy cell, `L` headlights, `P` cargo and `Space` dismount. Forward ramming at speed damages enemies, wildlife and placed structures with per-target cooldowns. Range, lights and cargo persist.
* **Van turret:** While driving, `T` fires one `ammo_rifle` from cargo every 0.75 s. Camera-forward hitscan reaches 70 m, deals 45 damage, and stops at the first solid obstruction; placed structure damage follows the firearm material multipliers.
* **Garage Workshop:** Powered (2 kW) service station. Its panel toggles repair and starts Scout Buggy/Armored Van assembly. Assembly consumes local pack/storage materials, takes 45 powered seconds and spawns one registered vehicle at a clear adjacent spot; progress persists. Repair uses one nearby Metal Repair Kit for up to 150 HP at 4 HP/s for a damaged vehicle within 12 m. Unused repair credit/target persist through outages and save/load.
* **Structural tiers:** Added brick, reinforced and concrete 2 m foundations and 3 m walls with increasing HP, recipes and explicit structural placement/support rules. Wall damage uses current steel-wall damage models until dedicated art is available.
* **Player structure damage:** Melee hits can damage placed pieces with `StructureHealth`. Hammer/axes favor wood, pickaxes favor stone and metal, and equipped melee weapons are less effective than proper tools. Firearms can damage them at reduced rates (18% wood, 8% stone, 4% metal); procedural scenery stays indestructible. Destruction drops a persistent loot bag with up to 50% of the piece's listed recipe inputs; health is already part of piece persistence. Advanced support-collapse behavior remains open.
* **Current vehicle scope:** Scout Buggy remains controller-driven with gameplay ramming but no physical body collision/impulse. Armored Van also rams and has a cargo-fed turret. Fuel cans and energy cells both add range. Garage assembly and repair are implemented.

---

## Active Backlog & Future Proposals

1. **Motorized Vehicles & Garage Workshop (partial):**
   - Implemented Scout Buggy ramming, ramming/turret-equipped Armored Van, fuel/energy-cell range, persistent cargo and powered Garage Workshop assembly/repair. Remaining: physical vehicle collision/impulse and Play Mode balance.
2. **Bestiary Projectile Presentation & Balance:**
   - Mirelurker acid and Scavenger flask attacks are telegraphed and blocked by world geometry. Claude owns distinct projectile visuals; playtest dodge windows and balance at typical encounter distances.
3. **Vehicle and structural Play Mode verification:**
   - Verify same-frame Space dismount, fuel/energy cells, van ram/turret cooldowns, garage power outages, repairs, brick/reinforced/concrete placement, structural damage stages, salvage persistence, and collapse behavior in the live scene.
4. **Linux Build Refresh:**
   - `Builds/Linux/SheltreSim.x86_64` predates M24/M25 scripts. Run `RebuildVerifyAndBuildLinux` from the Editor (see `SETUP.md`) to produce a build with all milestones baked in.
5. **Higher Structure Tiers (partial):**
   - Brick, reinforced and concrete walls/foundations are implemented with recipes and HP. Dedicated damage art remains open.
