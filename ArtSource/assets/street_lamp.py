import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('street_lamp', seed=36)
P = -0.55
m.cyl(0.2, 0.25, (P, 0, 0.125), 'steel_dark', seg=8, r2=0.14)
m.cyl(0.1, 0.12, (P, 0, 0.3), 'steel', seg=8)
m.cyl(0.07, 6.0, (P, 0, 3.3), 'steel_dark', seg=8, r2=0.05)
m.tube([(P, 0, 6.2), (P + 0.1, 0, 6.38), (P + 0.5, 0, 6.43), (P + 0.9, 0, 6.38)], 0.04, 'steel_dark', seg=6)
m.box((0.14, 0.14, 0.12), (P, 0, 6.2), 'steel')
m.box((0.58, 0.3, 0.05), (0.45, 0, 6.43), 'steel_dark')
m.box((0.5, 0.24, 0.1), (0.45, 0, 6.35), 'lamp_warm')
m.box((0.34, 0.28, 0.08), (0.4, 0, 6.46), 'steel')
finish(m, bounds={'x': (-0.8, 0.8), 'y': (-0.25, 0.25), 'z': (0, 6.5)})
