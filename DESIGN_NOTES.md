# SheltreSim — Working Design Notes & Architecture

Persistent design, architecture decisions, and milestone history for developers and AI agents. Keep updated and compact.

---

## Elevator Pitch & Core Loop

First-person bunker-builder + survival game set in a seeded, streamed wasteland.
1. **Pre-Apocalypse:** Start on a suburban lot with **$70,000**. Sign the mandatory **$20,000 bunker contract**, receive the starter survival pack, then spend the remainder across room fit-outs, construction kits, supplies, defense and vehicles. Stages 1–3 run automatically; seal the bunker to start life support and the 45 s warning. No enemies, hazards, or loot exist yet.
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
* **Player structure damage:** Melee hits can damage placed pieces with `StructureHealth`. Hammer/axes favor wood, pickaxes favor stone and metal, and equipped melee weapons are less effective than proper tools. Firearms can damage them at reduced rates (18% wood, 8% stone, 4% metal); procedural scenery stays indestructible. Destruction drops a persistent loot bag with up to 50% of the piece's listed recipe inputs; health is already part of piece persistence. Destroyed supports now trigger bottom-up collapse propagation with a fresh support check before each removal.
* **Current vehicle scope:** Scout Buggy and Armored Van use controller-driven oriented box sweeps to stop against blocking geometry, prevent blocked movement from causing ram damage, and apply throttled impulses to nearby dynamic rigidbodies. They are not full rigidbody vehicles. Fuel cans and energy cells add range; Garage assembly and repair are implemented.

---

## Active Backlog & Future Proposals

1. **Motorized Vehicles & Garage Workshop (partial):**
   - Implemented Scout Buggy/Armored Van collision sweeps and capped dynamic-body impact impulses, ramming, the van turret, fuel/energy-cell range, persistent cargo and powered Garage Workshop assembly/repair. The built-player M28 probe passes blocking and dynamic-impact checks for both vehicles. Remaining: Play Mode handling, bounds and balance.
2. **Bestiary Projectile Presentation & Balance:**
   - Mirelurker acid and Scavenger flask attacks are telegraphed and blocked by world geometry. Claude owns distinct projectile visuals; playtest dodge windows and balance at typical encounter distances.
3. **Vehicle and structural Play Mode verification:**
   - Verify same-frame Space dismount, vehicle collision/impulses, fuel/energy cells, van ram/turret cooldowns, garage power outages, repairs, brick/reinforced/concrete placement, structural damage stages, salvage persistence and cascading collapse in Play Mode.
4. **Linux Build Refresh:**
   - Refreshed in commit `b8ca8f4`: construction/economy verification, Runtime Smoke, Crafting Probe and Procedural World Probe pass. Rebuild after Claude's remaining art is imported and wired.
5. **Higher Structure Tiers (partial):**
   - Brick, reinforced and concrete walls/foundations are implemented with recipes and HP. Dedicated damage art remains open.

6. **Bunker budget & shop:**
   - Implemented: $70k contract economy, automatic first three stages, seal flow, category catalogue, room fit-outs, save migration, protected Supply Cage deliveries, and matching construction kits with a short dismantle refund. Isolated probes passed for bunker economy/geometry, storage persistence, crafting, M28 vehicle collisions, smoke and procedural world. Remaining: human Play Mode/balance pass, Claude's final prop import/wiring, and rerun the newest HomesteadProbe after Unity Package Manager recovers.

---

## 2026-11-10 Bunker budget & shop design (proposal)

Design only: no code changed. Playtester: *"It would be good to have more money at the start and be able to buy various things in the bunker, but balanced so the player has to make decisions about what they can afford and what they can't. So they buy the bunker as one item, and then something like the room expansion, which was good, something like that, but more."*

### What exists today (extend it, don't duplicate it)
* `Assets/Scripts/Core/HomeBase.cs`: `StartingMoney = 25000`, 4 `Stages` ($5k/$8k/$3k/$4k = $20k, 20/30/15/20 s), each started by hand; finishing stage 4 calls `GameStateManager.TriggerApocalypse()`. `ReservedForBunker` holds back what unfinished stages still cost, so only ~$5k is ever spendable. `TryBuyAnnex` ($3,000, needs stage 2) toggles `annexRoom`/`annexPlug`. `TryBuy(ShopEntry)` puts items straight into the backpack.
* `Assets/Scripts/Core/BunkerTerminalUI.cs`: 1320×880 modal (`ModalUI`, `GameUI`, Esc via a code-built `InputAction`), stages and annex on the left, `ItemCatalog.Shop` scroll list on the right. Opened by `SiteTerminal.cs` (laptop on the lot).
* `Assets/Scripts/Crafting/ItemCatalog.cs` line ~226: 26 `ShopEntry` lines at $10–$400. One of each costs $2,196, so there is no real choice today; the only meaningful decision is the annex.
* `Assets/Editor/M20HomeSetup.cs`: main room x 65–79, z −223.6…−211.6, 3 m high. The annex is 8×8 m behind the east wall gap at z −217.5. The expedition console is on the west wall. The terrain cell x 64–80, z −240…−224 is open for the stairs.
* Save: `SaveData.money/bunkerStages/bunkerBuilding/bunkerBuildElapsed/bunkerAnnex` (`SaveManager.cs` ~230, check ~348, restore ~539).
* `ExpeditionUpgradeStation`/`ExpeditionProgression` run on `expedition_parts` after the bombs, not on money. They stay separate. There is no difficulty system.
* Facts used for pricing: hunger 0.3/s and thirst 0.42/s, so a 20-min day uses ~7 `canned_food` (50) and ~11 `bottled_water` (45). Generator 6 kW, 120 s per `fuel`. Solar 1.5 kW peak, 0 at night. Pump 1 kW, irrigator 0.5 kW. MG turret needs power and `ammo_rifle`. `FarmPlot` is a pure 240 s timer with no weather, so it works indoors. First raid comes 900 s after the bombs.

### Core rules
* **Currency:** dollars, shown as **Savings $**. **Starting budget $70,000** on Normal.
* **Bunker Contract, $20,000, the mandatory first purchase.** Until it is bought, every other row is locked ("Sign the bunker contract first"). It pays all 4 stages at once, which replaces `ReservedForBunker` and removes the overspend soft-lock. Stages 1–3 then run on their own back to back (65 s). Stage 4 ("Life support") is the **SEAL THE BUNKER** button. It is enabled after stage 3 and asks for a two-click confirm. Pressing it closes the shop at once (`CanShop` false), and finishing stage 4 starts the bombs as today. The contract also gives a fixed starter pack so a zero-purchase run can't dead-end: `hammer`, 4 `canned_food`, 4 `bottled_water`, 2 `bandage`.
* **$50,000 to spend after the contract.** One of each of the 29 offers (counting the exclusive wing once) costs **$80,400, 161% of that** (143% of the full $70k). Repeatable bundles push it higher. Every build therefore leaves roughly $30k of wants unbought.
* **Delivery:** nothing goes into the backpack, because weight would break it. Everything lands in the **Supply Cage**, a fixed, indestructible, non-dismantlable storage container on the bunker's west wall. Because of `CraftingSystem.NearbyCrateRadius = 100f`, its contents count for crafting and build placement from anywhere on the lot, even before the stairs are open.
* **Kits:** a station, machine or defence is delivered as a `kit_<pieceId>` item, for example `kit_generator` or `kit_piece_turret_mg`. In build mode a kit pays the whole cost of that one piece instead of its `RecipeBook` inputs. The player chooses where it goes, and it persists like any other placed piece.
* **No refunds, no selling.** Rows show what each purchase does and what it depends on, so the choice is informed rather than undone. Money left at the seal is worthless and appears in `VictoryLedger` as "Unspent savings".

### Room ladder (fixed geometry toggled like the annex; all need stage 2 "Concrete shell")
| Tier | Offer id | Room | Price | Includes / gameplay |
|---|---|---|---|---|
| 0 | `bunker_contract` | Main room 14×12 m | $20,000 | Blast door, stairs, vents, Supply Cage, starter pack |
| 1 | `room_annex` | East annex 8×8 m (existing) | $4,000 | Room + 2 `kit_storage_crate`. Storage and building space |
| 2 | `room_wing_workshop` | North wing 10×8 m, **Workshop fit-out** | $6,000 | Room + `kit_workbench` + `kit_piece_stove` + tool wall. Crafting is underground from day 1 |
| 2 | `room_wing_hydro` | North wing 10×8 m, **Hydroponics fit-out** | $6,000 | Room + 4 `kit_piece_farm` + `kit_piece_irrigator` + 2 `kit_piece_light_ceiling` + 10 `seeds` + 4 `fertilizer`. Food behind the blast door, safe from raids |
| 3 | `room_vault` | Vault 6×6 m off the annex's north wall (needs `room_annex`) | $3,500 | Vault door + `kit_bed` + `kit_storage_crate`. A dead-end last-stand room: raiders have to breach two doors |

**Exclusive:** the two north-wing fit-outs share one footprint, so the player gets one or the other. Proposed geometry is north wing x 66–76, z −211.2…−203.2 with a 1.8 m opening at x ≈ 68, and vault x 80–86, z −213.1…−207.1. Both lie outside the open stair cell.

### Price list (29 offers + contract; "lim" = most you can buy)
**Bunker:** `door_reinforced` Reinforced blast door **$3,000**. The main door's break-in HP/time ×2.5. Needs stage 3.
**Rooms:** see the ladder ($3,500–$6,000).
**Power & water:**
* `kit_generator` + 6 `fuel` **$2,500**: 6 kW, ~12 min of running.
* `fuel_drums` 20 `fuel` **$1,500**, lim 3: ~40 min of generator.
* `solar_array` 2 `kit_piece_solar` + `kit_piece_battery` **$6,500**: 3 kW at noon, 0 at night. The panels must stand on the surface, where raids can reach them.
* `water_collectors` 2 `kit_water_collector` **$800**.
* `kit_piece_pump` electric well **$3,500**: 1 kW of clean `water` into a nearby crate.

**Stations:** `kit_workbench` **$1,200**. `kit_piece_stove` **$1,500** (cooking, boiling water). `kit_piece_forge` **$3,000** (ingots, steel).
**Defence:**
* `sandbags` 6 `kit_piece_sandbag` **$1,200**.
* `spike_traps` 4 `kit_piece_spike_trap` **$1,600**.
* `kit_piece_alarm` **$800**: raid warning at 120 s instead of 60 s.
* `mg_turret` `kit_piece_turret_mg` + 100 `ammo_rifle` **$7,500**, lim 2. Needs power.

**Supplies (repeatable bundles):**
* `pantry` 20 `canned_food` + 20 `bottled_water` **$1,500**, lim 3: ~2.8 days of food, ~1.8 of water.
* `medical` 4 `first_aid`, 8 `bandage`, 4 `antiseptic`, 2 `antibiotics`, 3 `anti_rad` **$2,500**, lim 2.
* `seed_sack` 15 `seeds`, 10 `grain`, 4 `fertilizer` **$800**, lim 2.
* `builder_pallet` 40 `planks`, 80 `nails`, 6 `hinges`, 10 `rope`, 10 `duct_tape` **$2,000**, lim 3.
* `metal_stock` 20 `steel_bar`, 20 `metal_plate`, 4 `reinforced_frame` **$4,000**, lim 2.

**Gear:**
* `tool_set` `iron_axe` + `iron_pickaxe` + `machete` **$1,000**.
* `hazard_kit` `gas_mask` + 3 `anti_rad` + `padded_jacket` **$1,500**.
* `backpack_frame` **$1,200**, lim 2 (+20 kg each).
* `pistol_kit` `pistol` + 60 `ammo_9mm` **$1,800**.
* `shotgun_kit` `shotgun` + 36 `ammo_shell` **$2,500**.

**Vehicles:** `kit_piece_bicycle` **$1,500**. `buggy` `kit_piece_scout_buggy` + 4 `fuel` **$12,000**, the single biggest decision at a quarter of the free budget.
Price range: $800–$12,000 for offers, plus the $20,000 contract. Every other `ItemCatalog.Shop` line goes away; this catalogue replaces it.

### Example builds ($50,000 after the contract)
* **Homesteader ($49,400):** Hydroponics wing, Annex, Solar array, Generator + 1 fuel drum, Electric well, Water collectors, Seed sack, 2 Pantry, Medical, Tool set, Workbench, Stove, Forge, Builder pallet, Pistol, Alarm, Spikes, Sandbags, Hazard kit, Backpack frame, Bicycle. It has food and water on autopilot. It is weak in the first raids (pistol and traps only) and has no vehicle.
* **Fortress ($48,700):** Reinforced door, MG turret, Generator + 2 fuel drums, Sandbags, Spikes, Alarm, Shotgun, Pistol, Vault, Annex, Workshop wing, Medical, 2 Pantry, Metal stock, Water collectors, Tool set. It shrugs off raids 1–5. It has no farm and runs on fuel, so it has to hunt or scavenge for food by day 3–4.
* **Roamer ($49,100):** Scout buggy + 2 fuel drums, 2 Backpack frames, Hazard kit, Tool set, Shotgun, Pistol, 2 Medical, 2 Pantry, Annex, Workbench, Stove, Forge, Water collectors, Alarm, Spikes, Builder pallet, Seed sack, Sandbags. It can reach landmarks (`expedition_parts`) on day 1, but its home defence is thin and it lives on scavenging.
* **Engineer ($48,900):** Annex, Workshop wing, Solar array, Generator + 1 fuel drum, Forge, 2 Metal stock, 2 Builder pallets, Electric well, Pantry, Medical, Tool set, Pistol, Alarm, Water collectors, Hazard kit. It walls and fortifies in hour one. Turrets still need scavenged `electric_motor`/`circuit_board`.
* **Bad, "Trophy garage" ($48,800):** Buggy, MG turret, Annex, Hydroponics wing, Vault, Reinforced door, Forge, Metal stock, Shotgun, Medical, Alarm. With no power source the turret, irrigator and grow lights are dead. With no pantry and no stove there is only the starter pack's 4 water, so thirst bites within the first day. It has rooms and toys and nothing to live on. The UI's dependency hints (below) should show 3 red warnings before the seal.

### UI (extend `BunkerTerminalUI`, same `GameUI` style)
* Header: `Savings $X   Spent $Y   Seal status`.
* Left column, "THE BUNKER": the contract row with live stage progress, the room ladder (the two north-wing fit-outs drawn as one row pair with an "OR" between them, a lock reason on each locked row) and, at the bottom, the large red **SEAL THE BUNKER** (two clicks within 3 s; the second-click label shows "Unspent $X is lost").
* Right column: category tab buttons (Supplies / Gear / Stations / Power & Water / Defence / Vehicles) over the existing `ScrollContent`. Each row shows name, price button, `owned/lim`, a one-line effect and a muted "or craft: …" line generated from `RecipeBook`, which makes the opportunity cost visible.
* **Dependency hints** in `GameUI.Warning` on owned rows: "needs power: you own no generator/solar", "needs fuel", "needs seeds", "needs a stove to cook/boil".
* Input: mouse plus Esc, with the existing code-built `InputAction` and `ModalUI` cursor unlock. Optional `1`–`6` tab keys built the same way. Rows the player can't afford are non-interactable, as today.

### Save/load
Add `bunkerContract` (bool), `bunkerRooms` (int bitmask: annex 1, workshop 2, hydro 4, vault 8, reinforced door 16) and `shopCounts` (parallel `string[]`/`int[]` for `lim`). Validation rejects workshop+hydro together, vault without annex, and counts above `lim`. Kits and bundles persist through the existing inventory and storage saves; the Supply Cage needs a stable id. Migration: old `bunkerAnnex` becomes bit 1. An old save with `bunkerStages > 0 || bunkerBuilding` counts as having the contract, and its money is kept as is. An untouched old save (stage 0, $25,000) is lifted to the new `StartingMoney`.

### Difficulty hooks
Use one static `EconomyTuning` (budget, price multiplier, starter pack on/off). Easy $85k, Normal $70k, Hard $58k (no starter pack, supplies ×1.25). The contract stays $20k on every level. Nothing reads a difficulty setting yet; ship Normal constants only.

### Exploits and breakage to guard against
* **Dismantle refund laundering:** a piece placed from a kit refunds **the kit** within the 30 s window, not 100% of its recipe; after that the normal 50% recipe refund applies. The prices above are set so that 50% salvage of any kit is worth less than the matching stock bundle.
* **Seal before stage 3:** blocked. A load during sealing restores stage 4 in progress, with the shop shut.
* **Supply Cage** must not be dismantlable, destructible or movable, and is excluded from raid loot-bag logic, so purchases can't vanish.
* **Kits placed on the surface before the bombs:** decide whether the flash damages pieces above ground. Recommended: 50% `StructureHealth` damage, which makes "set up solar before or after the bombs" a real choice and stops a free pre-built surface fort.
* **Zero-purchase run:** the starter pack, the bed-less main room and the existing crafting are enough to survive while scavenging. Verify in `HomeProbe`.
* **New rooms must be measured, not reasoned about:** use a `_TempProbe` `OverlapCapsule` sweep for 1.8 m doorway clearance and a check that every new room sits under solid terrain (not the open stair cell). See the doorway lesson in `CLAUDE.md`.

### Art needs (new models only, priority order; reuse `piece_*`, `bunker_shelf`, `bunker_locker`, `bunker_lamp`, `blast_door_frame` for everything else)
1. `prop_supply_cage`: x 2.4, y 1.2, z 0..2.2. Wire-mesh storage cage with a padlocked door and a stencilled "DELIVERIES" plate, where every purchase lands.
2. `prop_blast_door_reinforced`: x 2.3, y 0.35, z 0..2.25. The reinforced variant of `blast_door_leaf` with bolted plates and crossbars. Same hinge side and pivot as the original.
3. `prop_vault_door`: x 2.6, y 0.5, z 0..2.6. Heavy vault door with frame. **1.8 m clear opening**.
4. `prop_grow_light`: x 1.6, y 0.4, z 0..0.5. Hanging grow-light bar (white/violet emissive strip). Origin at the ceiling mount.
5. `prop_hydro_tank`: x 1.0, y 1.0, z 0..1.6. Nutrient tank with pump and pipes for the hydroponics corner.
6. `prop_tool_wall`: x 2.0, y 0.15, z 0..1.6. Pegboard with hanging tools for the workshop wing.
7. `prop_delivery_pallet`: x 1.2, y 1.2, z 0..1.4. Shrink-wrapped pallet of boxes and a drum at the stair hut, visible while orders arrive (removed at the seal).
8. `prop_kit_crate`: x 1.2, y 0.8, z 0..0.7. Stencilled flat-pack crate, stacked in the cage in proportion to undelivered kits (optional).
9. `prop_room_bulkhead`: x 2.4, y 0.5, z 0..2.8. Concrete interior bulkhead frame for room openings, 1.8 m clear (optional; the lintel boxes used today would do).

Map them in `M16ArtPass`. Write them per `ArtSource/ASSET_SPEC.md`, and don't place art in the milestone builders.

### Code work (handoff)
* **New `Assets/Scripts/Core/BunkerShop.cs`:** an `Offer` table (id, category, price, lim, prerequisites: stage/room bits/contract, exclusive group, grants (itemId, qty)[], room bit) and a `DependencyHints(HomeBase)` helper. It replaces `ItemCatalog.Shop`/`ShopEntry` (check `ReleaseVerification.cs` for references).
* **`HomeBase.cs`:** `EconomyTuning` budget; `HasContract`, `TryBuyContract` (auto-runs stages 1–3), `CanSeal`/`TrySeal` (starts stage 4 and makes `CanShop` false), a `Rooms` bitmask replacing `HasAnnex` (keep a compat property), `TryBuyOffer`, and purchase counts. Delete `ReservedForBunker`. `RefreshVisuals` toggles room groups, plugs and the reinforced door leaf from the bitmask.
* **`BunkerTerminalUI.cs`:** the layout above.
* **`ItemCatalog.cs`:** generate `kit_<pieceId>` items, with weight, for every kit referenced in `BunkerShop`.
* **`BuildModeController.cs`:** the `TryAfford`/`TrySpend` paths (~210, 345, 367, 509) accept one matching kit from pack or crates instead of `craftingCost`, and the cost label says "Kit". Mark the placed piece as kit-placed for the refund rule.
* **`M20HomeSetup.cs`:** north-wing shell and its two fit-out groups, the vault off the annex, the Supply Cage container, the delivery pallet, and the reinforced door leaf, wired to new `HomeBase` fields.
* **`SaveData.cs` / `SaveManager.cs`:** the fields, validation and migration above.
* **`VictoryLedger`:** "Unspent savings" line.
* **Bunker door:** find the main blast door's break-in path (`M20HomeSetup.BuildDoor` / `EnemyAI.ForceOpen`) for the ×2.5.
* **`HomeProbe` checks:** locked before the contract; exclusive wing; `lim`; no seal before stage 3; a kit is consumed instead of materials; the kit refund rule; rooms bitmask survives a save round-trip; a zero-purchase run reaches the bombs.
* **Docs:** README/SETUP opening text ($25,000 → $70,000, contract, seal button).

### Open questions / balance risks
* **Farming is already very strong:** one `FarmPlot` out-produces hunger drain, and plots are cheap to craft, so the Hydroponics wing's real value is protection plus the irrigator and lights. Watch whether everyone picks the Workshop wing.
* **Buggy at $12k may dominate,** since mobility brings landmark loot and `expedition_parts` early. Consider gating it behind the Annex, or raising it to $14k after a playtest.
* **The MG turret ($7.5k with ammo) may make the first raids trivial.** Watch it alongside `RaidDirector.FirstRaidSeconds = 900`.
* **Generator vs solar:** solar exposed on the surface against fuel dependency underground. Confirm `PowerGrid`'s 25 m range reaches the bunker from the yard.
* **Should the playtester be able to hold off the seal indefinitely?** No timer is proposed. An optional "eviction notice" deadline could add pressure later.
* **Pieces built before the bombs** (the flash-damage rule above) need the playtester's call.
* **$70k is 2.8× today's budget,** but the real spendable amount grows from ~$5k to $50k against 2–4× pricier offers. Expect a tuning pass on bundle sizes, not on the headline number.

## 2026-11-10 Bunker shop gameplay implementation — code complete, playtest pending

The old $25k reserve-based shop is being replaced by the proposal above. Implemented so far: the Normal $70k opening budget, mandatory $20k contract, fixed starter pack, auto-running first three stages, two-step bunker seal, 30-offer category catalogue, purchase limits and wing exclusivity, save version 7 migration for old annex/stage saves, Supply Cage delivery, `kit_<pieceId>` inventory definitions, kit-first construction, and kit refund during the 30-second placement window. `HomeProbe` has been rewritten for the new economy, and `CraftingProbe` validates the offer catalogue.

Compile checks pass (184 runtime + 29 editor files), and the isolated Unity build from before the latest probe/code-review fixes passed `--home-probe`, `--crafting-probe` (119 checks), `--m28-probe`, `--smoke-test`, and `--world-probe`. The isolated `HomeProbe` measured the bunker openings and verified room selection/exclusivity, Supply Cage delivery/save-load, sealing rules and raid-door health. A skeptical review then found and fixed: a paused terminal could stall the apocalypse; legacy mid-stage/annex saves could fail to resume or resave; raiders could steal from the Supply Cage; shop crafting hints missed build recipes; dependency warnings were clipped and ignored deployed equipment; and the starter bunker had a free bed/workbench despite the design. The probes now exercise modal sealing, actual purchase rollback, migration/resumption, a raider-width capsule, kit refunds and absence of free stations. Compile check passes after these changes. The new probe assertions have not run in a standalone player yet: Unity's scratch Package Manager failed at the `Upm-2` IPC startup. Human Play Mode/balance and Claude's art import/wiring remain open; no claim of full playability from compilation alone.

## 2026-11-10 M28 vehicle collision probe

Added the opt-in `--m28-probe` standalone-player diagnostic. It invokes the production buggy and armored-van collision resolvers against dynamic obstacles in an isolated test area. The probe confirms each vehicle blocks movement before an obstacle and delivers an impulse on the next physics step. The Linux player rebuilt from a clean archive plus this probe passes all M28 checks. This is bounded physics verification; player input/handling, vehicle bounds in the streamed world, garage outages/repairs, turret/ram balance and structural collapse still need Play Mode verification. Claude-owned art and shared Unity files were not modified for this probe.

## 2026-11-10 Art audit: missing models (proposal)

Audit only: no code, assets or Unity changed. Claude makes Blender models and GPT does programming, so this lists what is still drawn as a coloured primitive or with the wrong model, ranked by how often a first-person player sees it and from how far. Inventory: 185 FBX in `Assets/Art/Models/`, 186 non-helper sources in `ArtSource/assets/`. All 64 M22 ids, the 12 enemies/bosses, 8 guns, M27-M28 vans/garage/damage/projectiles and the 9 bunker-shop props exist as FBX.

**How the gaps were found:** `M21CraftingSetup.BuildPrefab` (`Assets/Editor/M21CraftingSetup.cs:195-212`) loads `Assets/Art/Models/<pieceId>.fbx` and otherwise builds a coloured `CreatePrimitive(Cube)`. `M22SystemsSetup.Setup()` rebuilds every `Row` on each `BatchSetupAll.RunAll`. The piece ids from every setup file and `HomesteadCatalog` were compared against the FBX list, and the committed prefabs were checked for the built-in cube mesh (`fileID: 10202`). Every runtime `CreatePrimitive` in `Assets/Scripts` was traced to what the player sees.

**Correction to an earlier note:** the intact brick, reinforced and concrete walls and foundations do **not** tint `piece_wall_steel`. Their prefabs (`Assets/Prefabs/Building/{Brick,Reinforced,Concrete}{Wall,Foundation}Piece.prefab`) are plain coloured cubes. Below 65% HP, `StructureDamageVisual` swaps in the real `piece_wall_<tier>_damaged` model. So a brick wall currently looks like a flat box until it is damaged, and then suddenly looks like brick.

### Ready now: a model with the matching id appears on the next `RunAll`, with no code change
| Batch | Asset id | Code today (fallback) | Bounds x / y / z (m), origin floor centre | Art direction |
|---|---|---|---|---|
| **1. Structural tier walls** (seen at 1-10 m, whole base walls of them) | `piece_wall_brick` | Coloured cube, `M22SystemsSetup.cs:57` → `M21CraftingSetup.cs:208` | 2.0 / 0.2 / 0..3.0 | Running-bond brick over a plaster back, like the intact state of `_m27_wall_damage.py:brick`; reuse that helper so the damaged swaps match. Must tile seamlessly at x = ±1. |
| | `piece_wall_reinforced` | Cube, `:53` | 2.0 / 0.2 / 0..3.0 | Concrete panel with welded steel plates, a bolted cross-brace and an exposed rebar lip at the top. Match `_m27_wall_damage.py`'s reinforced look. |
| | `piece_wall_concrete` | Cube, `:55` | 2.0 / 0.2 / 0..3.0 | Poured slab with form-tie holes, chamfered edges, a grime line at the bottom and a stencil band. It should read as the heaviest tier. |
| **2. Structural tier foundations** (underfoot all the time) | `piece_foundation_brick` | Cube, `:56` | 2.0 / 2.0 / 0..0.45 | Brick plinth sides with a mortar-capped top that has a slight edge lip, and a subtle brick pattern on top. It must tile in x and y. |
| | `piece_foundation_reinforced` | Cube, `:52` | 2.0 / 2.0 / 0..0.5 | Concrete slab with steel edge angles, corner plates and checker-plate top inserts. |
| | `piece_foundation_concrete` | Cube, `:54` | 2.0 / 2.0 / 0..0.6 | Thick poured slab with expansion-joint grooves, chamfer, tie holes on the sides and a stain line. |
| **3. Base lights** (every base at night; only 2 models) | `piece_light_ceiling` | Cube, `:79` | 0.4 / 0.4 / 0..0.2 | **Free placement rule: it sits on a floor or bench, not the ceiling.** Make a squat caged bulkhead lamp on a round base, with a `lamp_warm` dome. `LightPiece` puts the point light at the bounds centre. |
| | `piece_floodlight` | Cube, `:80` | 0.5 / 0.4 / 0..0.5 | Tripod or short-post work floodlight with its head tilted down. **The lens faces Blender +Y** (Unity forward): `LightPiece.cs:64` aims the spot along `transform.forward` from the top front. |

Only these 8 gaps can be fixed without code. Brief the art agents to use the `piece_*` footprint convention (model origin on the footprint's floor; `BuildPrefab` offsets by −height/2) and to check their preview against `piece_wall_steel`/`piece_foundation` for scale.

### Needs code, hand to GPT (ranked; the models could be built in parallel once the hook is agreed)
Each line gives the current fallback, the hook needed, and the proposed model ids/bounds. Hooks should load from `Resources/` via `M22SystemsSetup`/`M16ArtPass` (not the milestone builders) and fall back to today's primitive if the model is missing.

| Batch (after hook) | Asset ids | Current fallback | Hook | Bounds / direction |
|---|---|---|---|---|
| **4. Gathering tools in hand** (on screen ~80% of play) | `melee_hammer`, `melee_iron_axe`, `melee_iron_pickaxe` | Every melee item shows the `player_weapon` bat (`M16ArtPass.cs:689-698`, one fixed `WeaponVisual`, `MeleeAttacker.cs:19`) | Swap `weaponVisual`'s child by the equipped item id, as `RangedViewmodel.Build` does with `Resources/Weapons/<id>` | `player_weapon` convention: grip origin, length +Z, z −0.3..0.6, head x ±0.18 (pickaxe ±0.28). Stone/steel tiers can be palette-swapped variants later. |
| **5. Melee weapons in hand** | `melee_machete`, `melee_fire_axe`, `melee_steel_sword` | Same bat | Same hook | Same convention; sword/machete z −0.2..0.75. |
| **6. Wildlife** (seen at 10-40 m and hunted) | `animal_deer`, `animal_boar`, `animal_crow` | Cube parts, `WorldWildlife.cs:143-191`; carcass is a cube, `:124` | Load `Props/animal_*`, find `Leg_FL/FR/BL/BR` / `Wing_L/R` pivots by name; carcass = the same model rolled onto its side | Front +Y (Unity forward). Deer 0.8 / 1.6 / 0..2.4 (antlers to 2.4); boar 0.8 / 1.6 / 0..1.0; crow 1.1 / 0.6 / ±0.15, origin at the body. |
| **7. Raider camp** (every camp) | `camp_tent`, `camp_fire`, `camp_locked_chest` | Cube/cylinder panels `EnemyCamps.cs:203-224`, sphere stones `:230-241`, chest boxes `:292-296` | Spawn the model as the visual and keep today's colliders | Tent 2.8 / 3.6 / 0..2.1 (long axis Y, collider panels unchanged); fire ring 1.9 / 1.9 / 0..0.5 (light stays in code); chest 1.4 / 0.95 / 0..0.95, padlock at −Y. |
| **8. Landmark interactables** | `lm_relay_cabinet`, `lm_fuel_pump`, `lair_warning_post` | Boxes `ProceduralLandmarks.cs:374-389` (the relay lamp renderer is driven by code), `:277`, `BossLairs.cs:177-181` | Model with a named `Lamp` child the code can colour | Cabinet 1.4 / 0.8 / 0..1.6, panel at −Y, lamp on top; pump 0.9 / 0.7 / 0..1.6; post 0.3 / 0.3 / 0..3.3, red lamp at the top. |

**Other code-gated gaps (not batched):**
* **Bunker shop props:** the 9 `prop_*` models are built (see the shop section above) and nothing references them yet. This is GPT's `M20HomeSetup`/`M16ArtPass` wiring.
* **Landmark buildings** (`ProceduralLandmarks.cs:233-335`: hospital, factory, fuel station, farm, military, bunker) and **boss-lair storerooms** (`BossLairs.cs:128-169`) are all primitives. They would be a large set-piece model pass once a hook exists. `lm_silo`, `lm_smokestack`, `lm_fuel_canopy` and `lair_shutter_store` are the most visible.
* **Ground items:** `PropModels.ModelFor` (`Assets/Scripts/Items/PropModels.cs:35-46`) knows only fuel, the 3 ammo boxes and the energy cell. Landmark cache rewards of food, medicine and water show nothing. The candidates are `item_canned_food`, `item_bottle`, `item_medkit` and `item_scrap_bundle`, plus a `ModelFor` case for each.
* **Damage stages for the other pieces:** `M22SystemsSetup.DamageStagePieces` (`:29-38`) lists walls, doors and perimeter only. Foundations ×4, `piece_wall_half`, `piece_wall_window`, `piece_door_frame` and the roofs have no worn/critical look. Each needs both the model pair and a list entry.
* **Player hands/arms:** neither the guns nor the melee viewmodel has hands. This needs a rig/attach decision first.
* **Camp flag** (`EnemyCamps.cs:254-261`) and the lair glass-house panes are small. Do them later.
* **Ceiling mounting:** `piece_light_ceiling` uses `BuildPlacementRule.Free`, so it can't hang from a roof. If GPT adds a ceiling rule, `bunker_lamp`-style hanging art becomes the right shape. Until then, batch 3's floor/bench lamp is correct.

**Headline:** 8 no-code gaps, all build pieces that are still coloured cubes, and about 40 code-gated gaps: 6 melee viewmodels plus tiers, 3 animals, 4 camp props, 3 landmark interactables plus ~10 landmark set-pieces, 9 unwired shop props, ~4 ground items, 7+ damage-stage pairs and the hands. Do batches 1-3 now. Agree hooks 4-8 with GPT before modelling, so the models aren't orphaned the way the shop props currently are.

## 2026-11-10 Buildings & animation overhaul (proposal)

Design/audit only: no code, assets or Unity changed. Playtester: *"Maybe the animations could be improved, and the house models: the apartment blocks should be whole apartment blocks, not just a house at the bottom, with stairs, more loot, more places where loot can be, living rooms etc. A house should be a house, not just one room with nothing in it."*

### What the player finds today (all in `Assets/Scripts/World/ProceduralContent.cs`)
| Type | Where | What is inside | Loot |
|---|---|---|---|
| Family house | `House` :337, suburb chunks only, ≤6 lots/chunk at 80% (`Suburb` :272-293) | One empty 10 x 12 x 3 m box (`Floor`/`Back`/`West`/`East`/`Ceiling`), 1.8 m front gap, `house_door` stood open against the wall, windows are decals. No interior walls, furniture, stairs, light. | Ruined only: `Crate_00` always + `Crate_01` 50% (:399-400), `loot_crate` scaled x2 (:568), table `house` |
| Town "apartment" | `Building` :518, town chunks, 9 lots, corners guaranteed, middle 65% (:244-249) | 12 x 12 ground room, 3.4 m storey, 2.8 m door. `floors = random.Next(1,6)` (:248) adds a **solid box** `UpperFloors` (:537): storeys 2-5 are one cube with painted windows. No stairs anywhere. | Ruined only: 1 + 50% crates, table `town` (:557) |
| Landmarks | `ProceduralLandmarks.cs` :233-335 | Single-storey `Room` shells; 4 `Cache` boxes (:337) | 4 caches, first one with a guaranteed reward |
| Cars | `Car` :405 | - | trunk crate, table `car` |

* **Loot maths (`LootTables.cs`):** `Fill` draws 2 numbers per row always, caps at 6 stacks, never leaves a container empty (:81). Expected stacks: `house` ≈ 3.9/crate → **≈ 5.8 per house**; `town` ≈ 4.2 → **≈ 6.2 per town building**. Town chunk ≈ 7 buildings ≈ 43 stacks; suburb chunk ≈ 4.8 houses ≈ 28 stacks. Searched state persists by hierarchy path (`ProceduralWorldState.RelativeId` :115: name + same-name ordinal), never respawns.
* **No walkable stairs exist in the streamed world.** The only conventions: `M20HomeSetup.BuildStairs` (:192, one smooth `BoxCollider` ramp + visual-only `bunker_stair_step`s, because real steps catch a `CharacterController`) and `M22HomesteadSetup.Slope` (:146, `piece_stairs` = 2 m wide, 4 m run, 3 m rise, 36.9°). Controllers use Unity defaults (slopeLimit 45°, stepOffset 0.3); player r 0.35 h 1.8 (`M1SceneBuilder.cs:160`), humanoid species r 0.45-0.5 h 1.9-1.95 (`SpeciesSpec.cs:40-58`).
* **Enemies have no navigation** (no NavMesh anywhere; steer straight at the target). They cannot follow a player upstairs.
* **Existing bug found (fix with this work):** `House` :364 `bool burnedOut = ruined && random.NextDouble() < .35;` draws only in the ruined pass, so the shed roll (:390), parked-car roll (:394) and car model shift at the bombs: sheds and cars can pop in or out of house lots. Always draw, then AND with `ruined`.

### Animation audit (what a first-person player sees most, weakest first)
| # | What | Today | Cheap fix (code) | Blender? |
|---|---|---|---|---|
| 1 | **Melee swing** (every gather + fight) | `MeleeAttacker.SwingWeapon` :352-374: linear `Slerp` of one −60° X rotation in 0.1 s, back in 0.15 s. No wind-up, no arc, no follow-through, no impact. Hit is resolved instantly at the click (:126). | Curve-driven 4 phases on the `WeaponVisual` grip: anticipation 0.05 s (+12° X, +8° Y, pull 0.04 m back), strike 0.09 s ease-in (−80° X, −35° Y, −20° roll, 0.12 m forward/left: a diagonal arc), follow-through 0.06 s overshoot, recover 0.2 s ease-out. Weight per weapon (axe/sledge ×1.3 time, knife ×0.8). On hit: 50 ms visual hit-stop (curve pause, never `timeScale`), 1.5° camera kick, existing hit sfx. Move the damage apply to the strike frame (+0.06 s) with a re-check, or keep instant and accept 60 ms lead. | No |
| 2 | **No head-bob / landing / flinch** | `FirstPersonController` has none; `PlayerAudio` already tracks footsteps and `fastestFall` (:121-162). | `CameraPivot` offset: bob 0.035 m vertical / 0.02 m lateral at the footstep phase (sprint ×1.4, crouch ×0.5, off when mounted); landing dip 0.05-0.15 m by fall speed, 0.25 s spring; hit flinch 2-3° roll toward `DamageDirection` source. Settings toggle "Head bob" (motion sickness). | No |
| 3 | **Doors snap** | `InteractableDoor.RestoreState` :152 and `HomesteadDoorPiece.ApplyPose` :55 set the hinge instantly. | Visual-only tween: open 0.35 s ease-out with 4° overshoot, close 0.3 s ease-in, bash (`ForceOpen`) 0.12 s + 10° rebound. Logical state and collider switch at once as today (closing still refuses when `DoorwayOccupied`). `RestoreState` from save snaps (add an `animate` flag). | No |
| 4 | **Containers don't open** | `loot_crate` ×2, nothing moves; searched vs unsearched looks identical. | New furniture carries named parts `Door_L/Door_R/Drawer_n/Lid`; `LootContainer.Interact` tweens them 0.25 s and leaves them open. Restore the open pose on load from `Searched`. Doubles as the "already searched" readability cue. | Part pivots only |
| 5 | Ranged viewmodel | `RangedViewmodel.Tick` :51-57: kick + 28° reload dip only. | Equip raise 0.25 s from below; reload = dip + 20° roll + 0.06 m drop, back with overshoot; sprint low-ready pose; mouse-delta sway lag ≤2°; idle breathing 0.004 m. | No |
| 6 | Enemy walk foot-slide | `EnemyAnimationDriver` :63 sets `Speed`, Walk take (0.8 s) plays at fixed rate whatever the velocity. | Animator state speed multiplier parameter `WalkRate = speed / nominal` (nominal measured from clip stride), clamp 0.6-1.6. Don't reuse `animator.speed`: Attack sync owns it (:71). | No |
| 7 | Wildlife | `WorldWildlife.cs:255-262` sine legs ±24°, cube bodies. | Torso bob 2× leg frequency, head nod; blocked on the animal models (art audit batch 6). | Models first |
| 8 | Bicycle | `RideableBicycle.cs:69-72` glued 0.8 m behind the rider; no wheels/pedals. | Wheel/crank spin by distance, ±4° camera lean in turns, pedal bob. | `piece_bicycle` parts `Wheel_F/Wheel_B/Crank` |
| 9 | UI panels | Modal panels pop. | 0.12 s fade+scale on `ModalUI` open, feed lines fade. Lowest priority. | No |

**Top 3:** melee swing curve + hit-stop, camera bob/landing/flinch, eased doors + opening containers. All procedural, no clips. New Blender *animation clips* are not needed for this pass. The only later clip work worth doing is a first-person hands rig (blocked on the hands decision in the art audit) and an optional squatter `Sit`/`Guard` idle take for enemies inside buildings.

### Building system: shared rules
* **Module grid:** storey **3.0 m** (slab top to slab top; slab 0.2, clear 2.8), interior walls 0.2, exterior 0.3 (as today), doorways **1.8 m clear x 2.2 m** in every wall a raider may cross, corridors **≥1.4 m clear** (raider capsule 1.0 + skin 0.16; 1.2 is the absolute floor), flights **≥1.4 m** wide. Interior door leaves are always drawn open against the wall (like `house_door`), never narrowing the gap.
* **Stairs = the existing convention:** visual step model with **no collider** + one sloped `BoxCollider` ramp per flight (reuse `M22HomesteadSetup.Slope`'s maths in a runtime helper). Flights ≤36°. Ramp top meets the landing flush (≤0.03 m lip); landing ≥1.4 m deep before any wall. Upper slab has a void over the flight and a 1.0 m railing (box collider, 0.1 thick) on every open void edge, so nobody walks or gets pushed into the hole. The space under a flight is filled with a solid wedge collider (no crawl-under traps).
* **Furniture rule:** solid furniture (box collider = model bounds) stands against walls only, ≤0.65 m deep, never within 1.0 m of a doorway axis or a flight's foot/head; corridor clear width stays ≥1.4 after furniture. Small dressing (lamps, plates, rugs) has no collider.
* **Windows:** upper-floor facade windows stay opaque panels (as today) so interiors are invisible from outside, which is what makes interior culling free. Ground floor shop windows may be glass-less openings with a grille collider.
* **Measure, don't reason:** every new type gets a `_TempProbe` pass (`Physics.CapsuleCast` r 0.5 h 1.95 from the front door to every room centre, up and down every flight, roof included) before it ships. See the doorway lesson in `CLAUDE.md`.

### Family house (replaces `House`; footprint unchanged 10 x 12 so lots, hedges, shed and parking stay valid)
Two storeys + pitched roof (existing `house_roof_a/b`, lifted to y 6.1). Local coords, front door at z −6, x right. Schematic (1 char ≈ 0.5 m in x, 1 row ≈ 1 m in z); the table is authoritative.
```
 GROUND FLOOR (y 0)                   UPPER FLOOR (y 3.0)
 +--------+------+------+  z+6        +--------+------+------+
 | KITCHEN-DINER  | PAN- |            | BED 2  |CLOSET| STUDY|
 |   (K)     ]    | TRY  |  z+2.2     |  (B2)  ]--LDG-]  (S) |
 +--------  ......+ (P)  |            |        :  ^  |      |
 | LIVING |  w  |SS|     |            +--------: void|      |
 |  (L)   ]  a  |SS+-----+  z-1       | BED 1  :  |  +------+
 |        |  l  |SS| UTIL|            |  (B1)  ]  |  | BATH |
 |        |  FOYER  ] (U)|  z-2       |        |HALLWAY ] (W) |
 +--------+---[  ]--+----+  z-6       +--------+-------+------+
```
| Room | GF rect x / z (inner faces) | Doors (1.8 m) | Loot slots |
|---|---|---|---|
| Foyer + walkway | x −0.9..2.2 / z −5.7..−2.0, walkway x −0.9..0.8 / z −2..2.2 | front (existing), to L, U, K | - |
| Stair flight | x 0.8..2.2 (1.4 w) / z −2.0..2.0, rises +z 0.1 → 3.0 (35.9°) | - | under-stair wedge, solid |
| Living (L) | x −4.7..−1.1 / z −5.7..2.0 | from foyer, z −4.6..−2.8 | `tv_unit`, `bookcase` |
| Kitchen-diner (K) | x −4.7..2.2 / z 2.4..5.7 | from walkway, x −0.9..0.8 (open) | `kitchen_run`, `fridge` |
| Pantry (P) | x 2.4..4.7 / z −0.9..5.7 | from K, z 3.0..4.8 | `pantry_shelf` |
| Utility/WC (U) | x 2.4..4.7 / z −5.7..−1.1 | from foyer, z −5.6..−3.8 | `toolbox` |
| UF hallway + corridor | x −0.9..2.2 / z −5.7..−2.0; corridor x −0.9..0.8 / z −2..2; landing x −0.9..2.2 / z 2.0..3.8 | railing on void x 0.8 and z −2.0 | - |
| Bed 1 master (B1) | x −4.7..−1.1 / z −5.7..0.4 | from hallway, z −4.0..−2.2 | `wardrobe`, `nightstand` |
| Bed 2 (B2) | x −4.7..−1.1 / z 0.6..5.7 | from landing, z 2.0..3.8 | `dresser` |
| Bath (W) | x 2.4..4.7 / z −5.7..−1.1 | from hallway, z −5.4..−3.6 | `medicine_cabinet` |
| Study (S) | x 2.4..4.7 / z −0.9..5.7 | from landing, z 2.0..3.8 | `desk` |
| Linen closet | x −0.9..2.2 / z 4.0..5.7 | from landing | `linen_closet` |
**12 container slots**, 9 rooms + hall. Variants from the house's interior `SlotRandom`: mirror x (50%), study ↔ kids' bedroom, garage annex (25%, x 5..9 beside the house, replaces the parking spot; 2 more `toolbox` slots). No basement: `ProceduralTerrain` is a heightfield with holes only at the bunker cell. No attic: the roof models are not enterable.
**Ruined-only:** burned-out (35%) removes roof + UF ceiling and adds charred beams; scorch decals, broken windows, 0-2 `ruin_debris` piles placed only on wall-side furniture-free cells.

### Apartment block (replaces `Building` on dense lots)
Two widths, **4-6 storeys** (GF + 3-5 residential), roof with stair hut. Town lots are 33 m between street edges (centres 24/64/104, streets at 3.5/44/84/124.5), so a 20 m block keeps a 6.5 m sidewalk; raise the `Clear` margin from 9 to 11 for blocks.
* **Wide 20 x 14 m** (x −10..10, z −7..7): 4 flats/floor. **Narrow 14 x 14 m**: 3 flats/floor (one large front flat, two studios beside the core).
* **Stair core** back-centre, x −1.6..1.6 / z 0.9..6.85: floor landing z 0.9..2.5, two flights x −1.6..−0.1 and 0.1..1.6 (1.5 w, 0.2 divider wall), z 2.5..4.9 (2.4 run, 1.5 rise, **32°**), mid-landing z 4.9..6.85 at +1.5. Classic switchback: flight A rises +z to the mid-landing, flight B rises −z to the next landing. Flights stack exactly 3.0 m apart, so headroom is 2.8 everywhere. Slab void x −1.6..1.6 / z 2.5..6.85 on every storey, railing along z 2.5 on the flight-A side.
* **Corridor** x −9.85..9.85 / z −0.9..0.9 (1.8 w) on every floor, opening onto the core landing.
```
 TYPICAL FLOOR, WIDE (x −10..10 left→right, z +7 top)
 +--------------+-------+--------------+
 |  FLAT NW     | CORE  |   FLAT NE    |   core: ^ flight A | v flight B, mid-landing at top
 |  8.1 x 5.75  | ^ | v |  8.1 x 5.75  |
 +-----]--------+LANDING+--------[-----+  z +0.9
 |<-------------- CORRIDOR 1.8 ------->|
 +---------[----------------[----------+  z −0.9
 |  FLAT SW 9.75 x 5.75 | FLAT SE      |
 +----------------------+--------------+  z −7 (street)
```
* **Flat (SW, 9.75 x 5.75):** living + kitchenette x −5.0..−0.1 full depth (door from corridor near the core); bedroom x −9.85..−5.2 / z −6.85..−3.4; bathroom x −9.85..−5.2 / z −3.2..−1.1; doors 1.8 in wall x −5.1. Slots: `kitchen_run`, `fridge`, `wardrobe`, then one of `medicine_cabinet` / `nightstand` / `tv_unit` by flat roll (= **4 per flat**). Studios (5 x 5.75): one room + bath alcove, 3 slots.
* **Ground floor:** 2.8 m double entrance (today's convention) → lobby x −1.6..1.6 / z −6.85..−0.9 → corridor. SW = corner kiosk (street door + lobby door; `shop_shelf` x2, `counter`), SE = bike/storage room (`toolbox` x2, `lockers`), NW = mailroom + super's office (`mail_lockers`, `desk`, `safe` 30%), NE = boiler/laundry (`lockers`). ~8 slots.
* **Roof:** core rises into `bld_roof_stairhut` (door 1.8) onto a roof with a 1.0 m parapet (collider, nobody walks off), `roof_water_tank`, `roof_hvac`, and one **roof cache** (`roof` table, 60%).
* **Sealed flats (ruined only):** each flat's own `SlotRandom` rolls 40% "sealed": a rubble plug with a collider fills its door, no containers spawn inside. Keeps counts sane and gives the "locked door" feel. Follow-up option: plugs breakable with a pickaxe via `StructureHealth`.
* **Container count:** wide 5-storey: 16 flats × 0.6 × 4 + 8 GF + 1 roof ≈ **47**; narrow 4-storey ≈ **31**. Average block **≈ 40 containers, ≈ 30 stacks**.

### Two more types worth upgrading
* **Shop-house** (all other town lots; replaces the remaining 12 x 12 `Building`): 12 x 12, 2 storeys. GF shop: street door 2.8, 3 `shop_shelf` gondolas (aisles 1.6), `counter` with register; backroom (`toolbox`, `pantry_shelf`). UF flat via a straight house flight in the back-left corner (1.4 x 4.0, same as the house). **10 slots, ≈ 9 stacks.** 1-in-3 variants: pharmacy (shelves use `pharmacy`), hardware (`hardware`).
* **Office** (centre lot slot 4 at 50%, else shop-house): 16 x 12, 3 storeys, narrow switchback core at back-centre. Per floor: open plan with 4 `desk`s, 2 `filing_cabinet`s; top floor manager's room with `safe`. **≈ 19 slots, ≈ 12 stacks.**

**Lot assignment (Town, :241-251):** keep the lot's first two draws (`occupied`, `floors`) exactly as today so lot occupancy and positions don't move, then choose the type from a **new** `SlotRandom(coordinate, seed, 500 + slot)`: corner lots 0/2/6/8 → block at 45% (wide if `floors >= 4`), **max 2 blocks per town chunk** (slot order decides); slot 4 → office 50%; else shop-house. Suburbs keep houses only. Expect ≈ 1.3 blocks, 0.5 office, 5 shop-houses per town chunk; a town (4 core chunks) ≈ 5 blocks.

### Performance budget (128 m chunks, `loadRadius` 3 = up to 49 live chunks, one `Generate` per frame)
| Type | GameObjects | Colliders | Tris (models) | Draw calls after combine |
|---|---|---|---|---|
| House | ≤ 160 | ≤ 70 | ≤ 14k | ≤ 8 |
| Shop-house | ≤ 180 | ≤ 80 | ≤ 15k | ≤ 8 |
| Office | ≤ 320 | ≤ 140 | ≤ 25k | ≤ 12 |
| Apartment block | ≤ 600 | ≤ 260 | ≤ 45k | ≤ 20 |
* **Combine:** after a building is built, `StaticBatchingUtility.Combine(staticGroup)` (runtime API) on a `Static` child holding walls, slabs and non-moving furniture. Doors, container parts and lights stay outside it.
* **Interior culling** (new `BuildingInterior` component): each storey's interior renderers live under `Floor_n/Interior`. Disabled unless the player is inside the building's bounds (+2 m), then only storeys within ±1 of the player's storey render. GF stays on within 25 m (visible through the door). Colliders always stay on (enemies, projectiles, saves).
* **Chunk unload:** unchanged (`Plan` destroys chunks beyond radius+1 after `Remember`). Blocks add nothing persistent except searched states.
* **Hitch budget:** measure `Generate` for a 2-block town chunk in a probe; target ≤ 12 ms on the dev box. If over, fallback: shells in `Generate`, interiors from a queue one building per frame, each followed by `ProceduralWorldState.Apply` on that subtree (needs an `ApplySubtree`).

### Loot placement
* **Slots, not crates:** each furniture model is the container (`LootContainer` on the furniture root, collider = bounds). The old floating `loot_crate` ×2 stays only for cars, landmarks and generic storage.
* **Determinism:** layout (room variant, which furniture, mirror) draws from `SlotRandom(coordinate, seed, base + lot)` in **both** passes identically. Each container's contents draw from its **own** `SlotRandom(coordinate, seed, 10000 + lot * 128 + slotIndex)`, only in the ruined pass, so adding or removing one slot never changes another. Ruined-only decor (rubble, plugs, sealed roll, scorch) uses its own `SlotRandom(..., 20000 + lot * 128 + k)`. Never `UnityEngine.Random`. Names are unique per building (`C00`..`C63`), so `RelativeId` paths don't depend on ordinals.
* **Normal pass:** the same furniture, no `LootContainer`, no resource nodes. Ruined pass adds the component. Bump `WorldLayout.GenerationVersion` 2 → 3 (old saves get the existing warning; their town crate states won't match, i.e. those crates are fresh: accepted).
* **Furniture may be empty** (new `LootTables.FillSlot(loot, table, random, tier)`: same "2 draws per row always" + one extra draw for the empty roll; no forced non-empty rule). Empty chance listed below.

| Slot table | Rows (chance, qty) | Empty |
|---|---|---|
| `kitchen_run` | canned_food .35 1-2, sugar .25, grain .2, empty_bottle .3, glass .15, duct_tape .1, chemicals .08 | 30% |
| `fridge` | bottled_water .3, dirty_water .3, vegetables .1, canned_food .1 | 50% |
| `pantry_shelf` | canned_food .45 1-3, bottled_water .35 1-2, sugar .2, seeds .15 1-3, herbs .1 | 20% |
| `wardrobe` / `dresser` / `linen_closet` | cloth .6 1-4, leather .12, rope .1, (wardrobe only) ammo_9mm .04 4-8 | 25% |
| `nightstand` | bandage .2, cloth .2, stimulant .04, ammo_9mm .06 3-6, energy_cell .02 | 50% |
| `medicine_cabinet` | bandage .4 1-2, antiseptic .3, first_aid .1, antibiotics .06, anti_rad .04, empty_bottle .2 | 35% |
| `tv_unit` / `desk` / `filing_cabinet` | electronics .25, copper_wire .25 1-3, duct_tape .15, glass .1, circuit_board .03 | 40% |
| `bookcase` | cloth .2, planks .25 1-2, herbs .05 | 55% |
| `toolbox` / `lockers` | nails .5 3-8, scrap_metal .4 1-3, duct_tape .3, hinges .15, copper_wire .15, rope .15, repair_kit_metal .03 | 15% |
| `shop_shelf` / `counter` | canned_food .4 1-3, bottled_water .4 1-3, sugar .25, bandage .15, duct_tape .15 (counter: ammo_9mm .1 4-10) | 25% |
| `pharmacy` / `hardware` | as `hospital` / `toolbox` tables | 20% |
| `mail_lockers` | cloth .3, electronics .1, duct_tape .1 | 60% |
| `safe` | ammo_9mm .4 6-14, first_aid .3, energy_cell .15, expedition_parts .08, gunpowder .2 1-3 | 0% |
| `roof` | fuel .3, ammo_rifle .2 4-10, bottled_water .3 1-2, rope .2, energy_cell .08 | 40% |
* **Tier multiplier:** `tier = storey index` (GF 0) for blocks/offices, +1 in town chunks vs suburbs. Only the "rare" rows (ammo_*, first_aid, antibiotics, anti_rad, energy_cell, expedition_parts, circuit_board) get chance × (1 + 0.12 × tier), capped at ×1.6. Upper floors were looted less by earlier survivors; the roof and top-floor safe are the payoff for climbing.
* **Totals (expected stacks):** house ≈ 12 slots × 0.65 × 1.4 ≈ **11** (today 5.8); block ≈ **30**; shop-house ≈ 9; office ≈ 12. Suburb chunk ≈ 53 (today 28), town chunk ≈ 90 (today 43): **~2× the stacks**, at ~3× the searching time and real risk. Food+water per house stays ≈ 1.7 stacks (today 1.5): the bunker shop already front-loads food, so the extra loot is mostly crafting materials. Rare rows per house stay ≈ today's ×1.3 because they live only in the medicine cabinet, nightstand, safe and roof. Watch `Encumbrance` (100 kg base): a block run fills a pack, which pushes the bicycle/buggy cargo loop.

### Interior lighting and readability
* Built-in RP, no runtime GI: interiors get flat world ambient by day and are black at night. **The flashlight (F, 28 m, 62°) stays the night tool**: no per-room lights.
* Ruined pass: stairwells get one flickering emergency lamp per 2 storeys (existing `bunker_lamp` model, point light range 7, intensity 0.8), enabled only within 30 m of the player (≤ 4 extra pixel lights in a block). Normal pass: warm ceiling lamp in the house's living room and kitchen, on at night only (pre-bomb homes look lived in).
* Readability without light: every loot furniture has a small `lamp_warm`-palette handle/latch accent; opened parts are the "searched" cue (animation fix 4); interior walls one palette step lighter than facades; door frames dark (today's `DoorPost` trick) so doorways read in the flashlight cone.

### Save/load and respawn
* Persistence is unchanged: searched furniture is a `CrateState` (`ProceduralWorldState.Capture`). ≈ 50 B per searched container; a fully cleared block ≈ 2.5 KB. Restore applies `RestoreSearchedState` and then the open pose.
* **No loot respawn** (the world is infinite; scarcity is per-place). Sealed flats are never openable in v1, so nothing to save there.
* Changing a building's slot list later changes ids → bump `GenerationVersion` again; document it in the version comment.
* **Bombs while inside:** the chunk is rebuilt ruined in the same place; ruined-only colliders (plugs, debris) must skip any spot overlapping the player capsule (`Physics.CheckCapsule` at build time when the player is within the building bounds).

### Art needs (new models, none duplicate `Assets/Art/Models`; origin floor centre, front −Y, palette atlas; furniture with a loot role gets named part pivots for opening)
| Batch | Asset id | Bounds x / y / z (m) | Direction |
|---|---|---|---|
| **1. Kitchen + bedroom loot** | `furn_kitchen_run` | 2.4 / 0.65 / 0..2.2 | Base cabinets + sink + worktop + wall cupboards from z 1.4; parts `Door_1..4` |
| | `furn_fridge` | 0.75 / 0.7 / 0..1.8 | Rounded 1950s fridge, rust at the base, part `Door` hinged at x −0.37 |
| | `furn_wardrobe` | 1.4 / 0.6 / 0..2.1 | Two-door wardrobe, crooked door, parts `Door_L/Door_R` |
| **2. Small loot** | `furn_dresser` | 1.2 / 0.5 / 0..1.0 | Chest of drawers with a mirror stub, parts `Drawer_1..3` |
| | `furn_nightstand` | 0.5 / 0.45 / 0..0.6 | Bedside table with lamp, part `Drawer_1` |
| | `furn_medicine_cabinet` | 0.6 / −0.18..0 / 1.1..1.8 | Wall-mounted mirror cabinet (wall at y 0) over a small sink; part `Door` |
| **3. Valuable loot** | `furn_desk` | 1.4 / 0.7 / 0..1.0 | Desk with drawer pedestal, typewriter/CRT terminal, part `Drawer_1` |
| | `furn_toolbox` | 1.0 / 0.5 / 0..1.0 | Rolling tool chest with a vice on top, parts `Drawer_1..3` |
| | `furn_safe` | 0.6 / 0.6 / 0..0.8 | Squat steel safe, dial, part `Door` (x −0.3 hinge) |
| **4. Stairs** | `bld_stairs_house` | 1.4 / 4.0 / 0..2.9 | Wooden straight flight, run along +Y, open string + handrail one side. **Visual only** (ramp collider in code) |
| | `bld_stairs_flight` | 1.5 / 2.4 / 0..1.5 | Concrete switchback flight, worn nosings |
| | `bld_stair_rail` | 2.4 / 0.1 / 0..1.0 | Steel railing segment for voids and landings, tiles at y ends |
| **5. Living room** | `furn_sofa` | 2.0 / 0.9 / 0..0.85 | Torn three-seater, stuffing showing. No loot |
| | `furn_tv_unit` | 1.6 / 0.5 / 0..1.3 | Low sideboard with CRT TV; parts `Door_L/Door_R` |
| | `furn_bookcase` | 1.0 / 0.35 / 0..1.9 | Tall shelf, a few books toppled. No parts |
| **6. Beds + bath** | `furn_bed_double` | 1.6 / 2.1 / 0..0.9 | Headboard at +Y, rumpled blanket |
| | `furn_bed_single` | 1.0 / 2.0 / 0..0.8 | Kid's bed, simpler |
| | `furn_bath_set` | 2.2 / 1.7 / 0..1.1 | Tub + toilet + basin as one corner block, cracked tiles |
| **7. Facades** | `bld_window_bay` | 3.0 / 0.35 / 0..3.0 | One storey's facade panel with a window, sill and frame; tiles at x ±1.5; ruined variant later |
| | `bld_balcony` | 3.0 / 1.3 / 0..1.2 | Slab + railing, origin at slab top on the facade line (y 0 = wall), protrudes −Y |
| | `bld_apt_entrance` | 3.6 / 0.6 / 0..3.4 | Double-door frame, **2.8 m clear x 2.4 m**, canopy, buzzer panel |
| **8. Interior structure** | `bld_doorframe_interior` | 2.1 / 0.24 / 0..2.4 | Clean frame for a 1.8 x 2.2 opening in a 0.2 wall (`ruin_doorway` is the damaged look); opening empty |
| | `bld_roof_stairhut` | 3.6 / 6.2 / 0..3.1 | Roof exit for the core, door 1.8 at −Y, vent cap |
| | `furn_mail_lockers` | 1.6 / 0.4 / 0..1.8 | Wall of dented mailboxes, parts `Door_1..2` (only two open) |
| **9. Roof + shop** | `bld_roof_water_tank` | 2.0 / 2.0 / 0..3.0 | Tank on a steel stand |
| | `bld_roof_hvac` | 1.6 / 1.0 / 0..1.2 | Boxy AC unit with fan grille |
| | `furn_shop_shelf` | 2.0 / 0.6 / 0..1.7 | Two-sided gondola, scattered cans |
| **10. Shop + office** | `furn_shop_counter` | 2.0 / 0.7 / 0..1.1 | Counter with register, part `Drawer_1` |
| | `furn_filing_cabinet` | 0.5 / 0.65 / 0..1.3 | Four-drawer cabinet, parts `Drawer_1..2` |
| | `furn_dining_set` | 1.6 / 1.6 / 0..0.95 | Table + 4 chairs, one knocked over. No loot, no collider beyond the table |
**30 models in 10 batches.** Batches 1-4 are the gameplay-critical ones (loot + stairs); 5-6 make "a house is a house"; 7-8 make blocks read as blocks from the street; 9-10 polish. Map every id in `M16ArtPass` into new `ProceduralWorldAssets` fields (furniture by slot table name), never into the builders. Write per `ArtSource/ASSET_SPEC.md`; reuse `bunker_lamp`, `bunker_locker` (`lockers`), `piece_shelf`, `ruin_debris_*`, `rubble_*`, `house_roof_*`, `house_window*`, `house_door`. **Animation art:** no clips. Part pivots on loot furniture (listed) and on `piece_bicycle` (`Wheel_F`, `Wheel_B`, `Crank`). Later and optional: first-person hands rig (6 takes: equip, swing light, swing heavy, gather, reload short, reload long) and a squatter `Sit`/`Guard` take on the humanoid enemies.

### Code work (handoff to GPT)
* **New `Assets/Scripts/World/BuildingKit.cs`:** runtime helpers shared by all types: `Wall(from, to, height, openings[])` that cuts 1.8 m doors into piers, `Slab(rect, voids[])`, `Flight(foot, dir, width, run, rise)` (visual model + one ramp collider + under-wedge), `Railing`, `Furniture(slotTable, model, pos, yaw)`, the `Static`/`Floor_n/Interior` grouping, and `StaticBatchingUtility.Combine` at the end.
* **New `Assets/Scripts/World/BuildingPlans.cs`:** data for house / shop-house / office / block (rects, doors, slots exactly as tabled above) plus the variant rules. One place to edit when the probe says a gap is too narrow.
* **`ProceduralContent.cs`:** `House` :337 → `BuildingPlans.House` (fix the :364 RNG bug, pass `coordinate`/`slot`); `Town` :241-251 type selection from `SlotRandom(500 + slot)` after the existing draws; delete `Building` :518 (`UpperFloors`); `Crate` :562 stays for cars. Smoke height uses `storeys * 3f`.
* **`LootTables.cs`:** the slot tables above, `FillSlot(loot, table, random, tier)` with the always-drawn empty roll, rare-row tier multiplier; `TableNames` updated (check `ReleaseVerification`/`CraftingProbe` for the table list).
* **`LootContainer.cs`:** optional `openParts` + 0.25 s tween on search, open pose restored when `RestoreSearchedState(true)`.
* **New `Assets/Scripts/World/BuildingInterior.cs`:** storey culling + emergency-lamp distance toggle.
* **`WorldLayout.cs`:** `GenerationVersion = 3`; new `ProceduralWorldAssets` fields for the 30 models.
* **`Assets/Editor/M16ArtPass.cs`:** load and assign the new models.
* **`EnemyAI.cs` (+ `SpeciesBrain` chase):** minimal `BuildingNav`: each building registers stair waypoints (foot, mid-landing, head per storey); when target and enemy are in the same building bounds and |Δy| > 1.5, steer along the chain. Until then, no enemies spawn above the GF (`ChunkEncounters`), and README says so.
* **Animation:** `MeleeAttacker.SwingWeapon` → curve-based swing (+ optional strike-frame damage), `FirstPersonController`/new `CameraMotion.cs` (bob, landing, flinch; toggle in `GameplaySettings`), `InteractableDoor` + `HomesteadDoorPiece` tweens, `RangedViewmodel.Tick` equip/reload/sway, `EnemyAnimationDriver` `WalkRate` parameter (and the controller built in M23 setup), `RideableBicycle` wheel spin.
* **Probes:** `ProceduralWorldProbe` (:167 assumes `Building_` crates) and `RuntimeSmokeTest` (:115 counts `Building_`), `HomeProbe` (:142-143): keep the root names `House_nn`/`Building_nn`, add checks: ≥ 9 containers per house in ruined pass, 0 in normal pass, identical layout hash between passes, every flight walkable by a 0.5 m capsule, generate time of a 2-block chunk.
* **Docs:** README "Towns & Ruins", SETUP human checks (climb every flight, clear a block, bombs while inside a house, culling pop).

### Open questions / risks
* **Raiders upstairs:** without `BuildingNav`, upper floors are a free refuge and a sniping perch. Ship nav or keep fights on the GF.
* **Generation hitch:** a block is ~10× today's building in objects; measure before deciding between one-frame build and the interior queue.
* **Loot inflation ~2× stacks:** fine for materials, watch ammo and medicine; the tier multiplier is the knob. Playtester call on whether a 30-stack block is "more loot" or "too much".
* **Save compatibility:** generation version bump makes old town/suburb crates fresh and may put a new 2-storey house where an old save had placed pieces next to the 1-storey one (footprint unchanged, so only height differs).
* **Sealed flats** may read as "fake doors"; the breakable-plug follow-up turns them into a pickaxe goal.
* **Pitched-roof houses** stay non-enterable above the UF; ask if the playtester wants attics (needs a new roof model with interior volume).
* **Culling pop** when entering a building: test from the doorway; GF always-on within 25 m should hide it.

### GPT implementation progress — building interiors and motion

Procedural generation now replaces the single empty family house with a furnished two-storey layout and a stair flight, and town lots can generate two-storey shop-houses, offices, and apartment blocks with multiple real floor groups and stair cores. Furniture uses dedicated deterministic loot slot tables; the normal pre-bomb pass keeps the same room/lot layout without searchable containers, while the ruined pass fills those slots. The house burn roll is now drawn in both passes so its lot dressing stays seeded consistently. `BuildingInterior` hides distant/currently irrelevant floor renderers while leaving their colliders active, and the world generation version is 3 so older generated chunks refresh.

Procedural melee swing curves with contact hit-stop, eased door visuals, and player camera motion have also been added. `LootContainer` now animates authored `Door_L`, `Door_R`, `Drawer_*`, and `Lid` pivots in 0.25 s, and restores the open pose from searched save state; pivots can also use explicit local poses. A `--building-probe` checks seeded layout stability, normal/ruined loot separation, furniture slot tables, and coarse entrance clearance. C# compile check passes (195 runtime + 29 editor files). The Unity player build and runtime probe still need to be run in a clean scratch project, followed by human play checks for stair collisions, apartment navigation, renderer culling, and animation feel. Raiders do not yet follow stair routes; the probe does not yet verify every stair flight or generation time. Blender assets with these pivots and their Resources wiring remain the art handoff; no current furniture asset has been confirmed to provide them.

## 2026-11-10 Vehicle mounting, driving feel and persistence (playtester request, design note)

Playtester (translated): "Vehicle control needs to be better. When you get in, the car disappears: it needs an animation, and the vehicle should still be there. And when I get out, the vehicle should stay there too."

**What the code does today (read, not played):** the bicycle, Scout Buggy and Armored Van (`Assets/Scripts/World/RideableBicycle.cs`, `RideableScoutBuggy.cs`, `RideableArmoredVan.cs`) are not driven objects. The *player's* `CharacterController` moves, the vehicle is only placed relative to it each frame: bicycle at `rider.position - forward * 0.8` and 0.65 m lower (`RideableBicycle.cs:69-71`), the van snapped to `rider.position - forward * 3` on mount (`RideableArmoredVan.cs:177`), the buggy by its own follow code (`RideableScoutBuggy.cs:132-136`). On mount, `rider.SetBicycleMounted(true)` and all vehicle colliders are disabled (`:119-120`, `:164-165`, `:175-176`). There is no mount or dismount animation, no seat, no cabin view and no interior; the first-person camera sits at the player's eye height wherever the player stands.

**Likely causes of "the car disappears" (to confirm in Play Mode, none verified):**
1. The camera ends up inside or just behind the vehicle mesh, which is back-face culled, so the vehicle looks like it vanished (the van is pulled 3 m behind the eye, the buggy and van models are 3.8-6.5 m long with the camera nowhere near a seat).
2. The vehicle is moved by a script offset from the rider, so any frame order or clearance clamp (`RotationHasClearance`, nearest-hit clamps) can pop it away from where it was left.
3. On dismount the vehicle should remain exactly where it stopped, with its colliders re-enabled; check that `Dismount()` restores the collider states and does not re-anchor to the rider, and that its pose is saved with the piece (`PlacedPiece` / `IPieceState`) so it also survives save/load and chunk streaming.

**Requested behaviour (design):**
* **The vehicle is always a visible, solid world object.** It never hides, never changes parent, and stays where it was parked after dismount (and after F5/F9 and chunk unload/reload).
* **Mount animation (about 0.5-0.7 s):** the camera moves from the player's position to a seat anchor on the vehicle (door side), eased, with the door part swinging open and closed. Controls are locked during it. **Dismount** is the reverse: camera rises out of the seat to a clear spot beside the vehicle, validated with a capsule check so the player is never placed in geometry or the vehicle.
* **Seat anchors on the model:** every vehicle model gets an empty `Seat` (camera position and yaw) and `Exit_L`/`Exit_R` (candidate dismount points), plus named door parts. Art owner: Claude (models). Code owner: GPT.
* **Driving feel:** a real driver view (dashboard, steering wheel, mirrors) instead of a camera floating in the body; steering and speed lag (acceleration, braking, turn rate that depends on speed), wheel spin and steering animation from speed, suspension bob, engine idle/rev tied to speed (audio is already a separate system, `GameAudio`), headlight cone from the model.
* **Collision:** the vehicle should keep real colliders while driven if the controller design allows it (today they are disabled, with a sweep for ramming instead). Either way the vehicle must stop at walls and buildings, not clip them.
* **Bicycle:** visible legs/pedals turning, handlebar yaw, and the mount/dismount animation as above (needs the `Wheel_F`, `Wheel_B`, `Crank` pivots planned in the buildings proposal).

### Requested follow-up — vehicle audio and terrain handling

The playtester additionally wants vehicles to sound and respond like vehicles while moving, and to climb and descend uneven ground. This is a gameplay/audio task; coordinate with Claude before touching `Rideable*.cs`, `PlayerAudio.cs`, or audio presentation files because those are currently in active work.

* **Engine loop:** play a spatial vehicle motor loop while mounted. Pitch and volume should follow throttle, wheel speed, and load: quiet idle when stopped, rising revs during acceleration/uphill travel, and settling back when coasting or braking. Start/stop cleanly on mount, dismount, pause and destruction; avoid stacking a new AudioSource every frame. Keep it attached to the vehicle so nearby players hear it at the right distance.
* **No walking footsteps while riding:** suppress player footstep/jump/landing locomotion cues while the mount view is attached or the player is mounted. Resume normal surface footsteps on dismount without resetting unrelated survival or hurt sounds. Bicycle can use chain/pedal/tyre sounds instead of a motor.
* **Terrain following:** sample ground under the vehicle footprint/wheels and keep the body grounded over hills instead of floating, tunnelling or retaining a flat world-space height. Add bounded pitch/roll from the sampled slope; smooth changes to avoid camera jolts. Keep the existing obstacle sweep and prevent ground correction from pushing the vehicle through walls or floors.
* **Grade and traction:** uphill travel should remain possible, with reduced acceleration/top speed as the slope steepens; downhill travel should gain coasting speed but remain controllable with braking. Clamp the grade to a designed maximum and stop/slide gently beyond it rather than launching or snapping. Rain, loose ground and damaged terrain can reduce traction later, after the basic dry-ground loop is stable.
* **Verification:** test each vehicle ascending and descending the same gentle and steep slopes, across a slope sideways, and against a wall at the slope break. Confirm tyres/body stay above terrain, the camera stays above ground, speed changes with grade, engine sound tracks speed/load, footsteps stop while mounted and resume after dismount.

**Art needs (Claude):** add `Seat`, `Exit_L`, `Exit_R` empties and door parts (`Door_L`) to `piece_scout_buggy` and `piece_armored_van`; wheel part pivots (`Wheel_FL/FR/RL/RR`) on both; `Wheel_F`, `Wheel_B`, `Crank` on `piece_bicycle`; a simple interior (dashboard, steering wheel, seats) visible from the `Seat` camera. Optional later: a first-person hands rig for steering. **Code work (GPT):** read the three `Rideable*` scripts, move the player camera to `Seat` with an eased mount/dismount coroutine, keep the vehicle untouched in the world, save its pose, and add probes (vehicle renderers enabled before, during and after mounting; parked pose unchanged after dismount; pose survives save/load).
**Open question for the playtester:** is the vehicle completely invisible when mounted, or is the camera inside it? And does it vanish after dismount too, or only while riding?

### GPT implementation progress — vehicle mount view

Code now uses a shared `VehicleMountView` for the bicycle, Scout Buggy and Armored Van. It eases the camera to a `Seat` anchor, locks player input during the transition, checks `Exit_L`/`Exit_R` with the player capsule and tries surface-projected, capsule-clear positions around the vehicle when an exit is blocked. The camera pivot remains a child of Player so interaction and inventory lookup keep working. The camera and player return to a normal first-person state after leaving; renderers are never disabled, and the vehicle script stops changing its transform after dismount. Existing save data already stores placed vehicle world position/rotation and each vehicle's cargo/fuel state.

Claude owns the `Seat`, `Exit_L`, `Exit_R`, `Door_L`, wheel and cabin art anchors, as recorded in `DEV_COORDINATION.md`; runtime fallback anchors work before those models arrive. Static compile passes (185 runtime + 29 editor files). The mount/dismount path still needs standalone and human Play Mode passes, including exits on slopes. This slice does not yet add acceleration/steering smoothing, steering-wheel or wheel animation, or a visible authored interior.

## 2026-11-10 Art integration, vehicle interiors, turret fix (Claude, this session)

**Models in the game (all rendered in a scratch Unity, 0 `error CS`, probes below):** Armored Van, Garage, projectile models (`fx_acid_glob`, `fx_chem_flask`, wired in `SpeciesProjectile`), damaged/critical and intact brick/reinforced/concrete walls and foundations, redone bicycle (front +Y, collider .7 x 1.3 x 1.8), five held melee weapons (`MeleeViewmodel`: model per `PlayerGear.WeaponId`, held upright), bunker props (Supply Cage, reinforced door leaf as `HomeBase.reinforcedDoorLeaf`, workshop tool walls, hydroponics tanks and grow lights), hanging `piece_light_ceiling`, vehicle `Seat`/`Exit_L`/`Exit_R`/`Door_L`/`Wheel_*`/`Crank` parts plus cab interiors (the closed hull boxes are back-face culled from the seat, which was why the vehicle looked invisible once mounted). Models built but not wired anywhere yet: 24 `furn_*`/`bld_*` building pieces (need the generator from the buildings proposal), `melee_hammer`/`iron_axe`/`iron_pickaxe`, `piece_floodlight`, `prop_vault_door`, `prop_delivery_pallet`, `prop_kit_crate`, `prop_room_bulkhead`.

**Turret bug (root cause found with the new `--place-probe`):** all five defence pieces (MG turret, laser turret, spike trap, shock trap, alarm) were serialised into prefabs with a missing script, because their MonoBehaviours lived in one file, `DefencePieces.cs`. Unity only keeps a script reference for a class whose file has the same name, so the ghost threw a `NullReferenceException` in `EnsureGhostForPiece` and nothing appeared. Fixed by one file per component (`MachineGunTurretPiece.cs`, `LaserTurretPiece.cs`, `SpikeTrapPiece.cs`, `ShockTrapPiece.cs`, `AlarmPiece.cs`). **Rule for new work: a MonoBehaviour that is serialised into a prefab or scene needs its own file named after the class.** `grep -rlE 'm_Script: \{fileID: 0\}' Assets` must stay empty (it is empty now).

**Ceiling light:** a new `ceilingMount` branch in `BuildModeController` (aim at a ceiling underside; the footprint centre hangs half its height below it; the model origin is the mount point, `M21CraftingSetup` offsets the visual up for it). Footprint .4 x .5 x .4.

**Verified in a built player (scratch copy, not played by a human):** smoke, raid, power, m28, place, homestead, weapons, bestiary, storage, world, encounters, objectives probes pass. **Known failing, already on GPT's commit `cb297db` before these changes (verified by building a clean `HEAD`):** `--crafting-probe` (the "starting workbench exists" check is stale: the bench is now a shop offer, `kit_workbench` at $1,200, and the probe aborts after 58 checks) and `--home-probe` (the north-wing opening and the open vault doorway do not pass the raider-capsule check at x=68, z=-211.65). Not verified: Play Mode feel of vehicles, melee hold pose in motion, van cab view (the window is a slit and the headliner fills much of the view).

## 2026-11-10 Vehicle height bug, probe fixes, silent probes (Claude)
**Player fell under the map once in a vehicle:** `RideableScoutBuggy/Bicycle/ArmoredVan` set the vehicle root to `rider.y - 0.85/0.65/1.0`, but the prefab root is the footprint centre (floor half the footprint height below it), so the vehicle sank 1.3-1.9 m into the ground and the seat camera ended up underground. Now `rider.y + RootHeight` (1.05 / 0.65 / 1.25). New `--vehicle-probe` places, mounts, drives 6 m and dismounts each vehicle, checking it is drawn, above ground, and the camera is above the terrain (passes). **Probes fixed after the interiors commit:** `--crafting-probe` (furniture loot tables handed out the crafted-only `circuit_board`, now `energy_cell`; the old "starting workbench" check now places a bench), `--home-probe` (raider capsule was tangent to the floor plane; the probe now lifts it 2 cm and logs the blocking collider), `--world-probe` (crate check assumed a one-room building; entrance, floors and stairs are `--building-probe`'s job), `--building-probe` (after the bombs sealed apartment flats drop their `C##` slots on purpose; only that is allowed). **Probes run silent:** `tools/run_probe_silent.sh <player-dir> --<probe> <log>` starts the player with no audio device (log shows "FMOD initialized on nosound output"); never run `--audio-probe` on the user's speakers.

**Build refresh (Claude, 2026-11-10 ~16:00):** `Builds/Linux` and the generated prefabs/scene now contain the furnished multi-storey buildings, animated furniture, vehicle height fix, vehicle seats/interiors, turret fix and the bunker economy. Built from a full `BatchSetupAll.RunAll` in a scratch copy; 15 probes pass (smoke, world, building, vehicle, raid, place, home, crafting, homestead, weapons, bestiary, storage, power, m28, encounters, objectives), 0 prefabs/scenes with a missing script. GPT's uncommitted car-windshield FBXs were in the tree at build time. Not played by a human.

## 2026-11-10 Vehicle audio (Claude)
New loops `motor_buggy`, `motor_van`, `bike_chain` (`ArtSource/audio/gen_audio.py` section `m29`, `build_audio.sh m29`, seamless via `write_device_loop`, imported ADPCM like the other device loops). `VehicleAudio.cs` is added to the player by `GameAudio`; it only observes public state (`FirstPersonController.IsRiding`, `VehicleMountView.IsAttachedTo`, the vehicle's own speed from its transform), pitch 0.72-1.85 follows speed, the bicycle chain is silent when still, paused = silent. `PlayerAudio` now plays no strides or landings while riding (`StepsPlayed` counter for probes). `--vehicle-probe` checks the right loop plays per vehicle and that the body made no steps (passes). Sounds are generated, not heard by a human; levels and pitch range are a first guess.

## 2026-11-10 Models batch 9-10 and van cab view (Claude)
New unwired models (FBX + sources): `bld_roof_water_tank`, `bld_roof_hvac`, `furn_shop_shelf`, `furn_shop_counter` (`Drawer_1`), `furn_filing_cabinet` (`Drawer_1`, `Drawer_2`; bounds y -0.36..0.335 because drawer fronts and handles stick out), `furn_dining_set`. `piece_armored_van` got a much wider windscreen (about 3.2 x 0.58 m) and 1.35 x 0.65 m side windows, cab raised about 0.25 m; anchors, parts and pivots unchanged; from the `Seat` camera (rendered in Unity, FOV 70) the horizon and hood are clear and the headliner takes about a quarter of the view. The builder's glass quads had inward-facing normals (`normal_update()` was missing), fixed in the van script.

**Build refresh 2 (Claude, 2026-11-10 ~17:15):** `Builds/Linux` rebuilt from `765fa14` (terrain-aware vehicles, vehicle audio, wider van windscreen, new furniture models still unwired). All 16 silent probes pass (smoke, world, building, vehicle incl. slope and audio, raid, place, home, crafting, homestead, weapons, bestiary, storage, power, m28, encounters, objectives). `--m28-probe` needed one fix: the van's `ResolveVehicleMotion` gained a rotation parameter in the terrain pass, the probe's reflection call now passes it. Not played by a human.

## 2026-11-10 Furniture and stairs actually shown (Claude, work board C1)
**Root cause of "interiors look like boxes":** `ProceduralContent.Prop(id)` loads `Resources/Props/<id>`, but nothing created prefabs for `furn_*`/`bld_*`, so every piece of furniture, the stairs and the rooftop parts fell back to coloured boxes. `M22SystemsSetup` now creates a prefab for every `furn_*` and `bld_*` FBX (30 prefabs), a new model only needs its FBX. Added the roof water tank (`Decor`, no collider, no random numbers). New `--interior-shots <folder>` (run WITH graphics, silent audio env) writes PNGs of a ruined house and block: hall, stairs, lobby, roof; rendered in the player and looked at: wooden stairs with handrail, sofa, bookcase, fridge, doorframes, brick stairwell, rooftop stair hut + HVAC + tank. Not placed: `furn_dining_set`. 16 silent probes pass on the build in `Builds/Linux`.

## 2026-11-10 Audio for interiors and engines, wildlife/camp/landmark models (Claude, work board C2/C3)
**Sounds (`gen_audio.py` section m30, `build_audio.sh m30`):** `drawer_open` (3 variants), `cabinet_open` (3), `engine_start`, `engine_stop`, `amb_interior` (seamless 8 s room tone with distant drips). `LootContainer` now plays the drawer sound when its open parts are `Drawer_*`, the cabinet sound for `Door*` parts, the plain crate sound otherwise, and a plain `Door` part (fridge, safe, medicine cabinet) opens like `Door_L`. `VehicleAudio` plays the ignition on mount (motor loop fades in after 1.5 s) and the stop sound on dismount, not for the bicycle. `InteriorAudio` (added by `GameAudio`) fades the room tone in while `BuildingInterior.PlayerIn` is set and plays an occasional creak. `--interior-audio-probe` and the extended `--vehicle-probe` check it all without sound (clips imported, tone plays inside and stops outside, drawer/cabinet keys played, ignition played). Generated, not heard by a human; no horn (needs a key). **Models (FBX + sources, not wired to any spawner yet):** `animal_deer`, `animal_boar` (parts `Torso`,`Head`,`Foreleg_L/R`,`Hindleg_L/R`; the old wildlife code uses other names), `animal_crow` (`Torso`,`Head`,`Wing_L/R`, origin at the body, feet at z -0.15), `camp_tent` (2.8 x 3.6 x 2.1, open front), `camp_fire`, `camp_locked_chest` (part `Lid`, hinge at the back edge), `lm_relay_cabinet` (`Door`,`Lamp`), `lm_fuel_pump`, `lair_warning_post` (`Lamp`).

## 2026-11-10 Vault door and delivery pallet (Claude, work board C4)
`prop_vault_door` (frame fused to a closed leaf) is split into `prop_vault_door_frame` and `prop_vault_door_leaf` (origin at the floor centre, hinge side x=-0.9, front -Y, same convention as `blast_door_leaf`). `M20HomeSetup.BuildVault` hangs the leaf on the existing `VaultDoorHinge`/`InteractableDoor` and places the frame in the doorway, so the vault door now swings open (rendered in Unity: wheel, bolts and hinge barrels inside the frame). The Supply Cage uses `prop_supply_cage`; `prop_delivery_pallet` stands by the stair head from the moment the contract is signed until the bunker is sealed (`HomeBase.deliveryPallet`, toggled in `RefreshVisuals`). Still open: an emissive grow light (needs an emissive material, the atlas has none).
