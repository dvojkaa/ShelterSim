import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('fence_wood', seed=33)
m.box((0.1, 0.08, 1.2), (-0.95, 0, 0.6), 'paint_white', taper=(0.9, 0.9))
for z in (0.3, 0.85): m.box((2.0, 0.04, 0.07), (0, 0.025, z), 'paint_white')
for i in range(8):
    x = -0.8 + i * 0.24 + 0.0
    m.box((0.14, 0.03, 1.0), (x, -0.025, 0.55), 'paint_white', taper=(1, 1))
    m.poly([(-0.07, 0), (0.07, 0), (0, 0.07)], 0.03, (x, -0.025, 1.05), 'paint_white')
finish(m, bounds={'x': (-1, 1), 'y': (-0.05, 0.05), 'z': (0, 1.2)})
