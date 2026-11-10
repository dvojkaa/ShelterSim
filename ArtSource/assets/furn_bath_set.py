import math, random
from ss_art import Model, finish
m = Model('furn_bath_set', seed=63)
rng = random.Random(6303)
# --- Tub along +Y wall: x -1.1..0.45, y 0.3..0.85, rim z 0.55
TX0, TX1, TY0, TY1 = -1.1, 0.45, 0.3, 0.85
cx, cy = (TX0 + TX1) / 2, (TY0 + TY1) / 2
tw, td = TX1 - TX0, TY1 - TY0
# tiled enclosure: front + end panels as individual tiles (some cracked/missing)
tile_cols = ['paint_white', 'plaster', 'paint_white', 'fabric_white', 'paint_white', 'plaster']
n = 7
tw_each = tw / n
for i in range(n):
    x = TX0 + tw_each * (i + 0.5)
    for k in range(2):
        z = 0.14 + 0.28 * k
        c = rng.choice(tile_cols)
        if (i, k) in ((2, 0), (5, 1)): c = 'concrete'         # fallen tile, exposed backing
        if (i, k) in ((3, 1), (1, 0)): c = 'plaster_dirty'    # grimy
        m.box((tw_each - 0.015, 0.03, 0.26), (x, TY0 + 0.015, z + 0.0), c, bevel=0)
m.box((0.03, td - 0.03, 0.55), (TX0 + 0.015, cy, 0.275), 'paint_white', bevel=0.005)
# crack lines (thin dark slivers on tile face)
for (x, z, rot) in ((-0.55, 0.3, 40), (-0.2, 0.2, -25), (0.15, 0.42, 65)):
    m.box((0.2, 0.006, 0.012), (x, TY0 - 0.003, z), 'gunmetal', rot=(0, rot, 0))
m.box((0.035, 0.012, 0.012), (-0.5, TY0 - 0.003, 0.36), 'gunmetal', rot=(0, -50, 0))
# body core + rim
m.box((tw - 0.06, td - 0.06, 0.5), (cx + 0.0, cy + 0.0, 0.25), 'paint_white', bevel=0)
m.box((tw, 0.07, 0.06), (cx, TY0 + 0.035, 0.57), 'fabric_white', bevel=0.015)
m.box((tw, 0.07, 0.06), (cx, TY1 - 0.035, 0.57), 'fabric_white', bevel=0.015)
m.box((0.07, td, 0.06), (TX0 + 0.035, cy, 0.57), 'fabric_white', bevel=0.015)
m.box((0.07, td, 0.06), (TX1 - 0.035, cy, 0.57), 'fabric_white', bevel=0.015)
# dirty interior
m.box((tw - 0.18, td - 0.16, 0.02), (cx, cy, 0.56), 'plaster_dirty')
m.box((0.5, 0.2, 0.021), (cx - 0.2, cy, 0.565), 'dirt', rot=(0, 0, 15))
m.box((0.12, 0.12, 0.022), (TX1 - 0.25, cy, 0.566), 'water')         # puddle
m.cyl(0.04, 0.025, (TX0 + 0.2, cy, 0.57), 'steel_dark', seg=8)        # drain
for x in (TX0 + 0.12, TX1 - 0.12):
    for y in (TY0 + 0.1, TY1 - 0.1):
        m.cyl(0.05, 0.03, (x, y, 0.015), 'rust_dark', seg=6)
# tiled splash panel on the +Y wall above the tub, broken top edge
m.poly([(TX0, 0), (TX1 + 0.05, 0), (TX1 + 0.05, 0.38), (0.0, 0.5), (-0.3, 0.38), (-0.55, 0.52), (-0.85, 0.36), (TX0, 0.42)],
       0.03, (0, TY1 - 0.015 + 0.0, 0.58), 'paint_white', rot=(90, 0, 0)) if False else None
for i in range(4):
    x = -0.92 + 1.2 * i / 3
    h = 0.3 + 0.12 * (i % 3 == 1) - 0.1 * (i == 2)
    m.box((0.32, 0.02, h), (x, TY1 - 0.01, 0.6 + h / 2), rng.choice(['paint_white', 'plaster', 'paint_white', 'fabric_white']))
m.box((0.2, 0.012, 0.012), (-0.3, TY1 - 0.026, 0.8), 'gunmetal', rot=(0, 35, 0))
# rusty taps + spout
m.cyl(0.03, 0.12, (-0.1, TY1 - 0.06, 0.84), 'rust', rot=(90, 0, 0), seg=6)
m.cyl(0.02, 0.2, (-0.1, TY1 - 0.15, 0.75), 'rust_dark', rot=(0, 0, 0), seg=6) if False else None
m.tube([(-0.1, TY1 - 0.03, 0.8), (-0.1, TY1 - 0.18, 0.8), (-0.1, TY1 - 0.24, 0.74)], 0.018, 'rust', seg=6)
for x in (-0.28, 0.08):
    m.cyl(0.025, 0.05, (x, TY1 - 0.04, 0.8), 'rust_light', rot=(90, 0, 0), seg=6)
    m.cyl(0.04, 0.02, (x, TY1 - 0.075, 0.8), 'chrome' if x < 0 else 'rust_dark', rot=(90, 0, 0), seg=6)
# --- Toilet against +X wall (faces -X)
tx, ty = 0.82, 0.38
m.cyl(0.14, 0.26, (tx - 0.02, ty, 0.13), 'paint_white', seg=8)                    # pedestal
m.box((0.5, 0.36, 0.1), (tx - 0.12, ty, 0.3), 'paint_white', bevel=0.03, taper=(0.75, 1.0))   # bowl
m.box((0.34, 0.34, 0.03), (tx - 0.14, ty, 0.355), 'fabric_white', bevel=0.01)     # seat
m.box((0.24, 0.24, 0.022), (tx - 0.14, ty, 0.37), 'dirt')                        # stained bowl
m.box((0.14, 0.38, 0.42), (tx + 0.2, ty, 0.5), 'paint_white', bevel=0.02)         # cistern
m.box((0.13, 0.4, 0.04), (tx + 0.19, ty, 0.73), 'fabric_stain', rot=(0, 0, 3))  # chipped lid
m.cyl(0.02, 0.04, (tx + 0.2, ty - 0.1, 0.77), 'rust', seg=6)                      # flush knob
m.box((0.05, 0.015, 0.2), (tx + 0.1, ty + 0.15, 0.42), 'steel_dark')
m.box((0.2, 0.012, 0.012), (tx + 0.2, ty + 0.1, 0.6), 'gunmetal', rot=(0, 90, 0)) if False else None
# --- Basin on short pedestal against +X wall, y -0.5
bx, by = 0.88, -0.45
m.cyl(0.07, 0.62, (bx, by, 0.31), 'fabric_white', r2=0.09, seg=6)
m.cyl(0.1, 0.04, (bx, by, 0.02), 'concrete_dark', seg=6)
m.box((0.3, 0.5, 0.14), (bx - 0.02, by, 0.68), 'paint_white', bevel=0.03, taper=(0.8, 1.0))
m.box((0.2, 0.38, 0.02), (bx - 0.04, by, 0.76), 'plaster_dirty')
m.box((0.18, 0.03, 0.012), (bx - 0.04, by + 0.05, 0.771), 'dirt', rot=(0, 0, 40))
m.box((0.04, 0.5, 0.2), (bx + 0.12, by, 0.82), 'paint_white', bevel=0.01)       # backsplash
m.cyl(0.025, 0.06, (bx + 0.08, by, 0.86), 'rust', seg=6)
m.tube([(bx + 0.1, by, 0.86), (bx + 0.0, by, 0.92), (bx - 0.05, by, 0.88)], 0.014, 'rust', seg=6)
m.box((0.012, 0.3, 0.012), (bx + 0.141, by, 0.95), 'rust_dark')
m.box((0.3, 0.012, 0.012), (bx + 0.141, by, 0.97), 'gunmetal', rot=(0, 0, 0)) if False else None
# cracked basin edge chip
m.box((0.06, 0.05, 0.03), (bx - 0.15, by - 0.2, 0.755), 'concrete')
# --- Cracked floor tiles in front of the tub (flat decals, low)
for (x, y, c) in ((-0.8, 0.15, 'plaster_dirty'), (-0.5, 0.18, 'concrete'), (-0.2, 0.2, 'plaster'), (0.1, 0.2, 'concrete_light'), (-0.65, -0.05, 'plaster')):
    m.box((0.22, 0.2, 0.012), (x, y, 0.006), c, rot=(0, 0, rng.uniform(-8, 8)))
finish(m, bounds={'x': (-1.1, 1.1), 'y': (-0.85, 0.85), 'z': (0, 1.1)})
