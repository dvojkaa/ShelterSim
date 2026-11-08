import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('power_pole', seed=37)
m.cyl(0.17, 8.4, (0, 0, 4.2), 'wood_dark', seg=8, r2=0.12)
m.cyl(0.2, 0.2, (0, 0, 0.1), 'wood_grey', seg=8)
m.box((2.3, 0.1, 0.12), (0, -0.14, 7.9), 'wood', rot=(0, 0, 0))
for s in (-1, 1):
    beam(m, (0, 7.2), (s * 0.9, 7.9), 0.07, 0.07, -0.14, 'wood')
for x in (-1.0, -0.4, 0.4, 1.0):
    m.cyl(0.03, 0.2, (x, -0.14, 8.08), 'steel_dark', seg=6)
    m.cyl(0.06, 0.07, (x, -0.14, 8.17), 'paint_green' if False else 'glass', seg=8)
    m.cyl(0.05, 0.07, (x, -0.14, 8.24), 'glass', seg=8)
m.cyl(0.08, 0.1, (0.0, 0, 8.45), 'steel_dark', seg=6, r2=0.03)
# transformer can and a down-wire
m.cyl(0.2, 0.6, (0.4, 0.0, 7.0), 'gunmetal', seg=10)
m.cyl(0.2, 0.05, (0.4, 0.0, 7.3), 'steel', seg=10)
m.tube([(0.1, 0.1, 6.8), (0.12, 0.17, 3.0), (0.1, 0.18, 1.5)], 0.015, 'black', seg=4)
m.box((0.18, 0.06, 0.3), (0.0, 0.16, 1.5), 'steel')
m.box((0.14, 0.02, 0.14), (0.0, -0.17, 2.4), 'hazard_yellow')
for z in (1.0, 1.5, 2.0):
    m.box((0.12, 0.04, 0.04), (0.12, 0.0, z), 'steel_dark', rot=(0, 0, 40)) if False else None
finish(m, bounds={'x': (-1.2, 1.2), 'y': (-0.2, 0.2), 'z': (0, 8.5)})
