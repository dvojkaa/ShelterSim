import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_bunker_roof', seed=1303)
r = random.Random(1303)
m.box((24, 24, 1.0), (0, 0, 0.5), 'concrete', bevel=0.05)
m.box((24, 24, 0.12), (0, 0, 1.0), 'concrete_dark')
m.box((23.4, 23.4, 0.2), (0, 0, 1.1), 'concrete_light', bevel=0.03)
# parapet ring
for s in (-1, 1):
    m.box((24, 0.5, 0.3), (0, s * 11.75, 1.15), 'concrete_dark', bevel=0.03)
    m.box((0.5, 23.0, 0.3), (s * 11.75, 0, 1.15), 'concrete_dark', bevel=0.03)
# seams
for i in range(-2, 3):
    m.box((0.06, 23.0, 0.03), (i * 4.5, 0, 1.21), 'concrete_dark')
    m.box((23.0, 0.06, 0.03), (0, i * 4.5, 1.21), 'concrete_dark')
# armoured hatches
for (x, y) in ((-6, -6), (6, 5), (-7, 7)):
    m.box((2.2, 2.2, 0.15), (x, y, 1.28), 'steel_dark', bevel=0.03)
    m.box((1.9, 1.9, 0.12), (x, y, 1.4), 'gunmetal', bevel=0.03)
    m.cyl(0.25, 0.1, (x, y, 1.5), 'rust', seg=10)
    for k in range(4):
        a = math.radians(90 * k + 45)
        m.cyl(0.05, 0.1, (x + 0.8 * math.cos(a), y + 0.8 * math.sin(a), 1.5), 'chrome', seg=5)
    m.box((2.3, 0.4, 0.05), (x, y - 1.2, 1.25), 'hazard_yellow')
# vents / stacks
for (x, y) in ((0, 0), (3.5, -7), (-3, 8.5), (8, 0), (-9, -2), (9.5, -8)):
    m.cyl(0.55, 0.9, (x, y, 1.65), 'steel_dark', seg=10, r2=0.5)
    m.cyl(0.7, 0.12, (x, y, 2.16), 'rust_dark', seg=10, r2=0.6)
    m.cyl(0.62, 0.08, (x, y, 1.42), 'steel', seg=10)
# large central blast vent block
m.box((3.2, 2.4, 0.8), (-1, 4.5, 1.6), 'concrete_dark', bevel=0.04)
for k in range(6):
    m.box((2.8, 0.1, 0.4), (-1, 3.55 + k * 0.38, 2.05), 'steel_dark', rot=(25, 0, 0))
# antennas + a lamp mast
m.cyl(0.05, 3.0, (9, 8, 2.7), 'steel', seg=6)
m.cyl(0.08, 0.1, (9, 8, 4.2), 'red_bright', seg=6)
m.cyl(0.05, 2.0, (-10, -9, 2.2), 'steel_dark', seg=6)
m.box((1.0, 0.04, 0.04), (-10, -9, 3.0), 'steel_dark')
# rust streaks & patches
for i in range(40):
    x = r.uniform(-11, 11); y = r.uniform(-11, 11)
    m.box((r.uniform(0.15, 0.5), r.uniform(0.8, 2.5), 0.02), (x, y, 1.215), r.choice(['rust_dark', 'rust', 'concrete_dark', 'black']), rot=(0, 0, r.uniform(-30, 30)))
for i in range(10):
    m.box((0.12, r.uniform(0.8, 2.0), 0.02), (r.uniform(-11, 11), -11.97, r.uniform(0.4, 0.9)), 'rust_dark') if False else None
for s in (-1, 1):
    for i in range(6):
        m.box((r.uniform(0.15, 0.4), 0.03, r.uniform(0.4, 0.9)), (r.uniform(-11, 11), s * 11.97, 0.55), 'rust_dark')
# stencil on roof
m.box((3.0, 0.8, 0.02), (-4, -10, 1.215), 'hazard_yellow')
for k in range(8):
    m.box((0.2, 0.8, 0.03), (-5.2 + k * 0.4, -10, 1.225), 'black', rot=(0, 0, 25))
finish(m, bounds={'x': (-12.0, 12.0), 'y': (-12.0, 12.0), 'z': (0, 4.4)})
