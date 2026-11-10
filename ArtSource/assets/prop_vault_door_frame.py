import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('prop_vault_door_frame', seed=74)
# frame: posts |x| 0.9..1.3, lintel z 2.2..2.6, depth 0.5
for s in (-1, 1):
    m.box((0.38, 0.5, 2.6), (s * 1.108, 0, 1.3), 'concrete_dark', bevel=0.02)
    m.box((0.08, 0.5, 2.2), (s * 0.94, 0, 1.1), 'steel_dark')
    for z in (0.3, 0.9, 1.5, 2.1):
        m.cyl(0.04, 0.03, (s * 1.105, -0.232, z), 'steel', rot=(90, 0, 0), seg=6)
m.box((2.59, 0.5, 0.4), (0, 0, 2.4), 'concrete_dark', bevel=0.02)
m.box((1.8, 0.5, 0.06), (0, 0, 2.17), 'steel_dark')
stripes(m, -0.9, 0.9, -0.228, 2.28, 2.52, 0.03, w=0.14)
stripes(m, 0.94, 1.26, -0.228, 0.05, 0.3, 0.03, w=0.1)
stripes(m, -1.26, -0.94, -0.228, 0.05, 0.3, 0.03, w=0.1)
finish(m, bounds={'x': (-1.3, 1.3), 'y': (-0.25, 0.25), 'z': (0, 2.6)})
