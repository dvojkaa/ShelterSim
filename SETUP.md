# SheltreSim — Unity Setup

Unity 6000.6.8f1 is already installed on this machine and the full project (scene,
prefabs, generated assets) is committed and verified via headless batch mode — 0 compile
errors, 0 exceptions, all milestones' setup tools (M1 through M15) run clean end to end.
What's **not** verified is actual Play Mode gameplay (movement feel, HUD rendering, a real
save/load round-trip) — that needs a human. This file is for that pass.

## Quick start

1. Open the project in Unity Hub (point it at this folder) or directly via:
   ```
   /home/dvojkaa/Unity/Hub/Editor/6000.6.8f1/Editor/Unity -projectpath /home/dvojkaa/git/ShelterSim
   ```
2. Wait for scripts to compile (Console should show 0 errors — if Unity prompts about the
   Input System backend, accept/restart).
3. The scene (`Assets/Scenes/M1_BunkerShell.unity`) already has every milestone wired up — just
   open that scene and press Play.
4. **If you ever want to regenerate everything from scratch (e.g. after wiping the scene), the
   easiest way is one menu item: `Tools > SheltreSim > ▶ START GAME (build everything + Play)`.**
   It runs every milestone's setup tool in the right order and then enters Play mode for you —
   no need to click through M1 through M12 individually. (Only click this from the regular
   Editor window, not headlessly — forcing Play mode under `-batchmode -nographics` hangs, see
   the hard-won lessons in `CLAUDE.md`.) If you only want to rebuild without also pressing Play,
   `Tools > SheltreSim > ...` still has each milestone's tool individually, or run the whole
   chain without Play via:
   ```
   /home/dvojkaa/Unity/Hub/Editor/6000.6.8f1/Editor/Unity -batchmode -nographics \
     -projectPath /home/dvojkaa/git/ShelterSim -executeMethod SheltreSim.EditorTools.BatchSetupAll.RunAll -quit
   ```

## Full playtest checklist (press Play and work through this)

**Movement & interaction (M1)**
- WASD + mouse look moves/rotates the player; cursor is locked.
- Walk through the bunker's south doorway gap.
- Looking at the bunker door logs an interact prompt; **E** toggles it open/closed — it should
  now swing out to the side on a hinge rather than just spinning in place (that was a real bug,
  fixed after the first playtest — it used to still block the gap once "opened").

**Building (M2)**
- **B** toggles build mode. A ghost piece follows your look direction, snapped to a 2m grid.
- Scroll wheel cycles through **7 pieces**: Wall Segment, Floor Tile, Workbench, Bed, Storage
  Crate, Water Collector, Generator. **R** rotates 90°.
- Ghost tints green (clear) / red (overlapping something). Left-click places when green.
- **Escape** exits build mode.
- Bed heals at a food/water cost; Workbench crafts blades; Water Collector produces water.
  Storage Crate and Generator remain decorative: no storage capacity or power system is designed.
- Pieces now rest ON the surface you're aiming at rather than sinking half-way into it —
  the ghost is offset up by half its height. (An older note here warned that a placed Floor
  Tile sinks into the floor; that was fixed along with the "everything's always red" bug.)

**Survival & save (M3)**
- Three HUD bars top-left: health (red), hunger (brown), thirst (blue). Their *passive* decay
  is **frozen** until you trigger the apocalypse (see M7 below) — intentional, not a bug. The
  health bar can still drop before then if a raider hits you.
- **F5** saves (Console logs the file path), **F9** loads (player position, stats, placed
  pieces, and inventory all restore).
- Worth testing once: die, then press **F9** on a save taken while alive. You should get
  control back. (Until the second review you did not — health came back but the player stayed
  frozen forever.)

**Outdoor area & gathering (M4)**
- Beyond the doorway: a 40×36m wasteland (ground + 3 boundary walls so you can't walk off the
  edge), 4 cylinder "Wood" nodes, 4 sphere "Scrap Metal" nodes, and ~9 small cube "rubble" props
  scattered around for visual variety (purely decorative, not interactable).
- Looking at a node shows a gather prompt with remaining charges; **E** gathers it (logs the
  new inventory total). After 3 gathers it logs "Depleted" and disappears.

**Crafting (M5)**
- Wall Segment: 2 Wood. Floor Tile: 1 Scrap Metal. Workbench: 1 Wood + 1 Scrap Metal. Bed: 2
  Wood. Storage Crate: 1 Wood. Water Collector: 2 Scrap Metal. Generator: 3 Scrap Metal + 1
  Wood. Try building with an empty inventory — it should log "Cannot afford" and refuse; gather
  first, then it should work and the resource count should drop.

**Enemies (M6, collision + door-breaking added in M12)**
- 4 capsule "Raider" enemies patrol the outdoor area, spread across the larger map (plus 8 more
  scattered through the M10 expanded wasteland as of M12). Get within
  ~10m and they should turn and approach; within ~1.5m they stop and attack on a cooldown
  (Console logs each hit, and your health HUD bar drops immediately — raider damage is NOT
  gated on the apocalypse having started; only the *passive* hunger/thirst/health decay is.
  An older version of this note claimed otherwise and was wrong). You can fight back with
  left-click — see M8 below; avoiding or fleeing is still the cheaper option. If health
  reaches 0, you should actually freeze (movement/interaction disabled, cursor unlocked) —
  this used to be silent (Console-log only), now it's a real, visible consequence, and as of
  the second review it works before the apocalypse trigger too (it previously did not: the
  death check sat behind the "decay paused" gate, so a raider could grind you to 0 HP during
  the build phase and nothing at all would happen).
- **New in M12 — please specifically test this:** raiders now have real collision (a
  `CharacterController`, same as the player) instead of walking straight through walls/buildings/
  cars, and they no longer give up chasing just because you got some distance — provoke one, run
  home, and close the bunker door. It should walk up to the closed door and **force it open**
  (you'll see it swing open, plus a "was forced open!" message) and keep coming, instead of
  stopping dead outside. There's no real pathfinding, so approaching from an angle that doesn't
  line up with the doorway may leave a raider sliding along the outside wall for a while before
  it works its way to the gap — expected simple-AI behavior, not a crash, but worth confirming
  it doesn't get *permanently* stuck.
- **The bunker doorway and the ruined buildings' doorways are both wider than they used to be
  (1.2m -> 1.8m), as of the M12 QC pass.** A raider's collision capsule is 1.0m across (~1.16m
  once Unity's skin width is counted) against a player that is only 0.7m, and the old 1.2m gap
  measured only 1.04m of actual clearance with the bunker door swung open — i.e. the raider
  physically could not fit through the one entrance it was being sent to. It measures 1.65m now.
  If the bunker entrance looks too wide to you, say so — it's a geometry call, easy to retune,
  but don't narrow it below ~1.4m without re-measuring, or door-breaking silently stops working.
- One of the 12 ruined buildings (`Building_10`) has a loot crate sitting right inside its
  doorway, which a raider can't step over (0.4m crate vs a 0.3m step height). You can walk
  around it; a raider chasing you into *that* building will bump it. Measured, left alone —
  fixing it means re-rolling the loot seed and re-checking all 38 crate placements.

**Apocalypse trigger & full loop (M7)**
- A red button on a wall-mounted panel ("ApocalypseButton") sits inside the bunker near the north wall.
  Look at it, press **E** — Console logs "Sirens wail in the distance...", then ~2s later
  "The ground shakes. It's begun.", then ~1s later survival ticking starts for real (HUD bars
  begin draining — thirst empties in about 4 minutes, hunger in 5.5 minutes,
  with roughly 6 minutes total survival if you do nothing).
- Pressing **E** on it again afterward should be a harmless no-op (logs "Already triggered"
  via the prompt, or a warning if interacted with anyway).
- Let health reach 0 (wait it out, or let a raider finish you off) and confirm you actually
  freeze — this is a real fix, previously death did nothing visible at all.

**Menus, inventory, combat (M8)**
- **Escape** (outside build mode) opens a Pause menu with Resume/Quit — cursor unlocks, time
  freezes (`Time.timeScale = 0`). Click Resume or press Escape again to close. While it's open
  the view should stay still as you move the mouse to the button, clicking Resume should not
  also swing the weapon, and **B** should not drop you into build mode behind the panel — all
  three were live bugs until the second review.
- **Escape while in build mode** should only leave build mode; it must not also pop the pause
  menu open in the same keypress (both maps bind Escape — the hand-off is explicit now).
- **I** toggles an Inventory panel listing Wood/Scrap Metal counts, live-updating.
- **Left-click** (outside build mode) swings a melee weapon — anything with `EnemyAI` within
  ~2.5m in front of you takes damage (30 HP per raider, 20 damage per hit — 2 hits kills one).
  There's a simple procedural swing animation (no character rig exists, so it's a primitive
  prop rotating forward and back, not a skeletal animation).
  **Please specifically check all three of these** — the detection sweep has been reworked
  twice during review (see the 2026-11-07 entries in DESIGN_NOTES.md) and only the first case
  below has ever worked:
  1. a raider at arm's length;
  2. a raider pressed right up against you (point-blank);
  3. a swing while you are standing close to a wall, crate or bit of rubble — the second
     review found that any scenery merely *beside* you (within the sweep's 0.5m radius)
     silently swallowed every swing, because Unity reports an already-overlapping collider at
     distance 0 and it therefore always won the "nearest hit" test.
  A wall genuinely *between* you and a raider should still block the swing — that part is
  intended.
- The starting bunker room now has a Bed, Storage Crate, and Workbench in it from the start.

**Resources, food & crafting reference (M9)**
- The outdoor area now also has Trees (trunk + foliage, give Wood — extra nodes alongside
  M4's original cylinders), Stone and Iron Ore clusters (rock piles, grey vs. rust-colored),
  and Food Bushes (a conifer shape with small blue berry spheres) scattered around, all at
  least 3m clear of the original M4 nodes/rubble.
- Gathering a Food Bush gives Blueberries. Open the Inventory (**I**) — each food resource row
  (Blueberries) has an **Eat** button on the right. Clicking it consumes one and restores 30
  hunger — **please verify this actually moves the hunger bar**, since batch-mode verification
  cannot click UI buttons and this path has not been exercised in a real Play session yet.
- **The inventory panel is now modal, like the pause menu** (QC fix — the Eat button was
  literally unclickable before it, because the cursor stays locked to the centre of the screen
  during normal play). While it's open: the game is frozen, the mouse cursor is free, and
  looking/moving/swinging/entering build mode are all suppressed. **I** or **Escape** closes it
  again and re-locks the cursor. Worth confirming by hand: the cursor appears when you press
  **I**, the Eat button highlights under it and responds to a click, and closing the panel
  hands control cleanly back to the game (view doesn't jump, movement works again).
- **I** does nothing while you're in build mode — that's deliberate (build mode owns left-click
  for placement). Leave build mode with **B**/**Escape** first.
- The Inventory panel now also has a **Recipes** section below the resource list: every build
  piece with its real resource cost (e.g. "Wall Segment — 2 Wood"), greyed out when you can't
  currently afford it. This is reference-only — it does not place anything itself; placing
  still happens through build mode (**B**) same as before.
- Letting *hunger* run out should now be meaningfully avoidable by scavenging Food Bushes —
  worth checking that enough bushes exist within reasonable walking distance of the bunker
  doorway. As of M10, thirst has the same lever: Water is a lootable resource with its own
  **Drink** button in the inventory (see the M10 section below) — eating and drinking together
  should now be able to meaningfully extend survival, whereas through M9 only hunger could be
  addressed and thirst (which decays faster) was an unavoidable timer. Worth confirming by hand
  that scavenging both actually keeps you alive longer than doing nothing.

**Expanded wasteland & looting (M10)**
- Walk far enough past the original outdoor area (beyond where the M4 zone's far boundary used
  to be) and the ground should continue, uninterrupted, into a much larger zone (~24,000m²) —
  there should be no wall or gap at the seam between the two areas.
- This new zone has 12 small building shells (simple rooms with an open doorway gap, no actual
  door) and 15 cars scattered around it, at varied sizes/colors/rotations.
- Each building has 1-3 small crate props standing on its floor, and each car has exactly one
  on the ground **behind its rear bumper** — look at one and press **E** to search it. A crate
  should give either one or two resources (Wood/Scrap Metal/Stone/Iron Ore/Blueberries/Water/a
  First Aid Kit/a weapon upgrade, in bigger stacks than before — 2-5 of a raw material/food/water
  instead of 1-3) or come up empty ("Searched — found nothing") — about 85% should have
  something as of the M12 loot rebalance (playtester feedback: 10 crates only yielding 2 Water
  felt too stingy). Searching the same crate twice should log "Already searched" the second
  time, not give loot again.
- **Check the car crates specifically.** Through the first M10 build every one of them was
  buried inside the car body — invisible and impossible to search, 15 of the 38 crates dead
  (the QC pass moved the marker behind the bumper and added a construction-time check that
  fails loudly if any crate ends up inside another object). They should now be plainly visible
  sitting on the ground at the back of each car, and searchable from any side.
- Crates sit **on** the ground/floor now rather than hovering at chest height; if you see one
  floating, something regressed.
- Open the inventory (**I**): Water should show a **Drink** button (restores thirst) and First
  Aid Kit a **Use** button (restores health) — same style as Blueberries' existing Eat button. A
  weapon-upgrade item should show an **Equip** button; using it should permanently increase
  your melee damage (check via the "Hit ... for N damage" Console log before/after equipping —
  N should go up) and consume the item.
- **A searched crate stays searched for the rest of the session but not beyond it.** Saving
  (**F5**) and loading (**F9**) does *not* un-search crates, so there's no infinite-loot
  save-scum — but it also doesn't remember them, so loading a save taken *before* you looted a
  crate restores the older inventory while the crate stays empty: that loot is gone for good.
  Quitting and reopening the project resets every crate (same as resource nodes always have).
  Known gap, see `DESIGN_NOTES.md`.
- A looted **weapon upgrade now survives save/load** — equip it, press F5, press F9, and the
  "Hit ... for N damage" log should still show the upgraded N. (Before the QC pass it showed
  the upgraded N *and* handed the item back, so repeating F5/Equip/F9 stacked damage forever.)
- **The far half of the new zone may not be survivable to reach and return from.** It's ~240m
  from the bunker doorway to the far wall; you walk at 4 m/s (7 sprinting) and a do-nothing
  player dies about 80 seconds after triggering the apocalypse. Take Water and Blueberries with
  you, and tell the project owner if the distance feels wrong — it's a balance call, not a bug.
- **As of M12, the new zone has resource nodes (trees/stone/iron/food) and 8 more raiders
  scattered through it**, alongside the buildings/cars — it's no longer empty, pressure-free
  scavenging. See the Biome blend section below.
- **This entire new zone's layout — building/car positions, sizes, colors, and what each crate
  contains — is seeded/deterministic, not re-rolled per session.** Re-running
  `Tools > SheltreSim > Setup M10 Map Expansion` / `Setup M10 Consumables And Loot` reproduces
  the exact same layout and loot every time; it will only look different if the project owner
  deliberately changes a seed constant in the source. This is a first for the project (every
  earlier milestone's placement was 100% hand-picked literal coordinates) — worth confirming
  it doesn't feel janky or repetitive before treating it as settled.

**Workbench & Water Collector (M11)**
- Place or find a Workbench (the bunker starts with one). Look at it, press **E**: with at
  least 2 Scrap Metal and 2 Wood in your inventory, it should log a "Crafted 1 Makeshift Blade"
  style message and add a Makeshift Blade to your inventory — check the count goes up. Without
  enough materials it should log "Cannot afford" and not consume anything. Unlike resource nodes
  or loot crates, the Workbench should be usable repeatedly — crafting again immediately after
  (with enough materials) should work again, not say "already used."
- Place a Water Collector (2 Scrap Metal). Looking at it logs "Collecting water... (none ready
  yet)" immediately after placing; keep looking at it and after roughly 30 real seconds the
  prompt should **update on its own** to "Press E to collect 1 Water", then 2, 3… capping at 5.
  (The prompt refreshing while you stand still is a QC fix from this pass — before it, the text
  froze at whatever it said when you first looked, so the collector appeared permanently empty.
  As of M12 this prompt also shows on screen, just below the crosshair, not just in the Console.
  The on-screen line is blank whenever nothing is in range — only the Console says "No
  interactable in range", deliberately, so the HUD isn't carrying a permanent line of text.)
  Pressing **E** should add that much Water to your inventory and reset it to accumulating from
  zero. This is a simple timer, not real weather/rain — it accumulates indoors, outdoors, or
  anywhere else.
- **Check the placed Water Collector is standing on the floor, not sunk into it.** It used to be
  (Unity's Cylinder primitive is 2 units tall, not 1, so it was buried 0.6m); the fix is
  geometry-only and unverifiable without eyes on it. It should be a waist-high 1.2m barrel
  resting on the surface, and the red/green ghost should turn red when its *top* overlaps
  something, not just its bottom half.
- **With the pause menu (Escape) or the inventory panel (I) open, pressing E should now do
  nothing.** It previously still fired at whatever the frozen view was pointing at — crafting,
  draining a collector, or even tripping M7's one-way apocalypse trigger from behind the panel.
- Expect roughly 2.5 minutes for one collector to fill all 5. One collector on its own does
  **not** out-pace thirst (see the balance note in Known scope gaps) — build two if you want to
  actually live off it.

**One-click start, biome blend, on-screen HUD (M12)**
- `Tools > SheltreSim > ▶ START GAME (build everything + Play)` should build the entire chain
  and drop you into a running game in one click — the only thing worth checking by hand is that
  it actually does both (builds *and* presses Play), since this only runs correctly from the
  regular Editor window.
- Walk into the M10 expanded wasteland (past the original outdoor area) and you should now find
  trees, stone/iron ore clusters, and food bushes scattered among the buildings and cars, plus
  raiders patrolling the zone — it should feel like one continuous, mixed environment, not
  "nature near the bunker, then a separate empty city zone."
- You should now see, on screen (not just in the Console): a small crosshair at the center of
  the screen, an interaction prompt just below it ("Press E to open", "Press E to search", etc.)
  that updates live, and a small feed of the last few events (e.g. "Found 3 Wood, 2 Water", "Hit
  Raider_1 for 20 damage") in the bottom-left that each fade out a few seconds after appearing.
  **This is the most worth checking by hand of everything in this section** — batch mode can only
  prove it compiles and the GameObjects get built, not that the text is readable, positioned
  sensibly, doesn't overlap the three stat bars, or that the message feed's timing feels right.
- **Open the inventory (Tab) with the HUD up and click an Eat / Drink / Heal / Equip button.**
  The new HUD graphics are the first ones this game has ever drawn in the middle of the screen,
  and the inventory panel is centred there too — the M12 QC pass took the GraphicRaycaster off
  the HUD canvas and cleared `raycastTarget` on every HUD graphic so an invisible 600x32 prompt
  label can't swallow a click aimed at a button underneath it. Worth one click to confirm.
- Loot crates should feel noticeably more generous than before — see the M10 section above for
  the specific numbers. With the current seed every single crate has something in it; if you
  want the occasional empty one back, that's a seed change, not a bug fix.
- Raiders should chase you through a closed bunker door instead of stopping outside it — see the
  Enemies section above.

## Known scope gaps (by design, not oversights — see DESIGN_NOTES.md)

- Combat is one melee weapon and one swing (upgradeable once via a looted weapon item, or
  crafted at a Workbench) — no ranged weapons, blocking, or hit reactions.
- No day/night cycle or weather — the Water Collector's "collecting rainwater" is a flat timer,
  not an actual weather simulation.
- The Recipes list in the Inventory panel is read-only — it can't place pieces itself.
- M10's buildings/cars/loot are "randomly generated" only in the sense of a seeded, one-time
  editor-tool roll — not runtime randomness.
- Which crates you've already searched is not written to the save file (nor is resource-node
  depletion, nor a Water Collector's pending water, nor the current game phase) — it persists
  for the session but resets when you reopen the project. Fixing it needs stable per-object IDs
  in the save format.
- **One Water Collector does not out-pace thirst.** Thirst empties in ~59s and one Water
  restores ~21s of it, so a collector's 1-per-30s covers about 70% of the drain. Two clear it
  easily. Deliberately left as-is — it gives the build system a real survival purpose — but it's
  a tuning call the project owner may want to revisit.
- **Re-run the whole setup chain, not individual Tools-menu items.** `Setup M2 Build System`
  rewrites the build-piece prefabs from scratch, which silently strips M11's Workbench/Water
  Collector behaviour back off them (and stacks a duplicate BuildSystem and duplicate starting
  furniture). The batch chain is ordered correctly and always starts from a fresh scene; running
  one tool by hand is not.
- Nothing scales the survival clock to the expanded wasteland's size (M12 added enemies/resources
  to it, but the far end is still a long walk relative to how fast you starve) — worth revisiting
  if the big map becomes the main play space.
- Raiders have real collision as of M12 but no pathfinding — a raider whose straight line to you
  doesn't pass through a doorway can get stuck sliding along a wall rather than finding its way
  around. Deliberate (this project has no NavMesh anywhere), worth confirming it doesn't read as
  "broken" in practice.
- Building/crafting isn't gated to the Survival phase — you can keep building after the
  apocalypse too (deliberate restraint, not a missed requirement).
- Render pipeline ended up Built-in, not URP as originally planned — doesn't matter visually
  for grey-box primitives; code was made pipeline-agnostic rather than converting the project.

## Latest regression and playtest pass

Run `Tools > SheltreSim > Verify Current Game` for scene wiring, recipe accounting and physical
loot-visibility checks. For a clean rebuild in a disposable project copy, batch method
`SheltreSim.EditorTools.ReleaseVerification.RebuildAndVerify` runs the full construction chain
and checks it. `SheltreSim.EditorTools.ReleaseVerification.BuildLinuxPlayer` builds the Linux
player from the current scene. Scene regeneration overwrites generated content: preserve your
hand edits or work in a copy first.

The Linux executable accepts `-batchmode -nographics --smoke-test` for a finite runtime regression
run using temporary save snapshots. Check for `[RuntimeSmokeTest] ALL CHECKS PASSED` and exit 0.
This uses the standalone player; it does not force the Editor into headless Play Mode.

Human checks after this pass:

- Start a fresh session and follow the HUD guidance through gathering, building and apocalypse.
- Pause, open inventory, die, load and restart in different orders. Exactly one modal should own
  the cursor, and loading a live save should restore movement and hide the death screen.
- Search two crates in the same ruin, save, search another, then load: each crate should restore
  its own searched state. Repeat load twice and confirm furniture is not duplicated.
- Save a nearly regrown node and partially filled collector; loading should preserve progress.
- Try sleeping at full health, with insufficient food/water, and near a live raider. Rejected
  rests must not spend resources or grant health.
- Build near walls and surface edges; move the camera just before clicking. Red means blocked,
  yellow means insufficient materials, green means clear and affordable. Ghost collectors must
  never accumulate usable water.
- Fight at point blank, with an enemy behind you, and with a wall between you. Provoke a raider
  around a ruin and back to the bunker door. Confirm attacks do not pass through solid walls.
- Kill a raider, stay near its spawn and wait: it must not materialize inside the player or a
  newly built wall. When it returns, its appearance and combat settings should match.
- Check HUD/inventory legibility at 1280×720 and 1920×1080 and confirm consumable buttons remain
  reachable after scrolling. Check the standalone executable as well as Editor Play Mode.

## M16 art pass: what a human should check

Automated checks passed: scene colliders are compared before and after the art pass, the release
checks ran, and the built Linux player passed its smoke test with and without graphics. Screenshots
come from `SheltreSim.EditorTools.ArtCapture.RebuildVerifyAndCapture` (`-batchmode` WITHOUT
`-nographics`, written to `ArtSource/previews/ingame_*.png`).

`./Builds/Linux/SheltreSim.x86_64 -batchmode --art-probe <folder> -logFile <path>` (also without
`-nographics`) checks the art in the running player and must log
`[ArtRuntimeProbe] ALL CHECKS PASSED` and exit 0. It covers raiders walking with real AI movement,
swinging legs, feet on the ground, damage flash, a killed raider respawning with the same look and
behaviour, all five node types disappearing and returning, the door model swinging, and shaders
surviving into the build (screenshots in `<folder>`). It caps itself at 60 fps: above about 2500 fps,
which an uncapped batch run reaches, per-frame raider moves fall below
`CharacterController.minMoveDistance` and are dropped, so raiders stand still. Normal launches use the
Ultra quality level with vSync on.

None of that is a real play session, so check:

- Raiders walk with swinging legs and stop swinging when they stand still. Feet stay on the ground.
- Hit a raider: it flashes white for a moment, then returns to its normal look.
- Raider weapon stays in the hand during the attack swing.
- First-person club looks right while idle and swinging, including next to a wall.
- Harvest each node type to depletion: the whole model disappears, then returns on regrow.
- Aim at the lower part of a scrap pile. Its collider is the old floating sphere (y 0.4-1.2), so a
  ray at the very bottom can miss it. Report this if it feels wrong.
- Build mode: all 7 pieces preview green/red/yellow, place at the expected height, and walls and
  floors line up edge to edge. Bed, Workbench, Water Collector and Crate still work when placed.
- Walk the ruins and around the cars: nothing visible should stop you, and nothing should stop you
  where no model is. Trees keep their old wide collider, which is noticeably wider than the new
  thinner trunk.
- Frame rate on the far ruins with fog, and whether the fog distance feels right.
- An old save should still load, since IDs are unchanged. Building_10's crate in its doorway is
  still there (known from M12).

To rebuild one asset: `ArtSource/build.sh ArtSource/assets/<id>.py`, then re-run the setup chain.
Blender lives at `/mnt/Apps/blender-5.2.3-linux-x64/blender`; adjust `ArtSource/build.sh` elsewhere.
