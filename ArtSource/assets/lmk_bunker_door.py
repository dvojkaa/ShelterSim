import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_bunker_door', seed=1301)
r = random.Random(1301)
# heavy concrete surround, opening 3.4 x 2.8 stays clear
for sx in (-1, 1):
    m.box((0.9, 1.2, 3.6), (sx * 2.15, 0, 1.8), 'concrete', bevel=0.04)
    m.box((0.95, 1.25, 0.3), (sx * 2.15, 0, 0.15), 'concrete_dark')
    m.box((0.14, 0.9, 2.8), (sx * 1.63, -0.1, 1.4), 'steel_dark')            # steel jamb liner
    for k in range(7):   # hazard stripes on the front of the jambs
        z = 0.5 + k * 0.45
        m.box((0.5, 0.05, 0.2), (sx * 2.15, -0.62, z), 'hazard_yellow' if k % 2 == 0 else 'black', rot=(0, 18 * sx, 0))
m.box((5.2, 1.2, 0.8), (0, 0, 3.2), 'concrete', bevel=0.04)
m.box((3.5, 0.9, 0.14), (0, -0.1, 2.73), 'steel_dark')
m.box((5.0, 0.06, 0.28), (0, -0.62, 3.2), 'hazard_yellow')
for k in range(12):
    m.box((0.22, 0.07, 0.3), (-2.3 + k * 0.42, -0.62, 3.2), 'black', rot=(0, 25, 0))
m.box((5.4, 1.3, 0.2), (0, 0, 3.7), 'concrete_dark', bevel=0.03) if False else None
# cracks, stains, rust
for i in range(8):
    m.box((r.uniform(0.08, 0.3), 0.03, r.uniform(0.6, 1.6)), (r.choice([-1, 1]) * r.uniform(1.9, 2.5), -0.615, r.uniform(1.2, 3.3)), r.choice(['concrete_dark', 'rust_dark']))
# door bolts and hinge lugs on the left jamb face side opposite leaf
for k in range(4):
    m.cyl(0.14, 0.2, (-1.63 + 0.0, -0.58, 0.5 + k * 0.7), 'steel', rot=(90, 0, 0), seg=8)
# the open leaf, folded flat in front of the right surround
lx = 3.35
m.box((2.6, 0.38, 2.7), (lx, -0.95, 1.4), 'steel_dark', bevel=0.03)
m.box((2.3, 0.1, 2.4), (lx, -1.16, 1.4), 'steel', bevel=0.02)
for z in (0.5, 1.4, 2.3):
    m.box((2.5, 0.12, 0.12), (lx, -1.2, z), 'rust_dark')
for k in range(5):
    m.box((0.5, 0.1, 0.2), (lx - 0.9 + k * 0.45, -1.22, 0.3 + (k % 2) * 0.0), 'hazard_yellow' if k % 2 == 0 else 'black') if False else None
m.cyl(0.4, 0.1, (lx + 0.3, -1.25, 1.5), 'rust', rot=(90, 0, 0), seg=12)
m.cyl(0.12, 0.2, (lx + 0.3, -1.3, 1.5), 'steel', rot=(90, 0, 0), seg=8)
for a in (0, 60, 120):
    m.box((0.9, 0.08, 0.07), (lx + 0.3, -1.33, 1.5), 'steel_dark', rot=(0, a, 0))
for k in range(6):   # hazard slats along the leaf bottom
    m.box((0.35, 0.05, 0.3), (lx - 1.0 + k * 0.4, -1.2, 0.22), 'hazard_yellow' if k % 2 == 0 else 'black')
for z in (0.7, 2.1):   # hinge arms to the jamb
    m.box((0.9, 0.2, 0.25), (lx - 1.3 - 0.35, -0.8, z), 'gunmetal')
    m.cyl(0.14, 0.45, (lx - 1.3 - 0.8, -0.65, z), 'steel_dark', seg=8)
for k in range(14):
    m.cyl(0.04, 0.05, (lx - 1.15 + (k % 7) * 0.38, -1.19, 2.45 if k < 7 else 0.55), 'chrome', rot=(90, 0, 0), seg=5)
finish(m, bounds={'x': (-2.7, 4.8), 'y': (-1.4, 0.65), 'z': (0, 3.8)})
