import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('bunker_vent', seed=64)
m.box((0.8, 0.8, 0.12), (0, 0, 0.06), 'concrete', bevel=0.02)
m.cyl(0.22, 0.9, (0, 0, 0.55), 'steel', seg=10)
m.cyl(0.26, 0.06, (0, 0, 0.14), 'steel_dark', seg=10)
m.cyl(0.04, 0.5, (0, 0, 1.0), 'steel_dark', seg=6) if False else None
# mushroom cap
m.cyl(0.28, 0.1, (0, 0, 1.12), 'steel_dark', seg=10, r2=0.2)
m.cyl(0.38, 0.08, (0, 0, 1.28), 'steel', seg=12, r2=0.32)
m.cyl(0.32, 0.1, (0, 0, 1.38), 'steel', seg=12, r2=0.1)
m.cyl(0.05, 0.1, (0, 0, 1.5), 'rust' if False else 'steel_dark', seg=6)
for a in range(4):
    m.cyl(0.02, 0.2, (0.18 * math.cos(math.radians(a * 90 + 45)) if False else 0, 0, 1.2), 'steel_dark', seg=5) if False else None
m.cyl(0.24, 0.04, (0, 0, 1.18), 'gunmetal', seg=10)
for a in range(4):
    import math
    ang = math.radians(a * 90 + 45)
    m.box((0.05, 0.05, 0.2), (0.22 * math.cos(ang), 0.22 * math.sin(ang), 1.2), 'steel_dark')
m.box((0.06, 0.2, 0.06), (0.0, -0.3, 0.12), 'steel') if False else None
stripes(m, -0.35, 0.35, -0.385, 0.03, 0.1, 0.02, w=0.1)
finish(m, bounds={'x': (-0.4, 0.4), 'y': (-0.4, 0.4), 'z': (0, 1.6)})
