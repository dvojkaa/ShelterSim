import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('house_door', seed=31)
m.box((0.9, 0.15, 0.1), (0, 0, 2.15), 'paint_white')
for s in (-1, 1): m.box((0.1, 0.15, 2.2), (s * 0.5, 0, 1.1), 'paint_white')
m.box((0.9, 0.08, 2.05), (0, 0.0, 1.03), 'paint_green', bevel=0.01)
for s in (-1, 1):
    for z in (0.5, 1.5):
        m.box((0.32, 0.02, 0.7 if z < 1 else 0.7), (s * 0.19, -0.045, z + 0.1), 'plastic_blue' if False else 'paint_green', bevel=0.01)
m.box((0.5, 0.02, 0.25), (0, -0.05, 1.75), 'glass')
m.box((0.56, 0.015, 0.31), (0, -0.045, 1.75), 'paint_white')
m.cyl(0.035, 0.03, (0.32, -0.06, 1.0), 'chrome', rot=(90, 0, 0), seg=8)
m.box((0.14, 0.02, 0.05), (0.32, -0.05, 1.12), 'chrome')
m.box((0.12, 0.012, 0.06), (-0.0, -0.05, 1.45), 'chrome')
m.box((1.1, 0.15, 0.05), (0, 0, 0.025), 'concrete_light')
# step lamp on the wall beside the door (right)
m.box((0.12, 0.05, 0.2), (0.0 + 0.62 - 0.0, -0.05, 1.6), 'gunmetal') if False else None
m.cyl(0.07, 0.14, (0.0, -0.065, 2.0), 'lamp_warm', rot=(90, 0, 0), seg=8) if False else None
m.box((0.13, 0.03, 0.05), (0, -0.06, 2.08), 'gunmetal')
m.box((0.12, 0.04, 0.16), (0, -0.055, 1.97), 'lamp_warm')
m.box((0.14, 0.04, 0.03), (0, -0.055, 2.06), 'gunmetal')
finish(m, bounds={'x': (-0.55, 0.55), 'y': (-0.075, 0.075), 'z': (0, 2.2)})
