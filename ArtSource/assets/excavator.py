import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('excavator', seed=73)
Y = 'hazard_yellow'
# tracks (long in Y)
for s in (-1, 1):
    m.box((0.7, 3.6, 0.55), (s * 0.95, 0, 0.35), 'rubber', bevel=0.05)
    m.box((0.5, 3.4, 0.2), (s * 0.95, 0, 0.67), 'steel_dark')
    for yy in (-1.5, 1.5): m.cyl(0.4, 0.6, (s * 0.95, yy, 0.4), 'gunmetal', rot=(0, 90, 0), seg=10)
    for yy in (-0.9, -0.3, 0.3, 0.9): m.cyl(0.18, 0.55, (s * 0.95, yy, 0.22), 'steel_dark', rot=(0, 90, 0), seg=8)
m.box((1.4, 2.0, 0.3), (0, 0, 0.55), 'gunmetal')
# turntable + body
m.cyl(0.9, 0.12, (0, 0, 0.75), 'steel_dark', seg=12)
m.box((2.2, 2.5, 0.8), (0, -0.25, 1.15), Y, bevel=0.04)
m.box((2.2, 1.2, 0.5), (0, -1.2, 1.15), Y, bevel=0.04) if False else None
# counterweight at rear (-Y)
m.box((2.2, 0.6, 0.9), (0, -1.75, 1.3), 'steel_dark', bevel=0.04)
# engine hood
m.box((1.7, 1.2, 0.35), (-0.15, -0.75, 1.75), Y, bevel=0.04, taper=(0.95, 0.9))
m.box((0.2, 0.2, 0.7), (-0.6, -1.0, 2.1), 'gunmetal') if False else None
m.cyl(0.07, 0.6, (-0.65, -1.0, 2.1), 'gunmetal', seg=6)
# cab on the left (-x) front
m.box((1.0, 1.15, 1.2), (-0.55, 0.45, 2.0), 'steel_dark', bevel=0.03)
m.box((0.9, 0.04, 0.85), (-0.55, 1.03, 2.1), 'glass')
m.box((0.04, 1.0, 0.8), (-1.03, 0.45, 2.1), 'glass')
m.box((0.04, 1.0, 0.8), (-0.07, 0.45, 2.1), 'glass') if False else None
m.box((1.15, 1.3, 0.08), (-0.55, 0.45, 2.65), Y)
m.box((0.5, 0.04, 0.7), (-0.55, -0.15, 2.1), 'glass') if False else None
for x in (-1.0, -0.1): m.box((0.05, 0.05, 1.0), (x, 1.0, 2.1), Y)
m.box((0.08, 0.2, 0.1), (-0.95, 1.1, 2.55), 'lamp_warm') if False else None
# boom: from the body pivot up & forward
def bar(p0, p1, w, d, x, c):
    beam_yz(m, p0, p1, w, d, x, c)
bar((0.7, 1.2), (2.3, 3.3), 0.4, 0.4, 0.25, Y)       # boom (box in YZ plane)
bar((0.7, 1.2), (2.3, 3.3), 0.1, 0.46, 0.25, 'steel_dark') if False else None
# hydraulic cylinders
bar((0.5, 1.9), (1.7, 2.9), 0.12, 0.12, 0.25 + 0.3, 'steel')
bar((0.2, 1.3), (1.2, 2.6), 0.1, 0.1, 0.25 - 0.3, 'steel')
# stick down to the front
bar((2.3, 3.3), (3.3, 1.4), 0.3, 0.3, 0.25, Y)
bar((2.6, 3.0), (3.2, 1.9), 0.08, 0.08, 0.25 - 0.22, 'steel')
# bucket
m.box((0.8, 0.8, 0.12), (0.25, 3.05, 1.0), 'steel_dark', rot=(25, 0, 0))
m.box((0.8, 0.08, 0.7), (0.25, 2.9, 0.9), 'steel_dark', rot=(-15, 0, 0))
m.box((0.8, 0.08, 0.55), (0.25, 3.45, 0.8), 'steel_dark', rot=(-60, 0, 0)) if False else None
for s in (-1, 1): m.box((0.06, 0.6, 0.6), (0.25 + s * 0.4, 3.05, 0.8), 'steel_dark', rot=(-25, 0, 0))
for i in range(4): m.box((0.06, 0.12, 0.15), (-0.05 + i * 0.2, 3.62, 0.62), 'steel') if False else None
m.cyl(0.1, 0.4, (0.25, 3.3, 1.35), 'gunmetal', rot=(0, 90, 0), seg=6) if False else None
# lamps and railing
m.box((0.14, 0.1, 0.1), (-0.95, 1.05, 2.7), 'lamp_warm') if False else None
finish(m, bounds={'x': (-1.3, 1.3), 'y': (-3.5, 3.5), 'z': (0, 3.6)})
