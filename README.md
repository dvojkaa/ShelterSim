# SheltreSim

A small first-person bunker-builder and survival game. Build out a bunker, trigger the
apocalypse when you're ready, then survive inside it while scavenging resources from the
irradiated world outside — all before the raiders find you.

Think *Fallout* scaled down to a weekend-sized project: grey-box visuals, build, scavenge,
bash the occasional raider, and endure. The original outdoor area is still entirely hand-placed;
M10 added a much larger scavenging zone beyond it that uses seeded (deterministic, reproducible)
random placement for its buildings, cars, and loot — see Known limitations below for exactly
what "random" means here.

## Status

**Prototype — the original 7 planned milestones plus three rounds of playtester-driven
follow-up (M8-M10).** Built with heavy AI assistance (Claude), verified to compile and run
cleanly via headless Unity batch-mode automation after every change. See
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
| M10 | A second, much larger wasteland zone (~24,000m², ~17x the original outdoor area) beyond the first, with 12 lootable building shells and 15 lootable cars scattered via seeded random placement. 38 loot crates roll (once, deterministically) into Wood/Scrap Metal/Stone/Iron Ore/Blueberries/Water/a First Aid Kit/a weapon upgrade, or nothing — about 30% of crates come up empty. Water (drink) and First Aid Kits (heal) get their own inventory buttons alongside Eat; a weapon upgrade item permanently boosts melee damage when Equipped |
| M11 | The Workbench and Water Collector build pieces actually do something now. Workbench: press E to craft a Makeshift Blade (2 Scrap Metal + 2 Wood) — the same weapon-upgrade item M10 made lootable. Water Collector: passively accumulates 1 Water every 30s (caps at 5), press E to collect |

All world objects are flat-colored primitives (still grey-box by design, just not all the same
grey/white anymore) — generated entirely in code, no imported art assets.

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

## Tech

- **Unity 6** (6000.6.8f1), Built-in Render Pipeline
- New Input System — every control scheme is built entirely in code (no `.inputactions` asset)
- Low-poly/primitive grey-box art — intentional, not a placeholder
- Single Unity scene (no scene-transition loading between bunker and outdoors)

## Running it

Open the project in Unity Hub and press Play — the scene already has everything wired up. If
you ever need to regenerate it from scratch, each milestone has its own menu item under
`Tools > SheltreSim`, or run them all at once via `Tools > SheltreSim` → see
[`SETUP.md`](SETUP.md) for the exact steps and a full playtest checklist.

## Known limitations

Combat is one melee weapon (upgradeable via a looted *or* workbench-crafted weapon item) and one
swing — raiders have health and die, but there are no ranged weapons, no blocking, and no hit
reactions. No day/night cycle or weather, so the Water Collector's "collecting rainwater" is a
flat 30-second timer rather than a simulation, and a single collector produces roughly 70% of
what thirst drains — build two if you want to live off them. "Random generation" (M10's buildings/cars/loot) is seeded and
rolled once at scene-construction time by an editor tool, the same way every other milestone's
content gets built — it is not runtime randomness, the layout and loot contents are identical
every time you open the project, and a new "random" layout only happens if the project owner
re-runs the setup tool. Building and crafting aren't locked to the post-apocalypse phase (you
can keep building any time). The inventory's "Recipes" list is read-only reference — it shows
what you can afford, but actually placing a piece still happens through build mode (B), not from
this screen. Save files cover the player, survival stats, placed build pieces, inventory counts
and (since M10) melee damage — but not which loot crates you've already searched, resource-node
depletion, a Water Collector's pending water, or the current game phase, so those reset when you
reopen the project. Interaction prompts are Console messages; there is no on-screen prompt UI. The expanded
wasteland has no enemies in it, and reaching its far end and getting back may not be survivable
on the current decay rates. All deliberate scope decisions for a first prototype, not oversights
— see `DESIGN_NOTES.md` for the reasoning behind each one.
