# M22 Blender asset handoff — GPT art / Claude runtime

Status: 58 MODEL ASSETS COMPLETE,2026-11-09. Existing ASSET_SPEC.md remains authoritative for style/palette/export.
User assigned GPT new models; Claude retains runtime/gameplay, collider construction and import mapping.
No Unity runs by art workers. Shared atlas, library, existing assets, audio and game code remain untouched.

## Files and conventions

Each ID produces `ArtSource/assets/<id>.py`, `Assets/Art/Models/<id>.fbx`, and
`ArtSource/previews/<id>.png`. Blender sources are reproducible Python scripts, matching the existing
project pipeline. Default is metres, floor-centre origin, front-BlenderY. Characters face+BlenderY.
Viewmodel weapons use grip origin, barrel+BlenderZ, matching the existing player_weapon convention;
runtime attachment must explicitly account for this orientation.

An exported visual is not a collider. Open frames/windows/doors must keep the opening empty and
Claude's runtime colliders should follow that opening. Character sizes below are envelopes for
integration, not a promise that the original raider capsule will fit every new boss.

## First wave ownership

| Worker | Model | IDs |
|---|---|---|
| art_construction | gpt-6-sol / medium | piece_foundation, piece_wall_half, piece_wall_window, piece_door_frame, piece_door_wood, piece_door_steel, piece_roof_flat, piece_roof_slope, piece_ramp, piece_stairs, piece_fence, piece_gate, piece_barricade, piece_palisade |
| art_machines | gpt-6-sol / medium | piece_armory, piece_console, piece_radio_console, piece_solar, piece_battery, piece_cooler, piece_charger, piece_pump, piece_irrigator, piece_fabricator, piece_repair |
| art_firearms | gpt-6-sol / medium | weapon_pistol, weapon_smg, weapon_shotgun, weapon_hunting_rifle, weapon_assault_rifle, weapon_laser_pistol, weapon_laser_rifle, weapon_plasma_rifle |

All 58 source scripts, FBX exports, Unity `.meta` files and previews are present. See `M22_ASSET_IDS.json` and
`M22_MODEL_REPORT.json` for the complete inventory and measured source-mesh bounds/pivots.
The Blender source validator reported 58/58 assets with UVs, finite vertices and no bound failures.
Both construction openings remain clear in the report's 391-ray checks. `.blend` snapshots for
all 58 are in `ArtSource/blend/`. All FBX metadata GUIDs are unique; existing model importer
settings were reused for new files.

## Delivered second wave

Defence: piece_sandbag, piece_spike_trap, piece_shock_trap, piece_turret_mg, piece_turret_laser, piece_alarm.
Loot: loot_bag, loot_boss_cache, item_fuel_can, ammo_box_pistol, ammo_box_rifle, ammo_box_shell, energy_cell.
Turret exports contain a separate child mesh named exactly `Head`, with pivot at z=1m and barrels
facing +BlenderY. Runtime can yaw this child around its pivot.

## Delivered enemies and bosses

Humanoids: enemy_wanderer, enemy_runner, enemy_scavenger, enemy_screamer, enemy_plated, enemy_breacher.
Creatures: enemy_rootwalker, enemy_mirelurker, enemy_iron_boar.
Bosses: boss_gatekeeper, boss_root_colossus, boss_shift_commander.
Humanoids and humanoid bosses use separate mesh parts with raider-compatible `Torso`, `Head`,
`Arm_L`, `Arm_R`, `Leg_L`, and `Leg_R` names; their pivots are measured in the model report.
Creatures use separately pivoted torso/head/limb parts (`Foreleg_*`/`Hindleg_*` for quadrupeds).
These are modular mesh pivots, not skeletal rigs; no animation clips or gameplay integration are
included. The root colossus is about 2.79m wide × 1.16m deep × 3.98m tall and needs a dedicated
large-enemy collider/animation setup. Humanoid bounds vary by equipment; use the report for exact
envelopes. All models face +BlenderY and stand at floor Z=0.

All 58 delivered IDs have source `.py`, `.fbx`, `.fbx.meta` and `.png` preview files. The report records actual
silhouette bounds, which can exceed intended simple collider envelopes by a few millimetres on
some small details; notably piece_sandbag's front/back extents are about 0.67m against a nominal
0.60m depth. Please account for this if enforcing tight collision bounds. No Unity integration or
gameplay collider setup was performed by GPT. No existing raider, crate, generator or other model
was replaced by this round.
