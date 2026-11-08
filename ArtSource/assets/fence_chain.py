import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('fence_chain', seed=34)
for s in (-1, 1):
    m.cyl(0.05, 2.2, (s * 1.43, 0, 1.1), 'steel', seg=8)
    m.cyl(0.045, 0.06, (s * 1.43, 0, 2.17), 'steel_dark', seg=8)
m.tube([(-1.43, 0, 2.1), (1.43, 0, 2.1)], 0.03, 'steel', seg=6)
m.tube([(-1.43, 0, 0.12), (1.43, 0, 0.12)], 0.02, 'steel', seg=6)
m.box((2.9, 0.04, 0.02), (0, 0, 0.5), 'steel_dark') if False else None
x0, x1, z0, z1 = -1.42, 1.42, 0.14, 2.08
for s in (1, -1):
    k = -4.0
    while k < 6:
        seg = clip_diag(x0, x1, z0, z1, k, s)
        if seg and math.hypot(seg[1][0]-seg[0][0], seg[1][1]-seg[0][1]) > 0.05:
            beam(m, seg[0], seg[1], 0.012, 0.012, 0.0, 'steel_dark')
        k += 0.3
# privacy / warning sign on the fence (blank panel)
m.box((0.8, 0.02, 0.45), (0.2, -0.03, 1.5), 'hazard_yellow')
m.box((0.7, 0.025, 0.35), (0.2, -0.035, 1.5), 'steel') if False else None
m.cyl(0.1, 0.01, (0.2, -0.045, 1.5), 'warning_red', rot=(90, 0, 0), seg=3)
finish(m, bounds={'x': (-1.5, 1.5), 'y': (-0.05, 0.05), 'z': (0, 2.2)})
