import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_bunker_intake', seed=1302)
m.cyl(0.75, 0.25, (0, 0, 0.125), 'concrete_dark', seg=12)
m.cyl(0.62, 1.0, (0, 0, 0.75), 'steel_dark', seg=12, r2=0.58)
m.cyl(0.66, 0.1, (0, 0, 0.45), 'rust', seg=12)
m.cyl(0.64, 0.1, (0, 0, 1.15), 'hazard_yellow', seg=12)
for a in range(0, 360, 90):
    m.box((0.1, 0.2, 0.9), (0.66 * math.cos(math.radians(a + 45)), 0.66 * math.sin(math.radians(a + 45)), 0.7), 'steel', rot=(0, 0, a + 45)) if False else None
# louvred cap: 5 stacked slanted rings, then lid
for i in range(5):
    m.cyl(0.62 - i * 0.0, 0.06, (0, 0, 1.2 + i * 0.07), 'steel') if False else None
for i in range(4):
    m.cyl(0.68, 0.05, (0, 0, 1.2 + i * 0.09), 'steel', seg=12, r2=0.7)
    m.cyl(0.58, 0.04, (0, 0, 1.245 + i * 0.09), 'black', seg=12)
m.cyl(0.74, 0.1, (0, 0, 1.52), 'rust_dark', seg=12, r2=0.5)
m.cyl(0.18, 0.08, (0, 0, 1.58), 'steel_dark', seg=8)
for a in range(0, 360, 90):
    m.box((0.08, 0.08, 0.4), (0.6 * math.cos(math.radians(a + 45)), 0.6 * math.sin(math.radians(a + 45)), 1.3), 'gunmetal')
m.box((0.45, 0.05, 0.4), (0.0, -0.62, 0.65), 'rust_dark')
m.box((0.3, 0.04, 0.2), (0.0, -0.64, 0.95), 'warning_red')
for k in range(3):
    m.box((0.05, 0.04, 0.9), (0.3 + k * 0.1, -0.6, 0.7), 'rust') if False else None
finish(m, bounds={'x': (-0.8, 0.8), 'y': (-0.8, 0.8), 'z': (0, 1.65)})
