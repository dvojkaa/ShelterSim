import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('piece_electronics', seed=84)
for sx in (-1, 1):
    for sy in (-1, 1): m.box((0.06, 0.06, 0.8), (sx * 0.68, sy * 0.3, 0.4), 'steel_dark')
m.box((1.5, 0.8, 0.05), (0, 0, 0.82), 'army_green_dark', bevel=0.01)
m.box((1.4, 0.7, 0.14), (0, 0, 0.72), 'steel_dark')
m.box((1.4, 0.6, 0.04), (0, 0, 0.3), 'steel_dark')
# oscilloscope (right)
m.box((0.45, 0.35, 0.3), (0.45, 0.15, 1.0), 'gunmetal', bevel=0.02)
m.box((0.3, 0.02, 0.2), (0.4, -0.035, 1.03), 'black')
m.box((0.26, 0.015, 0.16), (0.4, -0.04, 1.03), 'leaf_dark')
m.box((0.2, 0.015, 0.01), (0.4, -0.05, 1.03), 'lamp_warm')
for i in range(3): m.cyl(0.025, 0.03, (0.58, -0.04, 0.95 + i * 0.07), 'steel', rot=(90, 0, 0), seg=6)
for i in range(3): m.cyl(0.02, 0.03, (0.3 + i * 0.1, -0.04, 0.9), 'warning_red' if i == 0 else 'steel', rot=(90, 0, 0), seg=6)
# circuit boards
m.box((0.3, 0.2, 0.012), (-0.35, -0.15, 0.85), 'paint_green')
for i in range(4): m.box((0.05, 0.04, 0.02), (-0.45 + i * 0.07, -0.15 + (i % 2) * 0.05, 0.865), 'black')
m.box((0.12, 0.08, 0.012), (-0.5, 0.12, 0.85), 'paint_green')
m.box((0.2, 0.14, 0.012), (-0.1, 0.1, 0.85), 'paint_green')
m.box((0.05, 0.05, 0.015), (-0.1, 0.1, 0.862), 'gunmetal')
for p in ((-0.3, -0.2), (-0.4, -0.12)): m.box((0.02, 0.02, 0.02), (p[0], p[1], 0.865), 'hazard_yellow')
# wire spools
for i, c in enumerate(['warning_red', 'plastic_blue', 'hazard_yellow']):
    x = -0.55 + i * 0.12
    m.cyl(0.05, 0.06, (x, 0.28, 0.88), c, seg=8)
    m.cyl(0.02, 0.07, (x, 0.28, 0.88), 'steel_dark', seg=6)
m.cyl(0.05, 0.03, (0.05, 0.28, 0.86), 'steel_dark', seg=8) if False else None
# soldering iron in stand
m.box((0.12, 0.1, 0.03), (0.1, -0.15, 0.87), 'steel_dark')
m.cyl(0.015, 0.25, (0.1, -0.15, 0.97), 'black', seg=6, rot=(0, 25, 0))
m.cyl(0.008, 0.06, (0.17, -0.15, 1.09), 'chrome', seg=4, rot=(0, 25, 0))
m.cyl(0.015, 0.1, (0.05, -0.15, 0.89), 'orange', seg=5) if False else None
m.tube([(0.03, -0.15, 0.88), (-0.1, -0.3, 0.84), (-0.2, -0.3, 0.84)], 0.006, 'black', seg=4)
# lamp
m.cyl(0.04, 0.02, (-0.6, 0.0, 0.86), 'gunmetal', seg=6) if False else None
# panel with switches under the bench top
m.box((0.5, 0.02, 0.18), (0.0, -0.355, 0.7), 'gunmetal')
for i in range(5): m.box((0.03, 0.03, 0.06), (-0.16 + i * 0.08, -0.37, 0.7), 'steel' if i != 2 else 'warning_red')
finish(m, bounds={'x': (-0.75, 0.75), 'y': (-0.4, 0.4), 'z': (0, 1.3)})
