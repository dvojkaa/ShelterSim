import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('piece_stove', seed=81)
for sx in (-1, 1):
    for sy in (-1, 1): m.box((0.06, 0.06, 0.2), (sx * 0.4, sy * 0.28, 0.1), 'steel_dark')
m.box((0.9, 0.7, 0.6), (0, 0, 0.5), 'steel_dark', bevel=0.02)
m.box((0.92, 0.72, 0.05), (0, 0, 0.82), 'gunmetal')
# patches and door
m.box((0.3, 0.02, 0.28), (-0.2, -0.36, 0.5), 'gunmetal')
m.box((0.28, 0.02, 0.26), (-0.2, -0.37, 0.5), 'rust_dark')
m.box((0.18, 0.02, 0.12), (-0.2, -0.38, 0.5), 'lamp_warm') if False else None
m.box((0.12, 0.02, 0.1), (-0.2, -0.385, 0.48), 'orange')
m.box((0.28, 0.02, 0.2), (0.25, -0.36, 0.55), 'rust')
for p in ((0.15, 0.62), (0.35, 0.62), (0.15, 0.45), (0.35, 0.45)): m.cyl(0.015, 0.02, (p[0], -0.375, p[1]), 'steel', rot=(90, 0, 0), seg=5)
m.box((0.12, 0.03, 0.03), (0.0, -0.385, 0.62), 'black')
m.box((0.05, 0.04, 0.14), (-0.0, -0.38, 0.38), 'steel')
# flue
m.cyl(0.07, 0.4, (0.3, 0.2, 1.05), 'gunmetal', seg=8) if False else None
m.tube([(0.3, 0.22, 0.85), (0.3, 0.22, 1.02)], 0.065, 'gunmetal', seg=8)
m.cyl(0.085, 0.04, (0.3, 0.22, 1.08), 'rust_dark', seg=8) if False else None
# pot on top
m.cyl(0.17, 0.2, (-0.2, -0.05, 0.92), 'steel', seg=10)
m.cyl(0.175, 0.03, (-0.2, -0.05, 1.03), 'steel_dark', seg=10)
m.box((0.12, 0.03, 0.03), (-0.43, -0.05, 1.02), 'black') if False else None
m.cyl(0.015, 0.2, (-0.2, -0.05, 1.08), 'black', rot=(0, 90, 0), seg=4) if False else None
m.box((0.5, 0.03, 0.03), (-0.2, -0.05, 1.04), 'wood_dark') if False else None
m.cyl(0.03, 0.04, (-0.2, -0.05, 1.07), 'black', seg=6)
m.cyl(0.02, 0.4, (-0.38, 0.0, 0.88), 'wood_dark', rot=(0, 70, 0), seg=5) if False else None
finish(m, bounds={'x': (-0.5, 0.5), 'y': (-0.4, 0.4), 'z': (0, 1.1)})
