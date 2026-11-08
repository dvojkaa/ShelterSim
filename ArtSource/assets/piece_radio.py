import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('piece_radio', seed=89)
# base plinth
m.box((1.2, 0.7, 0.1), (0, 0, 0.05), 'gunmetal')
# main rack frame
for sx in (-1, 1):
    m.box((0.08, 0.5, 2.0), (sx * 0.55, 0.0, 1.1), 'steel_dark')
m.box((1.18, 0.5, 0.08), (0, 0, 2.14), 'steel_dark')
m.box((1.1, 0.04, 2.0), (0, 0.23, 1.1), 'gunmetal')
# rack units
units = [(0.3, 'army_green_dark'), (0.2, 'steel_dark'), (0.3, 'army_green'), (0.22, 'gunmetal'), (0.3, 'steel_dark'), (0.26, 'army_green_dark')]
z = 0.18
for k, (h, c) in enumerate(units):
    m.box((1.0, 0.44, h - 0.02), (0, -0.0, z + h / 2), c, bevel=0.01)
    m.box((1.0, 0.02, h - 0.04), (0, -0.23, z + h / 2), 'black' if k % 2 else 'gunmetal')
    for sx in (-1, 1):
        m.cyl(0.015, 0.02, (sx * 0.47, -0.25, z + h * 0.85), 'steel', rot=(90, 0, 0), seg=5)
        m.cyl(0.015, 0.02, (sx * 0.47, -0.25, z + h * 0.15), 'steel', rot=(90, 0, 0), seg=5)
    # dials
    n = 3 + (k % 2)
    for i in range(n):
        m.cyl(0.04, 0.04, (-0.35 + i * 0.16, -0.25, z + h * 0.45), 'steel' if i % 2 else 'chrome', rot=(90, 0, 0), seg=8)
        m.box((0.01, 0.045, 0.04), (-0.35 + i * 0.16, -0.27, z + h * 0.45 + 0.015), 'black')
    z += h
# frequency display and meters
m.box((0.5, 0.02, 0.16), (0.1, -0.26, 0.65), 'black') if False else None
m.box((0.36, 0.03, 0.12), (0.22, -0.255, 0.18 + 0.15 + 0.2 + 0.18), 'lamp_warm')
m.box((0.4, 0.02, 0.16), (0.22, -0.248, 0.18 + 0.15 + 0.2 + 0.18), 'black') if False else None
for i in range(5): m.box((0.02, 0.035, 0.06), (0.1 + i * 0.07, -0.275, 0.18 + 0.15 + 0.2 + 0.18), 'black')
# round VU meters
for i, x in enumerate((-0.3, 0.0, 0.3)):
    m.cyl(0.07, 0.03, (x, -0.255, 1.55), 'paint_white', rot=(90, 0, 0), seg=10)
    m.box((0.01, 0.035, 0.05), (x + 0.01, -0.272, 1.57), 'warning_red', rot=(0, 25, 0))
# red power switch and morse key
m.box((0.12, 0.06, 0.08), (-0.35, -0.28, 0.4), 'warning_red', bevel=0.01) if False else None
m.cyl(0.05, 0.05, (0.35, -0.3, 1.1), 'warning_red', rot=(90, 0, 0), seg=10)
m.box((0.3, 0.2, 0.04), (-0.3, -0.45, 1.05), 'steel_dark') if False else None
# antenna mast + whip + dish
m.cyl(0.03, 0.3, (0.0, 0.0, 2.3), 'steel_dark', seg=6)
m.cyl(0.015, 0.55, (0.0, 0.0, 2.7), 'steel', seg=5, r2=0.005)
for z, w in ((2.55, 0.5), (2.65, 0.4), (2.75, 0.3)):
    m.box((w, 0.02, 0.02), (0.0, 0.0, z), 'chrome')
m.box((0.02, 0.02, 0.1), (0.0, 0.0, 2.98), 'warning_red') if False else None
m.cyl(0.03, 0.04, (0.0, 0.0, 2.96), 'warning_red', seg=6)
# status beacon lights on top
for i, (c, x) in enumerate((('warning_red', -0.35), ('lamp_warm', 0.35))):
    m.cyl(0.05, 0.1, (x, 0.0, 2.23), c, seg=8)
# cable bundle down the side
m.tube([(0.5, 0.15, 2.1), (0.52, 0.3, 1.5), (0.5, 0.3, 0.3), (0.3, 0.3, 0.12)], 0.025, 'black', seg=5)
# hazard stripe base
stripes(m, -0.5, 0.5, -0.33, 0.1, 0.18, 0.02, w=0.1)
finish(m, bounds={'x': (-0.6, 0.6), 'y': (-0.35, 0.35), 'z': (0, 3.0)})
