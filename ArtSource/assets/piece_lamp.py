import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('piece_lamp', seed=87)
# tripod base and pole
for a in (0, 120, 240):
    r = math.radians(a)
    beam_pts = [(0.0, 0.0, 0.7), (0.2 * math.cos(r), 0.2 * math.sin(r), 0.04)]
    m.tube(beam_pts, 0.018, 'steel_dark', seg=5)
    m.cyl(0.03, 0.03, (0.2 * math.cos(r), 0.2 * math.sin(r), 0.015), 'gunmetal', seg=6)
m.cyl(0.03, 1.0, (0, 0, 1.2), 'steel_dark', seg=6)
m.cyl(0.045, 0.1, (0, 0, 0.7), 'steel', seg=6)
# lamp cage head
m.cyl(0.11, 0.05, (0, 0, 1.68), 'steel_dark', seg=8, r2=0.14)
m.cyl(0.11, 0.2, (0, 0, 1.55), 'lamp_warm', seg=8)
for a in range(0, 360, 60):
    r = math.radians(a)
    m.box((0.015, 0.015, 0.24), (0.125 * math.cos(r), 0.125 * math.sin(r), 1.55), 'gunmetal')
m.cyl(0.13, 0.025, (0, 0, 1.42), 'steel_dark', seg=8)
m.cyl(0.13, 0.025, (0, 0, 1.64), 'steel_dark', seg=8)
m.cyl(0.015, 0.14, (0, 0, 1.73), 'steel', seg=6)
m.tube([(0.02, 0.0, 1.0), (0.1, 0.1, 0.5), (0.15, 0.2, 0.05)], 0.008, 'black', seg=4)
m.box((0.08, 0.06, 0.08), (0.0, -0.05, 0.9), 'hazard_yellow') if False else None
finish(m, bounds={'x': (-0.25, 0.25), 'y': (-0.25, 0.25), 'z': (0, 1.8)})
