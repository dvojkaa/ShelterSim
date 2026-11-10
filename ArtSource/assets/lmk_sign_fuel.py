import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

from _lmk_sign import sign_base
m = Model('lmk_sign_fuel', seed=1201)
sign_base(m, 'mustard')
Y = -0.11
# fuel drop icon (poly) on black disc
m.cyl(0.62, 0.05, (-0.95, Y, 2.5), 'black', rot=(90, 0, 0), seg=14)
drop = [(0, 0.5), (0.12, 0.25), (0.3, 0.0), (0.34, -0.2), (0.22, -0.4), (0, -0.48), (-0.22, -0.4), (-0.34, -0.2), (-0.3, 0.0), (-0.12, 0.25)]
m.poly([(-x - 0.0, z) for x, z in reversed(drop)], 0.05, (-0.95, Y - 0.04, 2.5), 'hazard_yellow')
# pump silhouette
m.box((0.7, 0.06, 1.2), (0.55, Y, 2.5), 'black', bevel=0.02)
m.box((0.5, 0.07, 0.4), (0.55, Y - 0.03, 2.8), 'glass_dirty')
m.box((0.5, 0.07, 0.1), (0.55, Y - 0.03, 2.35), 'hazard_yellow')
m.box((0.18, 0.06, 0.8), (1.0, Y, 2.3), 'black', rot=(0, 0, 10))
m.tube([(0.9, Y - 0.02, 2.7), (1.1, Y - 0.02, 2.65), (1.15, Y - 0.02, 2.2)], 0.03, 'black', seg=5)
m.box((1.5, 0.06, 0.12), (0.7, Y, 1.85), 'black')
m.box((0.5, 0.07, 0.5), (-1.5, -0.085, 1.95), 'rust_dark')
finish(m, bounds={'x': (-2.0, 2.0), 'y': (-0.3, 0.4), 'z': (0, 3.6)})
