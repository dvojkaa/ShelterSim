# SheltreSim

A small first-person bunker-builder and survival game. Build out a bunker, trigger the
apocalypse when you're ready, then survive inside it while scavenging resources from the
irradiated world outside — all before the raiders find you.

Think *Fallout* scaled down to a weekend-sized project: stylised low-poly visuals, build, scavenge,
bash the occasional raider, and endure. The original outdoor area is still entirely hand-placed;
M10 added a much larger scavenging zone beyond it that uses seeded (deterministic, reproducible)
random placement for its buildings, cars, and loot — see Known limitations below for exactly
what "random" means here.

## Status

**Playable prototype — M1–M15, followed by a gameplay and reliability pass.** Built with AI
assistance. Construction checks and opt-in standalone regression tests complement human
playtesting; passing compilation alone does not prove gameplay correctness. See
[`DESIGN_NOTES.md`](DESIGN_NOTES.md) for the full build log and every design decision made
along the way, and [`SETUP.md`](SETUP.md) for a milestone-by-milestone playtest checklist.

## Gameplay loop

1. **Build** — construct and expand your bunker, room by room.
2. **Trigger the apocalypse** — a point-of-no-return button inside the bunker. Once you press
   it, survival pressure begins for real.
3. **Survive** — hunger, thirst, and health tick down continuously from that point on.
4. **Scavenge** — venture out through the bunker's doorway to gather Wood, Scrap Metal, Stone,
   Iron Ore, and Blueberries, loot buildings and cars in the wider wasteland for Water, First
   Aid Kits, and weapon upgrades, and fight off (or flee) the raiders patrolling outside.
5. **Craft** — spend gathered resources to build more of your bunker. Repeat.

## Features

| Milestone | What it adds |
|---|---|
| M1 | First-person movement & look, interaction system (raycast + prompt), a starter bunker room |
| M2 | Grid-snapped building system — 7 placeable pieces (Wall, Floor, Workbench, Bed, Storage Crate, Water Collector, Generator) with a live ghost preview |
| M3 | Hunger/thirst/health, an on-screen HUD, and JSON save/load (F5 / F9) |
| M4 | A 40×36m outdoor wasteland beyond the bunker doorway — 8 gatherable Wood/Scrap Metal nodes plus decorative rubble |
| M5 | Crafting — every build piece costs real resources, drawn from your inventory |
| M6 | 4 raiders — simple AI that detects, chases, and attacks on sight (at this point unbeatable; M8 added the means to fight back) |
| M7 | The apocalypse trigger itself, tying the whole loop together |
| M8 | Pause menu, inventory screen ("I"), melee combat with a procedural weapon swing, and starting furniture in the bunker |
| M9 | More resource variety (trees, stone, iron ore) and a food source (blueberry bushes) you can eat from the inventory screen to recover hunger, plus a read-only recipe/crafting list in that same screen. The inventory is a modal panel now — it frees the cursor and freezes the game while open, same as the pause menu |
| M10 | A second, much larger wasteland zone (~24,000m², ~17x the original outdoor area) beyond the first, with 12 lootable building shells and 15 lootable cars scattered via seeded random placement. 38 loot crates roll (once, deterministically) into Wood/Scrap Metal/Stone/Iron Ore/Blueberries/Water/a First Aid Kit/a weapon upgrade, or nothing (see M12 — the empty-crate rate was cut right back in the loot rebalance). Water (drink) and First Aid Kits (heal) get their own inventory buttons alongside Eat; a weapon upgrade item permanently boosts melee damage when Equipped |
| M11 | The Workbench and Water Collector build pieces actually do something now. Workbench: press E to craft a Makeshift Blade (2 Scrap Metal + 2 Wood) — the same weapon-upgrade item M10 made lootable. Water Collector: passively accumulates 1 Water every 90s (caps at 5), press E to collect |
| M12 | A one-click **▶ START GAME** menu item that builds everything and presses Play. Raiders now have real collision (they're blocked by walls/buildings/cars instead of walking through them) and will force open a closed bunker door and keep chasing rather than giving up partway home. Loot crates are more generous (85% chance of contents, 1-2 varied item stacks per hit instead of always one). The big M10 wasteland is no longer empty/pressure-free — trees, stone, iron ore, food bushes and 8 more raiders are scattered through it, so the whole map reads as one mixed environment instead of "nature zone, then city zone." Interaction prompts, loot/craft/combat results now show on screen (crosshair + prompt + message feed) instead of being Console-only |
| M13-M15 | An Opus-model review's full suggestion list, implemented in one round: a "You Died" screen with a Restart button; survival decay retuned to ~6 minutes of do-nothing survival instead of ~79 seconds; a build-mode HUD readout showing the selected piece's cost and affordability; save/load now covers searched loot crates, depleted resource nodes (which also regrow over time instead of staying gone forever), a Water Collector's pending water, and the current game phase; raiders respawn 90s after dying and flash white when hit; the Bed now lets you sleep (heal, at the cost of some hunger/thirst, blocked if a raider's nearby). Plus a full visual pass — raiders, the player's weapon, trees/rocks/bushes/rubble, the bunker, ruined buildings, cars, all 7 build pieces, loot crates, and the apocalypse button all gained extra compound-primitive detail (still grey-box, just less flat) |

| M16 | Full art pass: 49 low-poly Blender models and 7 tiling textures replace the grey-box primitives in place: furniture, doors, trees, rocks, scrap, wrecked cars, ruins, raiders with a walk cycle, and both weapons, plus dusty sky, lighting and fog. Positions, colliders and save IDs are unchanged |

Art is generated by code: Python scripts in `ArtSource/` run in headless Blender and export FBX
models into `Assets/Art/`. See `ArtSource/ASSET_SPEC.md` for the spec and how to rebuild an asset.

## Controls

| Action | Key |
|---|---|
| Move | WASD |
| Look | Mouse |
| Jump | Space |
| Sprint | Left Shift |
| Interact | E |
| Toggle build mode | B |
| Cycle build piece | Mouse wheel |
| Rotate ghost piece | R |
| Place piece | Left click |
| Cancel build mode | Escape |
| Save / Load | F5 / F9 |
| Inventory / consumables / recipe reference | I |
| Melee attack | Left click outside build mode |
| Pause / resume | Escape outside build mode |

## Tech

- **Unity 6** (6000.6.8f1), Built-in Render Pipeline
- New Input System — every control scheme is built entirely in code (no `.inputactions` asset)
- Low-poly/primitive grey-box art — intentional, not a placeholder
- Single Unity scene (no scene-transition loading between bunker and outdoors)

## Running it

Open the project in Unity Hub and press Play — the scene already has everything wired up. If
you ever need to regenerate it from scratch, the fastest way is **`Tools > SheltreSim > ▶ START
GAME (build everything + Play)`** — one click runs all milestones' setup tools in order and
drops you straight into Play mode, no need to click through them individually. Use the full chain when regenerating: individual older setup tools can overwrite later
features. `Tools > SheltreSim > Verify Current Game` checks wiring, economy, and loot geometry;
`Build Linux Player` creates `Builds/Linux/SheltreSim.x86_64` from the current game scene. See
[`SETUP.md`](SETUP.md) for the exact steps and a full playtest checklist.

## Known limitations

Combat is one melee weapon (upgradeable via a looted *or* workbench-crafted weapon item) and one
swing — raiders have health, die, flash on hit, and respawn 90s later; there are still no ranged
weapons, no blocking. No day/night cycle or weather, so the Water Collector's "collecting
rainwater" is a flat timer rather than a simulation. "Random generation" (M10's buildings/cars/loot,
M12's biome blend) is seeded and rolled once at scene-construction time by an editor tool, the
same way every other milestone's content gets built — it is not runtime randomness, the layout
and loot contents are identical every time you open the project, and a new "random" layout only
happens if the project owner re-runs the setup tool. Building and crafting aren't locked to the
post-apocalypse phase (you can keep building any time). The inventory's "Recipes" list is
read-only reference — it shows what you can afford, but actually placing a piece still happens
through build mode (B), not from this screen. Save files now cover the player, survival stats,
placed build pieces (including a Water Collector's pending water), inventory counts, melee
damage, searched loot crates, resource-node depletion, and the current game phase. Interaction
prompts, loot/craft/combat results, and "cannot afford" messages
now also show on screen (a crosshair, a prompt line, and a small fading message feed), not just
the Console — though the Console log is still there too. Raiders have real collision (walls,
buildings, cars block them) and will force open a closed bunker door rather than be stopped by
it, and no longer give up chasing partway home — there's no pathfinding, though, so one can still
get stuck sliding along a wall it doesn't have a direct line through. The expanded wasteland now
has resource nodes and raiders scattered through it alongside the buildings/cars, but reaching
its far end and getting back may still not be survivable on the current decay rates. All
deliberate scope decisions for a first prototype, not oversights — see `DESIGN_NOTES.md` for the
reasoning behind each one.

## Reliability and usability pass

Save/load distinguishes identically named crates, preserves partial resource/water timers,
restores camera pitch, and safely recovers from death. Resting requires enough food and water;
preview furniture cannot act as real furniture. Building checks the current target when clicked,
uses a practical placement range, and shows yellow when a clear location is unaffordable.
Recipes combine repeated material costs and spend atomically. Generated ruins keep loot clear
of their entrance lanes without re-rolling the world seed.

The standalone player supports an optional regression run:

```sh
./Builds/Linux/SheltreSim.x86_64 -batchmode -nographics --smoke-test -logFile /tmp/sheltersim-smoke.log
```

This runs the real scene lifecycle and checks save/load and death recovery using temporary
snapshots, without modifying your normal save. It exits with status 0 on success. Normal launches
skip this diagnostic entirely. See `SETUP.md` for the interactive checks that remain necessary.

A second opt-in run checks the M16 art in the real player. It needs a GPU, so leave out `-nographics`:

```sh
./Builds/Linux/SheltreSim.x86_64 -batchmode --art-probe /tmp/sheltersim-art -logFile /tmp/sheltersim-art.log
```

It writes screenshots to the given folder, logs `[ArtRuntimeProbe] ALL CHECKS PASSED`, and exits with
status 0 on success.
