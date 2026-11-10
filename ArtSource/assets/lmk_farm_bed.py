import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_farm_bed', seed=1209)
r = random.Random(1209)
# frame boards
for sy in (-1, 1):
    for k in range(2):
        m.box((6.0, 0.1, 0.34), (0, sy * 1.45, 0.17 + k * 0.35), r.choice(['wood', 'wood_grey']), bevel=0.01)
for sx in (-1, 1):
    for k in range(2):
        m.box((0.1, 3.0, 0.34), (sx * 2.95, 0, 0.17 + k * 0.35), r.choice(['wood', 'wood_dark']), bevel=0.01)
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((0.2, 0.2, 0.74), (sx * 2.9, sy * 1.4, 0.37), 'wood_dark')
m.box((5.8, 2.8, 0.5), (0, 0, 0.3), 'dirt')
# crop rows along x
for row, y in enumerate((-1.0, -0.33, 0.33, 1.0)):
    m.box((5.6, 0.2, 0.08), (0, y, 0.58), 'dirt')
    n = 9
    for i in range(n):
        x = -2.6 + i * (5.2 / (n - 1)) + r.uniform(-0.1, 0.1)
        h = r.uniform(0.35, 0.8) * (0.6 if row == 3 else 1.0)
        c = r.choice(['leaf_green', 'leaf_dark', 'leaf_dry'])
        if row % 2 == 0:
            m.cyl(0.28, h, (x, y, 0.6 + h / 2), c, seg=5, r2=0.04)       # leafy tufts
            m.blob((0.3, 0.3, 0.2), (x + 0.1, y, 0.9 + h * 0.2), 'berry_light' if r.random() < 0.3 else c, rough=0.2) if r.random() < 0.6 else None
        else:
            m.box((0.05, 0.05, h + 0.2), (x, y, 0.6 + (h + 0.2) / 2), 'leaf_dry')   # stalks
            m.box((0.12, 0.12, 0.25), (x, y, 0.6 + h + 0.2), 'mustard', taper=(0.6, 0.6))
# weeds tufts and twigs
for i in range(10):
    m.cyl(0.18, 0.4, (r.uniform(-2.7, 2.7), r.uniform(-1.3, 1.3), 0.78), 'leaf_brown', seg=5, r2=0.03)
finish(m, bounds={'x': (-3.1, 3.1), 'y': (-1.6, 1.6), 'z': (0, 1.7)})
