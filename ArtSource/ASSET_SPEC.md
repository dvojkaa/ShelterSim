# SheltreSim art spec (Blender pipeline)

Style: stylised **low-poly post-apocalyptic** (faceted flat shading, palette-atlas colour, chunky readable
silhouettes, wear, rust, grime). Think Synty-style clarity, not realism. Every asset is original: no logos,
brand names, or recognisable designs from existing games or real vehicles. Must read clearly from 5-15 m
in first person (eye height 1.6 m). Sensible detail: props 200-1500 tris, cars up to 2500, raider up to 1500.

## Coordinates
- Metres. Blender X = Unity X, Blender Y = Unity Z, Blender Z = Unity Y (up). Export handles conversion (verified).
- Origin: floor centre (z=0 is the ground) unless the asset says otherwise.
- "Front" = -Y unless the asset says otherwise.
- Nothing may exceed the asset's bounds: pass them to `finish(m, bounds=...)`, which fails the build otherwise.

## How to write an asset
`ArtSource/assets/<asset_id>.py`, one per asset (variants may share a helper inside your own files only):

```python
from ss_art import Model, finish
m = Model('piece_generator', seed=11)   # name == asset id == FBX filename
m.box((sx,sy,sz), (x,y,z), 'mustard', bevel=0.02, rot=(rx,ry,rz), taper=(tx,ty))  # centred at loc, rot in degrees
m.cyl(r, h, (x,y,z), 'steel', rot=(..), seg=10, r2=None)   # along local Z, r2=top radius for cones
m.tube([(x,y,z),...], r, 'black', seg=6)                   # pipe through points
m.blob((sx,sy,sz), (x,y,z), 'stone', rough=0.18, subdiv=1, colors=['stone','stone_dark'], flat_bottom=None)  # faceted lump
m.poly([(x,z),...], thickness, (x,y,z), 'concrete', rot=(..))   # extruded XZ polygon, CCW: jagged edges, silhouettes
finish(m, bounds={'x':(-0.6,0.6),'y':(-0.6,0.6),'z':(0,1.2)})
```
Read `ArtSource/lib/ss_art.py` (palette names in PALETTE) and the example `ArtSource/assets/piece_generator.py`.
Colours must be palette names. **Do not edit the library or atlas**: if you need a helper, define it in your own script;
`from ss_art import PALETTE` etc. is fine.

Build: `ArtSource/build.sh ArtSource/assets/<id>.py` (run from repo root; takes ~10 s). Output: `Assets/Art/Models/<id>.fbx`
and a 4-view sheet `ArtSource/previews/<id>.png` (front 3/4, back 3/4, side, top). **Look at the preview with the Read tool and
iterate until it actually looks good**, at least 2 looks per asset. Fix floating parts, gaps, interpenetration that shows,
inverted looks, unreadable silhouettes. Quality is the job: a recognisable, characterful object, not a box with stripes.

Parts: `p = m.part('Name', pivot=(x,y,z))` then `p.box(...)` in the same world coordinates makes a separately-movable child
object pivoting there. Use parts ONLY where this spec requests them.

Rules: don't run Unity. Don't touch anything outside your own `ArtSource/assets/<id>.py` files and their build outputs.
Don't git commit. Determinism: only seeded `Model(seed=)`/`random.Random(literal)`, no time-based randomness.

## Asset list
(bounds as x / y / z ranges; a value like 2.0 means -1.0..1.0)

### Build pieces (player-placed, must fill most of their footprint so the collider feels honest)
- `piece_wall`: x 2.0, y 0.2, z 0..3. Scrap/concrete-frame wall panel, tiles side by side seamlessly at x edges.
- `piece_floor`: x 2.0, y 2.0, z 0..0.2. Plank/steel floor tile, tiles in both directions.
- `piece_workbench`: x 1.5, y 0.8, z 0..1. Bench with vice, tools, front at -Y.
- `piece_bed`: x 1.0, y 2.0, z 0..0.5. Salvaged metal cot with stained mattress, blanket; pillow toward -Y.
- `piece_crate`: x 1.0, y 1.0, z 0..1. Big storage crate/footlocker with straps.
- `piece_water_collector`: x 0.8, y 0.8, z 0..1.2. Barrel with funnel/tarp catcher, tap at -Y.
- `piece_generator`: done, it's the example.
- `loot_crate`: x 0.4, y 0.4, z 0..0.4. Small battered lootable box, must read as container from 10 m.

### Bunker
- `door_leaf`: x 1.6, y 0.1, z 0..2.2. Heavy riveted steel/wood bunker door, small window or wheel, hinge side at x=-0.8.
- `door_frame`: fits the fixed 1.8 m wide doorway in a 0.2 m thick wall. Posts occupy only |x| 0.78..1.0; lintel z 2.2..3.0;
  y within -0.15..0.15, z 0..3. **Opening |x|<0.78, z<2.2 must stay empty.** Add hazard stripes/bolts.
- `apocalypse_button`: origin at the button's centre, NOT floor. x 0.7, z -0.35..0.35, y -0.1..0.3. Wall-mounted big red mushroom
  button, in a dark panel mounted on the wall at y=+0.3, button faces -Y, hazard stripes, warning plate. Must look irresistibly dangerous.
- `bunker_lamp`: origin at the ceiling attach point; z -0.5..0, x 0.6, y 0.6. Caged hanging lamp, uses `lamp_warm` bulb.
- `bunker_pipes`: wall dressing with origin at wall surface floor; x 6.0, y -0.25..0 (wall at y=0, protrudes -Y), z 0..3. Pipes, valve wheel, junction boxes, conduit; keep clear of z<1.9 except a corner riser at x<-2.6.
- `bunker_shelf`: wall-mounted shelf origin at wall floor: x 1.4, y -0.35..0, z 1.2..2.3. Must leave z<1.2 empty so it never visually blocks walking. Cans, jars, a radio.

### Ruins and world dressing (no colliders at runtime)
- `ruin_window`: origin at wall surface, centre: x 1.3, y -0.1..0, z -0.7..0.7. Boarded-up/broken window frame applied onto a wall.
- `ruin_parapet_a`, `ruin_parapet_b`: top-of-wall broken masonry: origin at wall-top centre, x 2.0, y 0.4, z -0.5..0.5, jagged silhouette, exposed brick and rebar.
- `ruin_doorway`: frame for a 1.8 m opening in 0.2 m wall: posts |x| 0.86..1.05, lintel z 2.2..3.0, y 0.2 wide range -0.14..0.14, z 0..3; opening clear. Damaged look.
- `ruin_debris_a`, `ruin_debris_b`: x 1.2, y 1.2, z 0..0.5. Brick and plank piles lying on ground.
- `boundary_post`: x 0.6, y 0.6, z 0..3.6. Rusty barrier post with barbed wire arms and hazard sign (blank sign/no text); dresses the perimeter wall every few metres.
- `rubble_a`, `rubble_b`, `rubble_c`: normalised 1.0 x 1.0 x 1.0, origin floor centre, z 0..1. Three broken concrete chunks (one with rebar). Stretched non-uniformly at runtime, so they fill their box and stay chunky.
- `rubble_slab`: x 1.0, y 1.0, z 0..1. Flat-ish shattered concrete slab, fills the box footprint, will be squashed to low height at runtime.

### Nature and resources (harvestable)
- `tree_a`, `tree_b`, `tree_c`: x 3.6, y 3.6, z 0..4.8. Hardy wasteland trees: gnarled trunk 0.3-0.4 m wide on the base, faceted canopies. One lush-ish, one sparse/dry, one half-dead. Trunk centred on origin.
- `dead_log_a`, `dead_log_b`: x 1.2, y 1.2, z 0..3.0. Standing dead snag/chopped trunk with broken branches and a smaller stump beside; readable as "wood here".
- `food_bush_a`, `food_bush_b`: x 1.2, y 1.2, z 0..1.8. Small spruce-like conical bush covered with obvious blue berries (`berry_blue`, `berry_light`) you can spot from 10 m.
- `rock_stone_a`, `rock_stone_b`: x 1.6, y 1.5, z 0..0.95. Grey boulder clusters, flat bottom, 2-4 chunks.
- `ore_iron_a`, `ore_iron_b`: x 1.6, y 1.5, z 0..0.95. Same size, rusty-brown rock with glinting `ore_metal` veins/nuggets, clearly different from plain stone.
- `scrap_pile_a`, `scrap_pile_b`: x 1.3, y 1.3, z 0..1.15. Heaps of bent metal sheets, pipes, gears, a tyre. Must read as "metal to salvage".

### Cars (solid colliders exist at runtime; the model should fill this envelope)
Nose at **+Y**, rear at -Y. x -1.0..1.0, y -2.25..2.25, z 0..2.25. Wheels radius 0.35 at x ±0.95, y ±1.4 (tyres, some flat). Body z 0.25..1.65 approx, cabin above.
Wrecks: rust patches, smashed glass, missing panels. Generic, invented 1950s-ish rounded retro feel, no real make or badge.
- `car_sedan_red`, `car_sedan_blue`, `car_sedan_green`, `car_sedan_grey`: same shape, main paint `car_red`/`car_blue`/`car_green`/`car_grey`.
- `car_pickup_red`, `car_pickup_blue`, `car_pickup_green`, `car_pickup_grey`: a pickup with open bed. Use one shared helper script.

### Characters and weapons
- `raider`: origin at FEET centre; x 1.0, y 1.0, z 0..2.0; faces **+Y** (Unity forward). Height ~1.9 m. Masked scavenger in patched leathers, spiked shoulder pad, red rag, clearly hostile.
  Exactly these separately pivoted parts (a static `raider` default part may be left empty): `Torso` (pivot 0,0,1.0, includes pelvis/chest), `Head` (pivot 0,0,1.55), `Arm_L` (pivot -0.28,0,1.45), `Arm_R` (pivot 0.28,0,1.45), `Leg_L` (pivot -0.12,0,0.95), `Leg_R` (pivot 0.12,0,0.95). Limbs hang straight down at rest (arm hand ends about z 0.75), so rotating about X at a pivot swings them. Right arm is posed slightly forward/raised to hold a weapon near (0.32, 0.25, 1.05). No gaps at the joints when rotated ±30 degrees: overlap geometry generously.
- `raider_weapon`: origin at grip; length along +Y; x 0.25, y -0.3..0.9, z ±0.2. Spiked pipe or nail-board club.
- `player_weapon`: first-person viewmodel. Origin at grip middle; length along **+Z**; x ±0.08, y ±0.08, z -0.28..0.6. Baseball-bat-like scavenger club wrapped with tape and a few nails, handle at -Z. Close to the camera: can be a little more detailed. It's seen from behind/above.
