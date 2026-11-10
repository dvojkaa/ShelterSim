import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_mil_barricade', seed=1205)
r = random.Random(1205)
# concrete barrier blocks as base, 3 blocks of 4 m, gun slit gap in the sandbag course above
for i in range(3):
    x = -4 + i * 4
    m.box((3.9, 1.0, 0.7), (x, 0, 0.35), 'concrete', bevel=0.03, taper=(0.9, 0.8) if False else None)
    m.box((3.9, 0.5, 0.2), (x, 0, 0.8), 'concrete_light', bevel=0.02)
    m.box((3.92, 1.02, 0.08), (x, 0, 0.1), 'concrete_dark')
    m.box((0.5, 0.04, 0.3), (x + r.uniform(-1, 1), -0.52, 0.45), 'rust_dark')
    m.box((0.4, 0.04, 0.5), (x + 1.2, -0.52, 0.5), 'hazard_yellow')
# sandbag courses
def bag(x, y, z, c):
    m.blob((0.55, 0.32, 0.2), (x, y, z), c, rough=0.1, subdiv=1)
for course in range(4):
    z = 0.95 + course * 0.17
    n = 22
    for i in range(n):
        x = -5.8 + i * 0.53 + (0.26 if course % 2 else 0)
        if abs(x) > 5.6: continue
        if course in (1, 2) and abs(x) < 0.75: continue      # the gun slit
        for y in (-0.7, -0.38, 0.38, 0.7):
            bag(x, y * 0.6, z, r.choice(['sand', 'sand', 'plaster_dirty', 'army_green']))
# slit frame
for sx in (-1, 1):
    m.box((0.12, 1.1, 0.5), (sx * 0.82, 0, 1.2), 'wood_dark')
m.box((1.7, 1.1, 0.1), (0, 0, 1.5), 'wood_dark')
m.box((1.4, 1.0, 0.08), (0, 0, 1.0), 'steel_dark')
# top row stack
for i in range(22):
    x = -5.6 + i * 0.53
    if abs(x) < 0.9: continue
    bag(x, 0, 1.48, r.choice(['sand', 'plaster_dirty']))
# ends: blocks
for sx in (-1, 1):
    m.blob((0.6, 0.9, 1.2), (sx * 5.55, 0, 0.6), 'concrete', rough=0.1, flat_bottom=0.0)
finish(m, bounds={'x': (-6.0, 6.0), 'y': (-1.0, 1.0), 'z': (0, 1.6)})
