import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('garden_shed', seed=39)
m.box((2.8, 2.3, 0.12), (0, 0, 0.06), 'concrete_dark')
m.box((2.7, 2.1, 1.95), (0, 0, 1.1), 'wood_light', bevel=0.02)
for i in range(13):
    m.box((0.02, 2.14, 1.9), (-1.2 + i * 0.2, 0, 1.1), 'wood') if False else m.box((0.02, 0.03, 1.9), (-1.2 + i * 0.2, -1.07, 1.1), 'wood_dark')
for s in (-1, 1): m.box((0.12, 0.06, 2.0), (s * 1.3, -1.06, 1.1), 'paint_white')
# door on front
m.box((0.9, 0.06, 1.7), (-0.5, -1.08, 0.97), 'paint_green', bevel=0.01)
m.box((0.1, 0.07, 0.1), (-0.15, -1.12, 1.0), 'chrome')
beam(m, (-0.9, 0.2), (-0.1, 1.7), 0.08, 0.02, -1.12, 'wood_dark')
m.box((0.7, 0.06, 0.5), (0.7, -1.08, 1.3), 'glass')
m.box((0.8, 0.04, 0.6), (0.7, -1.07, 1.3), 'paint_white')
# slightly sloped roof (pent)
m.box((3.0, 2.5, 0.1), (0, 0, 2.15), 'steel_dark', rot=(-6, 0, 0)) if False else None
m.box((3.0, 2.5, 0.1), (0, 0, 2.2), 'army_green_dark', rot=(8, 0, 0))
m.box((3.0, 0.06, 0.14), (0, -1.2, 2.18), 'paint_white', rot=(8, 0, 0)) if False else None
m.box((2.6, 0.3, 0.3), (0, 1.02, 1.5), 'wood_dark') if False else None
# bits leaning on it: spade and barrel
m.cyl(0.015, 1.1, (1.2, -1.15, 0.9), 'wood', seg=5, rot=(0, 8, 0))
m.box((0.18, 0.02, 0.22), (1.35, -1.15, 0.3), 'steel')
finish(m, bounds={'x': (-1.5, 1.5), 'y': (-1.25, 1.25), 'z': (0, 2.6)})
