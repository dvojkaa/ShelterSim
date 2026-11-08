import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('blast_door_frame', seed=62)
for s in (-1, 1):
    m.box((0.35, 0.54, 3.0), (s * 1.375, 0, 1.5), 'steel_dark', bevel=0.02)
    m.box((0.1, 0.62, 2.3), (s * 1.2 + s * 0.0, 0, 1.15), 'steel') if False else None
    # inner lip and bolts
    m.box((0.05, 0.5, 2.3), (s * 1.225, 0, 1.15), 'steel')
    for z in (0.3, 0.9, 1.5, 2.1, 2.7):
        for y in (-0.22, 0.22) if False else (-0.3,):
            m.cyl(0.04, 0.03, (s * 1.375, -0.27, z), 'steel', rot=(90, 0, 0), seg=6)
    stripes(m, s * 1.375 - 0.15, s * 1.375 + 0.15, -0.3, 0.0, 0.6, 0.02, w=0.14) if False else None
# lintel
m.box((2.9, 0.5, 0.7), (0, 0, 2.65), 'steel_dark', bevel=0.02)
m.box((2.4, 0.5, 0.06), (0, 0, 2.31), 'steel')
stripes(m, -1.0, 1.0, -0.282, 2.4, 2.95, 0.03, w=0.16)
for x in (-1.45, 1.45):
    for z in (2.5, 2.8): m.cyl(0.04, 0.03, (x, -0.27, z), 'steel', rot=(90, 0, 0), seg=6)
# base hazard on the posts (front)
for s in (-1, 1):
    stripes(m, s * 1.375 - 0.15, s * 1.375 + 0.15, -0.282, 0.06, 0.28, 0.03, w=0.1)
finish(m, bounds={'x': (-1.55, 1.55), 'y': (-0.3, 0.3), 'z': (0, 3.0)})
