import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('blast_door_leaf', seed=63)
cx = 0.0
m.box((2.3, 0.14, 2.25), (0, 0, 1.125), 'steel_dark', bevel=0.03)
m.box((2.0, 0.04, 1.95), (0, -0.09, 1.125), 'steel', bevel=0.02)
m.box((2.2, 0.03, 0.14), (0, -0.115, 0.2), 'hazard_yellow') if False else None
# ribs
for z in (0.5, 1.75): m.box((1.9, 0.03, 0.08), (0, -0.115, z), 'steel_dark')
m.box((0.08, 0.03, 1.9), (0.0, -0.115, 1.125), 'steel_dark') if False else None
# hinges (left side)
for z in (0.35, 1.1, 1.85):
    m.cyl(0.07, 0.3, (-1.07, -0.05, z), 'gunmetal', seg=8)
    m.box((0.3, 0.05, 0.22), (-1.0, -0.07, z), 'gunmetal')
# bolts along the edge
for i in range(10):
    z = 0.2 + i * 0.22
    m.cyl(0.03, 0.03, (-0.95, -0.115, z), 'steel_dark', rot=(90, 0, 0), seg=6) if False else None
for i in range(9):
    x = -0.9 + i * 0.22
    m.cyl(0.028, 0.03, (x, -0.12, 2.12), 'steel_dark', rot=(90, 0, 0), seg=6)
    m.cyl(0.028, 0.03, (x, -0.12, 0.14), 'steel_dark', rot=(90, 0, 0), seg=6)
for z in (0.4, 0.8, 1.2, 1.6):
    m.cyl(0.028, 0.03, (-1.0, -0.12, z + 0.1), 'steel_dark', rot=(90, 0, 0), seg=6)
    m.cyl(0.028, 0.03, (1.0, -0.12, z + 0.1), 'steel_dark', rot=(90, 0, 0), seg=6)
# wheel
cz = 1.125; cxw = 0.35
m.cyl(0.32, 0.05, (cxw, -0.12, cz), 'steel_dark', rot=(90, 0, 0), seg=14)
m.cyl(0.24, 0.05, (cxw, -0.17, cz), 'steel', rot=(90, 0, 0), seg=14) if False else None
import math
for i in range(6):
    a = math.radians(i * 60)
    m.box((0.5, 0.05, 0.06), (cxw, -0.17, cz), 'chrome' if False else 'steel', rot=(0, i * 60, 0)) if False else None
for i in range(3):
    m.box((0.62, 0.03, 0.06), (cxw, -0.15, cz), 'steel', rot=(0, i * 60, 0))
m.cyl(0.07, 0.06, (cxw, -0.145, cz), 'gunmetal', rot=(90, 0, 0), seg=8)
m.tube([(cxw, -0.14, cz)] * 1 + [(cxw, -0.14, cz)], 0.001, 'steel') if False else None
m.cyl(0.32, 0.03, (cxw, -0.17, cz), 'steel', rot=(90, 0, 0), seg=14, r2=0.3) if False else None
# lock-bolt arms
m.box((0.5, 0.05, 0.18), (1.0, -0.1, 1.125), 'gunmetal') if False else None
m.box((0.2, 0.05, 0.4), (0.95, -0.1, 1.125), 'gunmetal')
# hazard sign
m.box((0.5, 0.03, 0.3), (-0.55, -0.115, 1.6), 'hazard_yellow')
m.box((0.4, 0.035, 0.22), (-0.55, -0.12, 1.6), 'black')
m.box((0.14, 0.04, 0.14), (-0.55, -0.125, 1.6), 'warning_red', rot=(0, 45, 0))
finish(m, bounds={'x': (-1.15, 1.15), 'y': (-0.18, 0.18), 'z': (0, 2.25)})
