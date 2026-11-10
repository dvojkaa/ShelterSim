import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_farm_silo', seed=1208)
r = random.Random(1208)
m.cyl(2.7, 0.3, (0, 0, 0.15), 'concrete_dark', seg=14)
secs = [(0.3, 2.4, 'steel'), (2.7, 2.4, 'steel_dark'), (5.1, 2.1, 'steel')]
for z0, h, c in secs:
    m.cyl(2.5, h, (0, 0, z0 + h / 2), c, seg=14)
for z in (0.9, 2.7, 5.1, 7.2):
    m.cyl(2.56, 0.2, (0, 0, z), 'rust_dark', seg=14)
for z in (1.8, 4.0, 6.3):
    m.cyl(2.54, 0.3, (0, 0, z), 'rust', seg=14)
for i in range(10):
    a = r.uniform(0, 6.283); z = r.uniform(1.0, 6.5)
    m.box((0.2, 0.05, r.uniform(0.8, 2.0)), (2.52 * math.cos(a), 2.52 * math.sin(a), z), r.choice(['rust_dark', 'rust_light', 'black']), rot=(0, 0, math.degrees(a) + 90))
# cap
m.cyl(2.55, 0.2, (0, 0, 7.3), 'steel_dark', seg=14)
m.cyl(2.55, 1.0, (0, 0, 7.9), 'rust_light', seg=14, r2=0.5)
m.cyl(0.55, 0.3, (0, 0, 8.4), 'steel_dark', seg=8)
m.cyl(0.35, 0.15, (0, 0, 8.55), 'rust_dark', seg=8) if False else None
# ladder with cage on -Y
for sx in (-1, 1):
    m.box((0.06, 0.08, 7.8), (sx * 0.28, -2.7, 4.0), 'steel_dark')
for i in range(26):
    m.box((0.56, 0.05, 0.05), (0, -2.7, 0.5 + i * 0.29), 'rust')
for z in (2.5, 3.5, 4.5, 5.5, 6.5):
    for sx in (-1, 1):
        m.box((0.05, 0.45, 0.05), (sx * 0.35, -2.55, z), 'steel_dark')
    m.box((0.75, 0.05, 0.05), (0, -2.9, z), 'steel_dark')
for z in (2.0, 4.5, 7.0):
    m.box((0.15, 0.25, 0.06), (0, -2.58, z), 'steel')
# base hatch, outlet chute, hopper
m.box((0.8, 0.12, 1.0), (1.5, -2.0, 0.9), 'gunmetal', rot=(0, 0, -50))
m.cyl(0.35, 0.8, (-2.6, 0.0, 0.7), 'steel_dark', rot=(0, 30, 0), seg=8, r2=0.2)
m.tube([(0.0, 2.5, 7.9), (0.0, 3.0, 7.0), (0.0, 3.0, 1.0)], 0.1, 'steel_dark', seg=6)
finish(m, bounds={'x': (-3.2, 2.9), 'y': (-3.1, 3.1), 'z': (0, 8.7)})
