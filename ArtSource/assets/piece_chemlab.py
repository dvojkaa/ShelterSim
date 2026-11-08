import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('piece_chemlab', seed=83)
for sx in (-1, 1):
    for sy in (-1, 1): m.box((0.06, 0.06, 0.8), (sx * 0.68, sy * 0.3, 0.4), 'steel_dark')
m.box((1.5, 0.8, 0.05), (0, 0, 0.82), 'paint_white', bevel=0.01)
m.box((1.4, 0.7, 0.1), (0, 0, 0.75), 'steel_dark')
m.box((1.4, 0.04, 0.3), (0, 0.33, 0.55), 'steel_dark') if False else None
m.box((1.4, 0.04, 0.04), (0, -0.3, 0.4), 'steel_dark') if False else None
m.box((1.4, 0.6, 0.04), (0, 0, 0.3), 'steel_dark')
# jars of colourful liquid
cols = ['leaf_green', 'berry_light', 'orange', 'plastic_blue', 'warning_red']
for i, c in enumerate(cols):
    x = -0.6 + i * 0.16
    m.cyl(0.06, 0.2, (x, 0.2, 0.95), 'glass_dirty', seg=8)
    m.cyl(0.05, 0.12 + (i % 2) * 0.03, (x, 0.2, 0.91), c, seg=8)
    m.cyl(0.04, 0.03, (x, 0.2, 1.07), 'steel_dark', seg=8)
# flask and tube rack
m.cyl(0.1, 0.16, (-0.2, -0.1, 0.93), 'glass_dirty', seg=10, r2=0.04)
m.cyl(0.085, 0.07, (-0.2, -0.1, 0.89), 'leaf_green', seg=10, r2=0.07)
m.cyl(0.05, 0.1, (-0.2, -0.1, 1.06), 'glass_dirty', seg=8)
m.tube([(-0.2, -0.1, 1.1), (-0.2, -0.1, 1.18), (0.05, -0.1, 1.18), (0.05, -0.1, 1.0), (0.2, -0.1, 0.95)], 0.012, 'chrome', seg=5)
# burner + stand
m.cyl(0.07, 0.03, (0.4, -0.15, 0.86), 'steel_dark', seg=8)
m.cyl(0.015, 0.2, (0.4, -0.15, 0.95), 'steel_dark', seg=5)
m.cyl(0.02, 0.05, (0.4, -0.15, 1.0), 'orange', seg=5, r2=0.0)
for a in (0, 120, 240):
    m.box((0.02, 0.02, 0.28), (0.4 + 0.07 * math.cos(math.radians(a)), -0.15 + 0.07 * math.sin(math.radians(a)), 1.0), 'steel_dark')
m.cyl(0.1, 0.1, (0.4, -0.15, 1.12), 'glass_dirty', seg=10)
m.cyl(0.085, 0.06, (0.4, -0.15, 1.1), 'water', seg=10)
# test tubes in a rack
m.box((0.3, 0.06, 0.02), (0.55, 0.2, 0.9), 'wood')
for i in range(4):
    m.cyl(0.015, 0.14, (0.45 + i * 0.07, 0.2, 0.98), 'glass_dirty', seg=5)
    m.cyl(0.011, 0.07, (0.45 + i * 0.07, 0.2, 0.95), ['warning_red', 'hazard_yellow', 'leaf_green', 'plastic_blue'][i], seg=5)
# hazard label and microscope-ish
m.box((0.16, 0.01, 0.1), (-0.5, -0.36, 0.5), 'hazard_yellow') if False else m.box((0.2, 0.01, 0.12), (0.0, -0.36, 0.55), 'hazard_yellow')
m.box((0.06, 0.02, 0.06), (0.0, -0.37, 0.55), 'warning_red', rot=(0, 45, 0))
finish(m, bounds={'x': (-0.75, 0.75), 'y': (-0.4, 0.4), 'z': (0, 1.2)})
