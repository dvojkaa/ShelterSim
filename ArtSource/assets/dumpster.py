import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('dumpster', seed=41)
m.box((1.7, 1.0, 0.8), (0, 0, 0.75), 'paint_green', taper=(1.0, 1.0), bevel=0.02)
m.box((1.78, 1.08, 0.1), (0, 0, 0.12), 'gunmetal')
for s in (-1, 1): m.box((0.1, 1.0, 0.14), (s * 0.7, 0, 0.17), 'steel_dark')
# sloped lid halves
for s, c in ((-1, 'plastic_blue'), (1, 'plastic_blue')):
    m.box((0.9, 1.06, 0.06), (s * 0.42, 0, 1.2), c, rot=(0, s * 7, 0))
m.box((1.76, 0.06, 0.06), (0, -0.52, 1.12), 'steel_dark')
m.box((1.76, 0.06, 0.06), (0, 0.52, 1.12), 'steel_dark')
# ribs
for x in (-0.6, 0, 0.6):
    m.box((0.06, 1.06, 0.7), (x, 0, 0.72), 'paint_green') if False else m.box((0.05, 0.02, 0.7), (x, -0.5, 0.72), 'army_green_dark')
for z in (0.5, 0.9): m.box((1.72, 0.02, 0.05), (0, -0.51, z), 'army_green_dark')
for s in (-1, 1): m.cyl(0.04, 0.12, (s * 0.8, -0.5, 0.15), 'rubber', rot=(0, 90, 0), seg=8) if False else m.cyl(0.07, 0.07, (s * 0.7, -0.45, 0.035), 'rubber', seg=8)
m.box((0.5, 0.01, 0.2), (0.4, -0.51, 0.75), 'paint_white')
finish(m, bounds={'x': (-0.9, 0.9), 'y': (-0.55, 0.55), 'z': (0, 1.3)})
