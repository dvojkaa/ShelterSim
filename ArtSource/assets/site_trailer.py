import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('site_trailer', seed=71)
F = -0.75
m.box((6.0, 2.0, 2.6), (0, 0.25, 1.65), 'paint_white', bevel=0.03)
m.box((6.0, 2.0, 0.25), (0, 0.25, 0.3), 'steel_dark')
m.box((5.8, 1.9, 0.1), (0, 0.25, 0.55), 'steel')
for x in (-2.8, 0, 2.8):
    m.box((0.2, 2.0, 0.12), (x, 0.25, 0.1), 'gunmetal')
m.box((5.96, 0.02, 0.35), (0, F - 0.005, 1.0), 'plastic_blue')
for i in range(20):
    x = -2.85 + i * 0.3
    m.box((0.05, 0.02, 1.6), (x, F - 0.005, 1.9), 'steel')
m.box((6.0, 2.0, 0.1), (0, 0.25, 2.95), 'steel')
# door at -Y
m.box((1.0, 0.05, 2.0), (1.4, F - 0.02, 1.65), 'plastic_blue', bevel=0.01)
for s in (-1, 1): m.box((0.08, 0.06, 2.1), (1.4 + s * 0.55, F - 0.02, 1.65), 'steel_dark')
m.box((1.1, 0.06, 0.08), (1.4, F - 0.02, 2.7), 'steel_dark')
m.box((0.06, 0.07, 0.2), (1.0, F - 0.05, 1.6), 'chrome')
m.box((0.5, 0.02, 0.35), (1.4, F - 0.055, 2.2), 'glass')
# window
m.box((1.3, 0.05, 0.9), (-1.2, F - 0.02, 1.9), 'steel_dark')
m.box((1.15, 0.05, 0.75), (-1.2, F - 0.04, 1.9), 'glass')
m.box((0.05, 0.06, 0.75), (-1.2, F - 0.05, 1.9), 'paint_white')
m.box((1.45, 0.1, 0.05), (-1.2, F - 0.05, 1.42), 'steel')
# steps: platform and one step, railings
m.box((1.4, 0.45, 0.07), (1.4, F - 0.225, 0.62), 'steel')
m.box((0.05, 0.45, 0.55), (0.75, F - 0.225, 0.35), 'steel_dark')
m.box((0.05, 0.45, 0.55), (2.05, F - 0.225, 0.35), 'steel_dark')
m.box((1.1, 0.25, 0.07), (1.4, -1.125, 0.32), 'steel')
for y in (F - 0.05, F - 0.4):
    for x in (0.75, 2.05): m.box((0.05, 0.05, 0.85), (x, y, 1.05), 'hazard_yellow')
for x in (0.75, 2.05): m.box((0.05, 0.5, 0.05), (x, F - 0.22, 1.45), 'hazard_yellow')
m.box((0.2, 0.1, 0.15), (1.4, F - 0.1, 2.85), 'lamp_warm')
# roof vents / AC on top kept inside z 3.0: a flat cover
m.box((0.9, 0.5, 0.03), (-2.0, 0.3, 3.01), 'steel', bevel=0.01) if False else None
# back wall panel
m.box((0.5, 0.05, 0.5), (2.0, 1.22, 1.7), 'steel_dark')
m.box((1.2, 0.04, 0.3), (-1.0, 1.24, 1.6), 'hazard_yellow') if False else None
finish(m, bounds={'x': (-3.0, 3.0), 'y': (-1.25, 1.25), 'z': (0, 3.0)})
