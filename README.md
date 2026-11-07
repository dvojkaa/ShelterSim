# SheltreSim

A small first-person bunker-builder and survival game. Build out a bunker, trigger the
apocalypse when you're ready, then survive inside it while scavenging resources from the
irradiated world outside — all before the raiders find you.

Think *Fallout* scaled down to a weekend-sized project: one hand-built outdoor area, grey-box
visuals, no procedural generation — build, scavenge, bash the occasional raider, and endure.

## Status

**Prototype — all 7 planned milestones implemented and through one round of human playtesting.**
Built with heavy AI assistance (Claude), verified to compile and run cleanly via headless Unity
batch-mode automation after every change. First playtest found a door-hinge bug (fixed), a
confusing survival-stat pacing issue (tuned, and death now has a real visible consequence
instead of being a silent no-op), and asked for more visual variety and a bigger map (done —
see Features below). See [`DESIGN_NOTES.md`](DESIGN_NOTES.md) for the full build log and every
design decision made along the way, and [`SETUP.md`](SETUP.md) for a milestone-by-milestone
playtest checklist.

## Gameplay loop

1. **Build** — construct and expand your bunker, room by room.
2. **Trigger the apocalypse** — a point-of-no-return button inside the bunker. Once you press
   it, survival pressure begins for real.
3. **Survive** — hunger, thirst, and health tick down continuously from that point on.
4. **Scavenge** — venture out through the bunker's doorway to gather Wood and Scrap Metal,
   fighting off (or fleeing) the raiders patrolling outside.
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

Combat is one melee weapon and one swing — raiders have health and die, but there are no
ranged weapons, no blocking, and no hit reactions. No day/night cycle, weather, or procedural
generation. Building and crafting aren't locked to the post-apocalypse
phase (you can keep building any time). All deliberate scope decisions for a first prototype,
not oversights — see `DESIGN_NOTES.md` for the reasoning behind each one.
