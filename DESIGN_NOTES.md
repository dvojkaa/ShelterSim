# SheltreSim — Working Design Notes (for Claude, cross-session reference)

> Purpose of this file: persistent context so any future session (or me, resumed) can pick up this project without re-deriving decisions. Update it as things change — treat it as the source of truth over whatever is in my head from a prior conversation.

## Elevator pitch

First-person bunker-builder + survival game. Small scale, Fallout-inspired but far smaller in scope. Player builds out a bunker before an apocalypse event, then survives inside it and makes supply runs into one outdoor area.

## Core loop (MVP target)

1. **Build phase** — player constructs/expands a bunker (multiple rooms) before the apocalypse trigger.
2. **Apocalypse event** — scripted trigger, forces player into the bunker.
3. **Survive phase** — manage hunger/thirst/health inside the bunker over time.
4. **Scavenge phase** — venture into one outdoor map to gather resources, encounter simple threats, return before survival stats run out.
5. Loop 3–4 repeats; crafting turns raw resources into usable items/upgrades.

Scope is explicitly: **bunker with multiple rooms + one outdoor map + basic crafting + simple enemies/hazards.** Not an open world, not procedural generation, not multiplayer.

## Dev context

- Developer: experienced C++ engineer (ROS2 background), has shipped small personal Unity projects before. Not a beginner — skip beginner-level explanations, go straight to architecture/implementation.
- Engine: **Unity**, C#.
- Art: **low-poly / primitive shapes** — no custom 3D asset pipeline needed for MVP. Grey-boxing is the final look for v1, not just a placeholder.
- Perspective: first-person.

## Confirmed decisions

- Render pipeline: **URP**.
- Save/load: **core requirement**, not deferred — build it in from the start, not bolted on later.
- Enemies: **simple AI** (raiders/mutants) — basic movement, player detection, attack. Not deep combat, but a real moving threat, not just environmental hazards.
- Working style: **longer agentic sessions per milestone** — I implement a larger chunk (e.g. the whole build system) in one go, user reviews at the end of the milestone rather than after every small step. Implication: be extra careful to keep scope tight and self-consistent within a milestone, since there's no mid-flight checkpoint. Flag assumptions/decisions made along the way in the session summary so they're reviewable even without a diff-by-diff walkthrough.
- Input: **Unity New Input System**.
- Target platform: **Linux only for now** — user doesn't own a Windows machine. Don't add Windows-specific code paths or assume Windows is a target; avoid platform-specific APIs that aren't cross-platform in Unity. Revisit if this changes.
- Milestone order confirmed as drafted below (M1 → M7), no reordering.

## Reference games / mood board

- **Fallout Shelter** (mobile) — bunker management/building style reference, though SheltreSim is 3D first-person rather than top-down management.
- **Sheltered** — similar bunker-survival framing.
- **60 Seconds!** — tone/style reference for the bunker-survival setup.
- **Cities: Skylines** — reference for *how building/placement should feel* (grid placement, zoning-like feedback), not for scale — adapt the feel, not the scope.
- **Fallout (mainline)** — reference for outdoor world feel, enemies, and NPCs/factions flavor (long-term inspiration; NPCs/factions are explicitly out of scope for v1, see below).

## Assumptions not yet confirmed with user (flag these, don't silently lock in)

- No multiplayer/co-op — single player only.
- No plan (yet) to publish on Steam — treat as a personal/hobby project unless stated otherwise. This affects how much polish vs. working-prototype quality matters.

## System breakdown (for scoping/estimating work, not final architecture)

| System | MVP scope | Notes |
|---|---|---|
| Player controller | FPS movement, camera, interact raycast | Standard Unity FPS controller |
| Building system | Grid-based room/wall placement, snapping, resource cost per piece | Needs a placement-preview + validity-check pattern |
| Survival stats | Hunger, thirst, health (maybe sleep) ticking over time | Simple timers + UI bars |
| Inventory & crafting | Slot-based inventory, basic recipes (raw → usable item) | No deep crafting tree for v1 |
| Resource gathering | Interactable objects in the outdoor map, simple loot | |
| Outdoor map | One hand-built (not procedural) scene | Small enough to hand-author with primitives |
| Apocalypse trigger | Scripted cutscene/event, one-time state transition | Not a system, just a sequencer |
| Enemies/hazards | Simple AI (raiders/mutants) — movement, detection, attack; not deep combat | Keep minimal for MVP; expand later |
| Save/load | Core requirement, build in from the start | JSON or Unity's serialization; scope TBD |
| UI | HUD (stats), inventory screen, build menu | |

## Explicitly out of scope for v1

- Procedural world generation
- Multiplayer
- Deep crafting trees / tech trees
- NPC factions, dialogue, quests
- Custom 3D assets / animation pipeline

## Cost context (from earlier discussion, Claude API pricing as of 2026-11)

Rough token/cost estimate for AI-assisted development at this "small but complete loop" scope, using Claude Sonnet 5 ($2/$10 per MTok intro pricing through 2026-08-31, then $3/$15):

- Estimated code volume: ~5,000–9,000 lines for a tight MVP of this scope.
- Estimated total token spend across the whole build (all sessions, debugging, iteration): ~8–20M tokens.
- Estimated cost: roughly **$35–120**, heavily dependent on how well-scoped prompts/sessions are — sloppy iteration can push this 3–5x higher.
- Does not cover art assets (not needed here — low-poly primitives) or Unity-specific learning curve time.

## Open questions for the user (ask before/while building, don't assume silently)

- [ ] None outstanding right now — all initial planning questions resolved as of 2026-11-06. Re-open this section if new ambiguity comes up during a milestone.

## Milestones (confirmed order — M1 → M7, no reordering)

Given the "longer session per milestone" working style, each milestone should be a self-contained, testable chunk:

1. **M1 — Player & bunker shell**: FPS controller, a hand-built starting bunker room (no building system yet), basic interact raycast. Goal: can walk around a static bunker.
2. **M2 — Building system**: grid placement, snapping, room/wall pieces, resource cost gating (resources can be stubbed/free for now). Goal: can expand the bunker.
3. **M3 — Survival stats + save/load**: hunger/thirst/health ticking, HUD bars, save/load of player + bunker state. Goal: can leave and resume a session with state intact.
4. **M4 — Outdoor map + resource gathering**: one hand-built outdoor scene, interactable resource nodes, inventory. Goal: can scavenge and bring resources back.
5. **M5 — Crafting**: basic recipes turning raw resources into usable items/building materials.
6. **M6 — Enemies**: simple raider/mutant AI in the outdoor map — detection, movement, attack.
7. **M7 — Apocalypse trigger + full loop glue**: scripted intro event, wiring build-phase → apocalypse → survive/scavenge loop together.

## Status log

- 2026-11-06: Initial design notes created. No code written yet. No git repo initialized in this folder yet.
- 2026-11-06: All initial planning questions resolved — engine/pipeline/input/platform/enemies/save-load/working-style/references/milestone order all confirmed. Ready to start M1 once git repo is initialized.
- 2026-11-06: M1 code written (player controller + input, interaction system, editor scene builder). No Unity Editor or dotnet SDK available in the dev sandbox that wrote this — code has NOT been compiled or run. See `SETUP.md` for the exact steps to open this in Unity and verify M1 actually works before trusting it.
- 2026-11-06: M2 code written (grid snapping, build-mode input map, piece definitions + resource stub, ghost-preview/placement controller, editor setup tool). Also not compiled/run — same caveat as M1.
- 2026-11-06: M3 code written (survival stats ticking + HUD bars, JSON save/load of player position + stats + placed pieces, editor setup tool). Caught and fixed one cross-milestone integration bug myself during review: the M2 ghost preview is instantiated from the same prefab as a real placed piece, so without an explicit fix it would have self-registered as a "placed piece" in the save registry while merely being previewed — `BuildModeController` now disables the `PlacedPiece` component on ghost instances. Still not compiled/run anywhere — user will verify M1+M2+M3 together in Unity Editor. See `SETUP.md` for all setup/test steps (1-9). Next milestone once verified: M4 — outdoor map + resource gathering.
