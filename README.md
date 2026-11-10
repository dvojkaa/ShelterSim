# SheltreSim

First-person bunker building and survival in a seeded, streamed wasteland. Prepare at home, trigger the apocalypse, explore, scavenge, build defenses and power grids, and broadcast for rescue.
**Tech stack:** Unity 6 (6000.6.8f1) / C# / Linux / Built-in Render Pipeline / New Input System.

---

## Play it (Linux testers)

No Unity required for standalone testing:

```sh
git clone git@github.com:dvojkaa/ShelterSim.git
cd ShelterSim
./Builds/Linux/SheltreSim.x86_64
```

See [`SETUP.md`](SETUP.md) for the complete playtest checklist and diagnostic probes.

---

## The World & Exploration

* **Streamed 128m chunks:** Deterministic terrain, roads, and waterways generated from seed; unloads at distance.
* **Six biomes:** Wasteland, pine forest, desert, snow, wetlands, and highlands.
* **Towns & Ruins:** Street grids, abandoned vehicles, searchable buildings, and suburban house ruins.
* **Six major landmarks:** Hospital, factory, fuel station, farm, military outpost, and abandoned bunker.
* **Bestiary & Wildlife:** 12 enemy species and landmark bosses with 5-take animations, raider camps with locked chests, dodgeable Mirelurker acid and Scavenger flask projectiles, plus wander/flee deer, boars, and birds.
* **Atlas (`M`):** Procedural seeded map showing terrain, biomes, roads, town districts, discovered landmarks, and home direction.

---

## Gameplay Loop

1. **Pre-Apocalypse:** Start on a suburban lot with **$25,000**. Use the site terminal to contract 4 stages of bunker construction and buy supplies. No raiders, hazards, or loot exist yet.
2. **Apocalypse:** Completing the final stage triggers sirens (45 seconds to get underground). Bombs drop: surface burns, flash/quake damage above ground, and the world switches to its Ruined state.
3. **Survival:** 20-minute day/night cycle with dynamic weather (clear, fog, rain, snow, overcast). Manage hunger, thirst, health, temperature exposure, and radiation hot spots.
4. **Scavenging & Foraging:** Scavenge ruins and vehicle trunks; harvest trees, rocks, berry bushes, tall grass, and river reeds; collect dirty water directly from rivers and lakes.
5. **Crafting & Homesteading:** 96 items, 71 recipes across 6 stations (Workbench, Stove, Forge, Chem Lab, Electronics Bench, Fabricator). Place 20+ build pieces (walls, doors, stairs, roofs, defenses, farm plots).
6. **Base Automation & Power:** Proximity power grid (25m wireless link range) with solar panels, fuel generators, batteries, and cooling units to prevent overheating.
7. **Raids & Base Defense:** Periodic raider assaults with warning sirens. Build sandbags, barricades, spike/shock traps, and automated machine gun / laser turrets.
8. **Endgame:** Craft and power the Radio Beacon in your bunker. Survive a 5-minute, 3-wave assault to achieve rescue, view your survival ledger, and continue in endless mode.

---

## Controls

| Action | Key |
|---|---|
| **Move / Look** | `WASD` / Mouse |
| **Sprint / Jump / Crouch** | `Shift` / `Space` / Hold `Left Ctrl` |
| **Interact / Gather** | `E` |
| **Flashlight** | `F` (uses Energy Cells when drained) |
| **Quick Consumables** | `3` (Medicine) / `4` (Food) / `5` (Drink) |
| **Weapons & Combat** | `1` Melee / `2` Firearm or Energy / `Left click` Attack / `R` Reload |
| **Inventory & Crafting** | `I` or `Tab` (Inventory) / `C` (Hand Crafting) |
| **Build Mode** | `B` (Toggle) / `Scroll wheel` (Select) / `R` (Rotate) / `Left click` (Place) |
| **Repair / Dismantle** | `V` (Repair) / `X` (Dismantle, in build mode with Hammer) |
| **Bicycle** | `E` (Mount) / `P` (Panniers) / `Space` (Dismount) |
| **Scout Buggy** | `E` (Mount) / `H` (Fuel) / `J` (Energy Cell) / `L` (Headlights) / `P` (Trunk) / `Space` (Dismount) |
| **Armored Van** | `E` (Mount) / `H` (Fuel) / `J` (Energy Cell) / `L` (Headlights) / `T` (Turret) / `P` (Cargo) / `Space` (Dismount) |
| **Atlas Map** | `M` |
| **Quick Save / Load** | `F5` / `F9` |
| **Pause Menu & Settings** | `Escape` (Sensitivity, FOV, FPS toggle, Audio volumes) |

---

## Verification & Diagnostics

Fast command-line verification without launching the Unity Editor GUI:

```sh
# Fast Roslyn script compilation check (2 seconds, parallel-safe):
./tools/compile_check.sh

# Standalone diagnostic probes (run in built Linux player):
./Builds/Linux/SheltreSim.x86_64 -batchmode -nographics --smoke-test -logFile /tmp/smoke.log
./Builds/Linux/SheltreSim.x86_64 -batchmode -nographics --crafting-probe -logFile /tmp/crafting.log
./Builds/Linux/SheltreSim.x86_64 -batchmode -nographics --world-probe -logFile /tmp/world.log
```

---

## Current Scope & Limitations

* **Implemented:** Full pre-apocalypse to endgame loop, procedural world streaming, brick/reinforced/concrete build tiers, firearms and energy weapons, electricity & automation, raids & defense, 12 bestiary species with FBX animations, telegraphed Mirelurker and Scavenger projectiles, Scrap Bicycle, fuel/energy-cell Scout Buggy with collision sweeps and capped impact impulses, a ramming and turret-equipped Armored Van, powered garage vehicle assembly/repair, backpack upgrades to 120 kg, radiation, water harvesting, crouch stealth, nearby-storage crafting and audio. The M27–M28 vehicle and structure pieces are wired into the build palette.
* **Deliberately out of scope:** Multiplayer/co-op, swimming mechanics, NPC dialogue trees, second-floor building interiors in ruins. Vehicles use controller-driven movement with query-based collisions, not full Rigidbody vehicle simulation.
