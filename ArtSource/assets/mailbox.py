import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('mailbox', seed=38)
m.box((0.1, 0.1, 1.0), (0, 0, 0.5), 'wood', bevel=0.01)
m.box((0.16, 0.16, 0.06), (0, 0, 0.03), 'wood_dark')
m.box((0.2, 0.34, 0.03), (0, 0, 1.02), 'wood_dark')
m.box((0.28, 0.36, 0.2), (0, 0, 1.15), 'plastic_blue', bevel=0.02)
m.cyl(0.14, 0.36, (0, 0, 1.14), 'plastic_blue', rot=(90, 0, 0), seg=10)
m.box((0.28, 0.02, 0.16), (0, -0.18, 1.17), 'steel')
m.box((0.02, 0.1, 0.1), (0.17, -0.1, 1.2), 'red_bright')
m.box((0.28, 0.02, 0.05), (0, -0.19, 1.24), 'steel_dark')
finish(m, bounds={'x': (-0.2, 0.2), 'y': (-0.2, 0.2), 'z': (0, 1.3)})
