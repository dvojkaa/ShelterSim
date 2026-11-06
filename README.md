# SheltreSim

A small first-person bunker-builder and survival game. Build out a bunker, trigger the
apocalypse when you're ready, then survive inside it while scavenging resources from the
irradiated world outside — all before the raiders find you.

Think *Fallout* scaled down to a weekend-sized project: one hand-built outdoor area, grey-box
visuals, no procedural generation, no combat — just build, scavenge, and endure.

## Status

**Prototype — all 7 planned milestones implemented.** Built with heavy AI assistance (Claude),
verified to compile and run cleanly via headless Unity batch-mode automation, but **not yet
playtested by a human**. See [`DESIGN_NOTES.md`](DESIGN_NOTES.md) for the full build log and
every design decision made along the way, and [`SETUP.md`](SETUP.md) for a milestone-by-milestone
playtest checklist.

## Gameplay loop

1. **Build** — construct and expand your bunker, room by room.
2. **Trigger the apocalypse** — a point-of-no-return button inside the bunker. Once you press
   it, survival pressure begins for real.
3. **Survive** — hunger, thirst, and health tick down continuously from that point on.
4. **Scavenge** — venture out through the bunker's doorway to gather Wood and Scrap Metal,
   while avoiding (or fleeing) the raiders patrolling outside.
5. **Craft** — spend gathered resources to build more of your bunker. Repeat.

## Features

| Milestone | What it adds |
|---|---|
| M1 | First-person movement & look, interaction system (raycast + prompt), a starter bunker room |
| M2 | Grid-snapped building system — place walls and floors with a live ghost preview |
| M3 | Hunger/thirst/health, an on-screen HUD, and JSON save/load (F5 / F9) |
| M4 | An outdoor area beyond the bunker doorway, with gatherable Wood and Scrap Metal nodes |
| M5 | Crafting — building pieces cost real resources, drawn from your inventory |
| M6 | Raiders — simple AI that detects, chases, and attacks on sight (no combat yet — avoid or flee) |
| M7 | The apocalypse trigger itself, tying the whole loop together |

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

No weapon/combat system — raiders are pure pressure, not (yet) defeatable. No day/night cycle,
weather, or procedural generation. Building and crafting aren't locked to the post-apocalypse
phase (you can keep building any time). All deliberate scope decisions for a first prototype,
not oversights — see `DESIGN_NOTES.md` for the reasoning behind each one.
