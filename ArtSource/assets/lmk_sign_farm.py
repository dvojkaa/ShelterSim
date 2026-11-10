import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

from _lmk_sign import sign_base
m = Model('lmk_sign_farm', seed=1202)
sign_base(m, 'paint_green')
Y = -0.11
# barn icon
m.box((1.2, 0.06, 0.8), (-0.9, Y, 2.3), 'brick', bevel=0.01)
m.poly([(-0.7, 0), (0.7, 0), (0.5, 0.3), (0, 0.5), (-0.5, 0.3)], 0.06, (-0.9, Y, 2.7), 'brick_dark')
m.box((0.4, 0.07, 0.5), (-0.9, Y - 0.02, 2.15), 'paint_white')
m.box((0.34, 0.075, 0.03), (-0.9, Y - 0.03, 2.15), 'brick_dark', rot=(0, 28, 0))
m.box((0.34, 0.075, 0.03), (-0.9, Y - 0.03, 2.15), 'brick_dark', rot=(0, -28, 0))
# wheat ear
m.box((0.06, 0.06, 1.3), (0.9, Y, 2.3), 'mustard_dark')
for i in range(5):
    z = 2.55 + i * 0.15
    for s in (-1, 1):
        m.box((0.2, 0.06, 0.1), (0.9 + s * 0.12, Y, z), 'mustard', rot=(0, s * 35, 0))
m.box((0.14, 0.06, 0.2), (0.9, Y, 3.0), 'mustard')
m.box((1.4, 0.06, 0.1), (0.0, Y, 1.8), 'paint_white')
m.box((0.6, 0.07, 0.6), (1.4, -0.085, 3.0), 'rust_dark')
finish(m, bounds={'x': (-2.0, 2.0), 'y': (-0.3, 0.4), 'z': (0, 3.6)})
