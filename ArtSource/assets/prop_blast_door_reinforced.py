import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('prop_blast_door_reinforced', seed=72)
# base leaf: identical hinge side and pivot conventions as blast_door_leaf (hinge pins at x=-1.07, leaf centred on x=0)
m.box((2.3, 0.14, 2.25), (0, 0, 1.125), 'steel_dark', bevel=0.03)
m.box((2.0, 0.04, 1.95), (0, -0.09, 1.125), 'steel', bevel=0.02)
# heavy hinges (left side, same z as original but bigger)
for z in (0.35, 1.1, 1.85):
    m.cyl(0.08, 0.34, (-1.07, -0.05, z), 'gunmetal', seg=8)
    m.box((0.3, 0.07, 0.26), (-0.98, -0.07, z), 'gunmetal')
# extra bolted armour plates
for (x, z, w, h) in ((-0.5, 0.62, 0.9, 0.85), (-0.5, 1.7, 0.9, 0.85), (0.55, 1.7, 0.9, 0.85), (0.55, 0.62, 0.9, 0.85)):
    m.box((w, 0.05, h), (x, -0.095, z), 'rust_dark', bevel=0.015)
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.cyl(0.032, 0.03, (x + sx * (w / 2 - 0.07), -0.13, z + sz * (h / 2 - 0.07)), 'steel', rot=(90, 0, 0), seg=6)
# horizontal crossbars (full width, protrude both ends over the leaf edge)
for z in (1.125, 0.2, 2.05):
    m.box((2.26, 0.09, 0.14), (0, -0.1, z), 'steel_dark', bevel=0.012)
    m.box((2.26, 0.03, 0.05), (0, -0.15, z), 'steel')
    for i in range(6):
        m.cyl(0.026, 0.03, (-0.95 + i * 0.38, -0.16, z), 'chrome', rot=(90, 0, 0), seg=6)
# vertical edge strap at latch side
m.box((0.12, 0.08, 2.1), (1.02, -0.1, 1.125), 'gunmetal')
for z in (0.5, 1.2, 1.9):
    m.box((0.28, 0.12, 0.24), (0.98, -0.1, z), 'gunmetal')   # lock-bolt blocks
# wheel
cz, cxw = 1.125, 0.35
m.cyl(0.3, 0.05, (cxw, -0.125, cz), 'gunmetal', rot=(90, 0, 0), seg=14)
for i in range(3):
    m.box((0.62, 0.04, 0.07), (cxw, -0.15, cz), 'steel', rot=(0, i * 60, 0))
m.cyl(0.08, 0.07, (cxw, -0.138, cz), 'rust_light', rot=(90, 0, 0), seg=8)
# hazard plate on back-left upper plate
m.box((0.5, 0.03, 0.3), (-0.5, -0.135, 1.7), 'hazard_yellow')
m.box((0.4, 0.035, 0.22), (-0.5, -0.138, 1.7), 'black')
m.box((0.14, 0.04, 0.14), (-0.5, -0.14, 1.7), 'warning_red', rot=(0, 45, 0))
finish(m, bounds={'x': (-1.15, 1.15), 'y': (-0.175, 0.175), 'z': (0, 2.25)})
