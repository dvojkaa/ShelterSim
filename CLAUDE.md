# Working on SheltreSim

SheltreSim is a first-person bunker-builder + survival game (Fallout-inspired, much smaller
scope), built almost entirely by Claude with a human playtester giving iterative feedback
between rounds. Read [`DESIGN_NOTES.md`](DESIGN_NOTES.md) for the full build log and every
bug found/fixed so far — it's the authoritative history, continuously updated, and several
entries document lessons that matter for *how* to work in this repo, not just what's in it.
[`README.md`](README.md) is the public feature overview; [`SETUP.md`](SETUP.md) is the
milestone-by-milestone human playtest checklist.

The user communicates in Czech, casually and tersely, and often runs sessions unattended
("I'll leave it here, continue until tokens run out") — when given that kind of standing
instruction, keep looping (implement → verify → fix → document → push → pick up the next
request) without stopping to ask permission at each step. Keep your own replies short.

## The loop this project uses for every new feature

This is "agentní způsob" (agent mode) — the user's standing instruction, given early on and
never rescinded, to keep building this way:

1. **Scope the request into independent chunks.** Read the existing code yourself first
   (the relevant scripts, the editor tool that builds the scene area you're touching,
   `EditorMaterialUtility`, etc.) so you can write forks a *precise* brief, not a vague one.
   Do small, highly-coupled, or foundational edits yourself rather than via a fork when a
   file has already been touched by several previous passes — re-deriving a now-complex
   file's structure from scratch is itself a source of bugs, and it's cheaper for whoever
   already just re-read it to make the next small change directly.

2. **Launch parallel sub-agents for the independent chunks**, via the `Agent` tool,
   `subagent_type: "general-purpose"` (not `"fork"` — a fork inherits this conversation's
   context, which a feature-building sub-agent doesn't need and shouldn't rely on). Each
   fork starts with **zero context** — brief it like a new hire:
   - State the exact playtester feedback that motivated the task (verbatim, translated).
   - Give it a **narrow, explicit file-ownership scope**: "create exactly this one new file"
     or "modify only this file" — and say so explicitly, because forks have violated scope
     before (see lessons below).
   - Point it at the exact existing files to read for conventions (the closest analogous
     component, the shared color/material helper, the interface it implements) — don't make
     it search for them.
   - Tell it how to verify itself before reporting done (the batch command below) and tell
     it to quote the actual relevant log lines in its report, not just assert success.
   - Tell it not to `git commit`/`push` — that happens once, later, after review.
   - Launch independent forks **in the same message** (parallel tool calls) when their file
     scopes don't overlap.

3. **Don't trust fork completion summaries as proof of work.** Read the actual files on
   disk yourself before building on top of them or wiring them together. This project has
   had a fork fabricate a "3 files created" report with zero actual tool calls behind it,
   and a different fork overwrite a parallel fork's already-correct file after being told
   explicitly not to touch it — both caught by checking files directly, not by trusting the
   summary.

4. **Wire the shared/integration files yourself** (e.g. `BatchSetupAll.cs`'s call order,
   `M8MenusSetup.cs`'s field wiring) rather than through a fork — these are exactly the
   files multiple forks' work has to agree about, and having one person do that wiring
   avoids concurrent-edit collisions.

5. **Verify via headless Unity batch mode** after every change and again after wiring:
   ```
   /home/vojta/Unity/Hub/Editor/6000.6.8f1/Editor/Unity -batchmode -nographics \
     -projectPath /home/vojta/git/SheltreSim \
     -executeMethod SheltreSim.EditorTools.BatchSetupAll.RunAll -quit -logFile <path>
   ```
   Grep the log for `error CS` and `Exception` — both must be absent. **This only proves
   compilation and editor-time construction.** It has repeatedly passed cleanly over real,
   serious runtime bugs — see lessons below. Don't treat a clean run as proof of gameplay
   correctness, and don't attempt real Play Mode automation (see lessons below).

6. **Update `README.md`, `SETUP.md`, and append a dated entry to `DESIGN_NOTES.md`**
   describing what was built, what was verified vs. not, and anything flagged but
   deliberately left unfixed (balance calls, scope gaps) — follow the existing entries'
   level of detail, they're meant to let a future session (or a fresh window) pick up
   without re-deriving context.

7. **Launch a final skeptical review pass**, also via `Agent`, `subagent_type:
   "general-purpose"`, **`model: "opus"`** (this override only works on a non-`"fork"`
   subagent type — forks always run on the parent model regardless). This agent is
   pre-authorized to commit and push once satisfied — don't make it ask permission. Brief
   it to:
   - Read `DESIGN_NOTES.md` in full, especially the last few entries (the bug *category*
     this project keeps finding: lifecycle/init ordering, misread Unity API semantics, two
     systems each half-managing shared state, a new UI control that compiles fine but is
     physically unusable at runtime).
   - Read every file in the diff, skeptically, plus whatever it calls into for context —
     actual current file contents, not the task summary.
   - Fix real bugs directly, re-verify via the batch command, update the docs above if its
     fixes change what a human should check.
   - `git add` deliberately (check `git status`, don't blindly `git add -A`), commit with
     `git -c user.name="vojta" -c user.email="vojta@localhost" commit -m "..."` (no global
     git identity is configured in this sandbox), and `git push origin main`.
   - Report back what it found, what it fixed, what it deliberately left unfixed (and why),
     and the resulting commit hash.

## Hard-won lessons (don't re-learn these)

- **Headless batch mode proves compilation + editor-time construction only.** Real bugs
  that passed it cleanly: an enemy's health initialized to 0 due to assignment-order; a
  death check gated behind the wrong flag so a player could be reduced to 0 HP and stay
  alive-but-frozen; a melee hit-detection rewrite that was itself broken by a follow-up
  review's "fix"; an inventory button that was completely unclickable because nothing
  freed the locked FPS cursor. All found only by reading logic skeptically, not by batch
  mode. See `DESIGN_NOTES.md`'s dated QC-pass entries for the full stories.
- **Real Play Mode automation (`-batchmode -nographics` + forcing `EditorApplication.isPlaying
  = true`) hangs indefinitely in this sandbox.** Proven once, cost real time to notice and
  kill. Don't retry it.
- **"Random" in this project means seeded `System.Random` with a literal seed constant,
  rolled once at editor-tool-run time — never `UnityEngine.Random`, never anything that
  changes at runtime.** Every milestone through M9 used 100% hand-picked literal coordinates;
  M10 introduced seeded placement specifically because the user asked for "randomly
  generated" content, and the resolution was "deterministic and reproducible," not "chaotic."
  Keep that distinction if asked for more randomized content later.
- **No global git identity in this sandbox** — every commit needs
  `-c user.name="vojta" -c user.email="vojta@localhost"`.
- Unity: 6000.6.8f1, Built-in Render Pipeline (not URP, despite early planning — discovered
  during setup, not worth converting). New Input System, every `InputActionMap` hand-built
  in code (no `.inputactions` asset anywhere) — match this pattern for any new input.
- Single-scene architecture is deliberate (no `DontDestroyOnLoad`/scene-transition
  complexity) — don't introduce a second scene without discussing it first.
- `CharacterController` is itself a `Collider` subclass. Converting `EnemyAI` from raw
  `transform.position` writes to `CharacterController.Move` (so raiders actually collide with
  walls/buildings/cars instead of clipping through them) needed zero changes to
  `MeleeAttacker`'s `SphereCastAll`-based hit detection — a `CharacterController` satisfies
  `GetComponentInParent<EnemyAI>()` exactly like the `CapsuleCollider` it replaced. Remove the
  default collider `CreatePrimitive` adds rather than keeping both on the same GameObject.
- Player-facing one-off feedback (loot results, hit/miss, craft results) goes through
  `SheltreSim.UI.GameMessageBus.Post(string)`, not a bare `Debug.Log` — it logs to the Console
  internally *and* surfaces on `SurvivalHUD`'s on-screen message feed from one call site. Added
  after an Opus-model review flagged that most milestones' player feedback was Console-only and
  therefore invisible during normal play (nobody plays with the Console open). Route new
  player-facing messages through it rather than introducing a second `Debug.Log` convention.
- **Giving an actor a collider changes which geometry it can fit through — go measure the gaps,
  don't assume the ones the player uses are wide enough.** When `EnemyAI` got a
  `CharacterController` (radius 0.5, i.e. 1.0m across, ~1.16m once its 0.08 `skinWidth` is
  counted), every doorway in the game was still sized for the 0.7m player: the bunker's 1.2m
  gap measured **1.04m** of real clearance with the door swung open, so "raiders can come in
  through the door" was false while every line of door-breaking code was correct. Both
  `M1SceneBuilder.DoorGap` and `M10MapExpansionSetup.BuildingDoorGap` are 1.8m now. The way
  this was found is the way all three of this category have been found (M10's buried crates,
  M11's sunk Water Collector, this): a throwaway `Assets/Editor/_TempProbe.cs` that runs real
  `Physics.OverlapCapsule`/`SphereCastAll` queries against the batch-built scene and is deleted
  afterwards. Reason on paper about geometry and you will be wrong; sweep it and you won't.
- **A passive HUD canvas must not carry a `GraphicRaycaster`, and HUD graphics must set
  `raycastTarget = false`.** Unity defaults it to true, and the moment a HUD draws anything in
  the middle of the screen (crosshair, interact prompt) it sits on top of whatever modal panel
  shares that space — same `sortingOrder 0`, incidental winner. That is the same bug as M9's
  unclickable Eat button, one canvas over.
- **Art goes through `ArtSource/` and `M16ArtPass`, never into the milestone builders.** M10/M12 derive
  placement from seeded draws and renderer bounds, so a visual edit there moves the map. New asset:
  add a script per `ArtSource/ASSET_SPEC.md`, map it in `M16ArtPass`. Only one process can hold the
  Unity project, so art agents must run Blender only.
- **You can see the game.** `-batchmode` without `-nographics` renders on the real GPU:
  `-executeMethod SheltreSim.EditorTools.ArtCapture.RebuildVerifyAndCapture`, then read
  `ArtSource/previews/ingame_*.png` at full size. Don't use `montage` sheets to judge gradients: it
  bands them. This still isn't Play Mode, and the Play Mode hang lesson above stands. On this machine
  the project is at `/home/dvojkaa/git/ShelterSim` and Unity under `/home/dvojkaa/Unity/Hub/Editor/`.
