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

- Render pipeline: **URP was the plan, but the actual created project turned out to be Built-in Render Pipeline** (discovered 2026-11-06 during first real Unity verification — `GraphicsSettings.asset` has no SRP assigned, `Packages/manifest.json` has no `com.unity.render-pipelines.universal`). Not worth converting for a grey-box/primitive-shapes prototype — code was patched to be pipeline-agnostic instead (`BuildModeController.TintGhost` now sets both `_Color` and `_BaseColor`). Revisit only if visual fidelity needs ever go beyond solid-color primitives.
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
- **Architecture decision made autonomously (M4):** the outdoor map is NOT a separate Unity scene — it's built as an extension of the same `M1_BunkerShell.unity` scene, directly beyond the bunker's doorway. Reasoning: a real scene-transition architecture needs persistent state across scene loads (`DontDestroyOnLoad`, or a save/reload-on-transition scheme) which is a meaningfully bigger, riskier piece of plumbing to get right with no Unity Editor available to test it. A single scene sidesteps that entirely — nothing unloads, so the player/stats/inventory/placed pieces just keep existing. Revisit this if the outdoor area grows large enough that having it all loaded at once becomes a real performance problem (unlikely at this project's scale).

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
- 2026-11-06: M3 code written (survival stats ticking + HUD bars, JSON save/load of player position + stats + placed pieces, editor setup tool). Caught and fixed one cross-milestone integration bug myself during review: the M2 ghost preview is instantiated from the same prefab as a real placed piece, so without an explicit fix it would have self-registered as a "placed piece" in the save registry while merely being previewed — `BuildModeController` now disables the `PlacedPiece` component on ghost instances.
- 2026-11-06: M4 code written (resource definitions, inventory, gatherable resource nodes using the existing M1 `IInteractable` system, editor tool extending the M1 scene with an outdoor area). Also wired inventory into the M3 save/load system myself (`SaveData`/`SaveManager` gained an inventory section — optional, degrades gracefully if M3 wasn't set up first). Architecture note: outdoor area is the *same* Unity scene as the bunker, not a separate one — see the "Architecture decision made autonomously (M4)" entry above.
- 2026-11-06: **First real Unity verification.** Unity Hub + Editor 6000.6.8f1 got installed locally; found the project is Built-in Render Pipeline (not URP as planned — too minor to fix, code patched to be pipeline-agnostic instead). Ran the whole M1-M4 stack through headless batch mode (`-batchmode -nographics -executeMethod`) via a new `Assets/Editor/BatchSetupAll.cs` helper that chains all four milestones' setup tools. Found and fixed 3 real bugs that manual review had missed: (1) Active Input Handling was stuck on legacy-only — switched to "Both"; (2) `Packages/manifest.json` was missing `com.unity.ugui` (the `UnityEngine.UI` namespace `SurvivalHUD` needs); (3) `InputActionMap.AddAction`'s parameter is actually named `expectedControlLayout`, not `expectedControlType` as three call sites guessed. After fixes: 0 compile errors, 0 exceptions, full M1→M4 setup chain runs clean end to end. Committed the full Unity project (ProjectSettings/Packages/.meta files) for the first time, with a proper `.gitignore` (Library/Logs/UserSettings/.idea excluded). **Caveat: this verifies compilation and editor-time construction/wiring only — actual Play Mode gameplay (movement feel, HUD rendering, a real save/load round trip) has not been tested and still needs a human or a deeper automated test.** Next milestone: M5 — crafting (turn Wood/Scrap Metal into build-piece resource costs, replacing `ResourceStub`), followed by M6 (enemies) and M7 (apocalypse trigger + full loop glue) — continuing autonomously, running the same headless batch-mode verification after each.
- 2026-11-06: M5 done (wrote directly rather than via forks, given the small scope — `CraftingCost`/`CraftingSystem` replace `ResourceStub`; `BuildPieceDefinition.resourceCost` (always 0) replaced with a real `craftingCost` recipe list; Wall Segment now costs 2 Wood, Floor Tile costs 1 Scrap Metal). Caught one compile error on the first batch run myself (`M2BuildSystemSetup.cs` still referenced the removed `resourceCost` field) — fixed, reverified, 0 errors, full M1→M5 chain clean. User's earlier instruction: keep using the fork/agent pattern for ongoing milestones (not just do everything directly) — reverting to that for M6.
- 2026-11-06: M6 done via 2 parallel forks (`EnemyAI` — simple detect/chase/attack state machine, no NavMesh, direct `Vector3.MoveTowards`; `M6EnemySetup` editor tool spawning 2 "Raider" capsules in the outdoor area). Explicit scope guard honored: enemies have no health/death — the player cannot fight back yet (no weapon system exists or is planned in the roadmap), it's a pure avoid/flee pressure mechanic. Clean on the first batch-mode verification — 0 errors, 0 exceptions, full M1→M6 chain runs end to end.
- 2026-11-06: **M7 done — all 7 originally-planned milestones now complete.** `GameStateManager` (Building/Apocalypse/Survival phase enum; pauses `SurvivalStats.decayPaused` during Building so the construction phase is pressure-free) + `ApocalypseTrigger` (an `IInteractable` button inside the bunker — one-way, re-triggering is a no-op) written via 1 fork; the small `SurvivalStats.decayPaused` field and the `M7ApocalypseSetup` editor tool I wrote directly. Interacting with the button starts a 3-second scripted sequence (two log beats: "Sirens wail..." then "The ground shakes. It's begun.") and then unpauses decay, handing control to the Survival phase — this is the "full loop glue" the milestone asked for: build phase, with free play before the player chooses to commit, into permanent survival pressure after. Clean on the first batch-mode verification — 0 errors, 0 exceptions, **the complete M1→M7 chain runs end to end in one pass.**
  - **What this does NOT verify**: actual Play Mode gameplay — movement feel, HUD rendering, a real save/load round-trip, whether the apocalypse coroutine's `WaitForSeconds` actually fires correctly over real frames, whether enemy detection/chase feels right. Batch-mode `-executeMethod` only proves the editor-time construction and wiring code runs without throwing. A human needs to actually press Play and move around before trusting this is *fun*, only that it *compiles and assembles*.
  - **Known scope gaps, honest about what's NOT here**: no weapon/combat system (enemies are unbeatable pressure, by design per M6's scope note); outdoor area has no day/night cycle or weather; no procedural variety (every resource node/enemy spawn position is hand-placed, per the original "no procedural generation" scope decision); Building-phase building/crafting is not actually gated to only the Building phase (you can keep building after the apocalypse too — this was a deliberate scope-restraint call, not an oversight, see M7's commit).
- 2026-11-06: **First human playtest — 3 bugs/gaps reported, all fixed same session.**
  1. **Door bug (real, confirmed root cause):** `InteractableDoor` rotated the door's own transform around its own center, so "opening" just spun a 1m×0.1m slab into a 0.1m×1m slab at the same spot — still fully blocking the gap. Fixed in `M1SceneBuilder`: the door mesh is now a child of a `DoorHinge` empty positioned at the gap's edge, offset by half the door's width; rotating the hinge correctly swings the mesh out of the doorway. `InteractableDoor.doorVisual` now points at the hinge, not the mesh.
  2. **"I don't lose health / don't die" (partly expected behavior, partly a real gap):** stats are frozen until the apocalypse trigger is pressed (already documented in SETUP.md, but easy to miss on a short test — and the original decay rates took *minutes* before health visibly moved even after triggering, reading as broken). Tuned `SurvivalStats` decay rates up (hunger 0.5→1.1/s, thirst 0.8→1.7/s, starvation health drain 2→5/s) so a do-nothing player sees health move within ~1.5-2 minutes of triggering. Separately, death itself was genuinely a no-op from the player's perspective — only a `Debug.Log`. Added a real (if minimal) consequence: `SurvivalStats.Die()` now disables `FirstPersonController` and `PlayerInteractor`, freezing the player (FirstPersonController's own `OnDisable` also unlocks the cursor as a visible signal). Exposed `SurvivalStats.IsDead` and had `EnemyAI` stop attacking once it's true. No respawn/game-over screen — that's further than a bug-fix pass should go.
  3. **"Add graphics, expand the map"** — both done:
     - New `Assets/Editor/EditorMaterialUtility.cs` helper (`Apply(GameObject, Color)`) — still flat-colored primitives per the project's art direction, just not all the same grey/white anymore. Applied across M1 (floor/walls/door), M2 (all build pieces), M4 (ground/boundary/resources/rubble), M6 (raiders), M7 (button).
     - M4's outdoor area grew from 24×20m to **40×36m**, resource nodes from 2+2 to **4 Wood + 4 Scrap Metal**, added 9 decorative rubble props for visual variety, raiders from 2 to 4 (repositioned to fit the bigger space) — done via 2 parallel forks (map expansion; build-menu additions) plus 1 more (crafting costs for the new pieces), per the user's explicit request to keep using the fork/agent workflow.
     - **Build menu expanded from 2 pieces to 7**: Wall Segment, Floor Tile, Workbench, Bed, Storage Crate, Water Collector, Generator. All 5 new ones are placeable decoration only — no unique gameplay function (no sleep mechanic, no crafting-at-station requirement, no real power generation). `BuildModeController` needed zero changes to support this — it already cycles generically through whatever's in `availablePieces`, which is why this was pure editor-tool/data work, not a new system.
  - Verified via the same headless batch-mode method as every prior milestone: 0 compile errors, 0 exceptions, full M1→M7 chain runs clean with all of the above in place.
  - **Still not verified**: actual Play Mode feel (is the new decay pacing actually fun, does the bigger map feel empty or busy, does the door swing read correctly to a human eye) — construction-only verification can't tell us that.
