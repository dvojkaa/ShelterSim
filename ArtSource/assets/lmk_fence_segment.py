import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_fence_segment', seed=1203)
r = random.Random(1203)
for sx in (-1, 1):
    m.cyl(0.05, 2.3, (sx * 1.95, 0, 1.15), 'steel_dark', seg=6)
    m.box((0.1, 0.1, 0.08), (sx * 1.95, 0, 0.04), 'concrete_dark')
    m.box((0.06, 0.5, 0.06), (sx * 1.95, 0.0, 2.25), 'steel_dark', rot=(0, 0, 0))   # barbed arm
    m.box((0.5, 0.06, 0.06), (sx * 1.95, -0.2, 2.35), 'steel_dark', rot=(0, 0, 0)) if False else None
for z in (0.05, 1.1, 2.05):
    sag = 0.05 if z > 1 else 0.0
    m.box((3.9, 0.04, 0.05), (0, 0, z - sag), 'steel')
# mesh: diamond lattice of short crossed bars
for row in range(5):
    z = 0.3 + row * 0.42
    for i in range(9):
        x = -1.75 + i * 0.4375 + (0.22 if row % 2 else 0)
        if abs(x) > 1.8: continue
        sag = 0.05 * (1 - (x / 1.95) ** 2) * (z / 2.0)
        for s_ in (-1, 1):
            m.box((0.012, 0.012, 0.55), (x, 0.0, z - sag), 'steel', rot=(0, s_ * 40, 0))
# barbed wire strands
for dy in (-0.12, 0.12):
    pts = [(-1.95 + k * 0.325, 0.25 * (1 if dy > 0 else -1), 2.4 + 0.04 * math.sin(k * 1.7)) for k in range(13)]
    m.tube(pts, 0.012, 'gunmetal', seg=4)
for k in range(12):
    x = -1.8 + k * 0.33
    m.box((0.05, 0.03, 0.03), (x, 0.0, 2.4), 'rust', rot=(0, 0, 45))
# torn flap and rust
m.box((0.8, 0.02, 0.6), (0.6, -0.02, 0.4), 'rust_dark', rot=(0, 15, 0))
finish(m, bounds={'x': (-2.0, 2.0), 'y': (-0.3, 0.3), 'z': (0, 2.6)})
