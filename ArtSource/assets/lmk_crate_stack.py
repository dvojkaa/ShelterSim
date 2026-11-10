import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('lmk_crate_stack', seed=1104)
def crate(x, y, z, s, c, rot=0):
    m.box(s, (x, y, z + s[2] / 2), c, rot=(0, 0, rot), bevel=0.02)
    for k in (-1, 1):
        m.box((s[0] + 0.03, 0.08, s[2]) if False else (0.08, s[1] + 0.03, s[2]), (x + k * (s[0] / 2 - 0.1) * math.cos(math.radians(rot)), y + k * (s[0] / 2 - 0.1) * math.sin(math.radians(rot)), z + s[2] / 2), 'wood_dark', rot=(0, 0, rot))
    m.box((s[0] + 0.02, s[1] + 0.02, 0.06), (x, y, z + s[2] / 2), 'wood_dark', rot=(0, 0, rot)) if False else None
crate(-0.5, 0, 0, (1.0, 1.0, 0.9), 'wood')
crate(0.55, 0.05, 0, (0.9, 1.1, 0.7), 'wood_light', 6)
crate(-0.45, 0.0, 0.9, (0.9, 0.9, 0.6), 'wood_grey', -8)
crate(0.5, 0.05, 0.7, (0.7, 0.7, 0.55), 'army_green', 15)
crate(0.0, -0.05, 1.5 - 0.0, (0.01, 0.01, 0.01), 'wood') if False else None
m.box((0.6, 0.02, 0.18), (-0.5, -0.51, 0.5), 'paint_white')
m.box((0.5, 0.02, 0.1), (-0.45, -0.46, 1.2), 'warning_red')
m.box((0.5, 0.04, 0.5), (0.5, -0.47, 0.35), 'wood_dark', rot=(0, 0, 0))
finish(m, bounds={'x': (-1.1, 1.1), 'y': (-0.9, 0.8), 'z': (0, 1.5)})
