import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('hedge', seed=35)
m.box((2.0, 0.7, 0.3), (0, 0, 0.15), 'bark', bevel=0.01)
m.box((1.96, 0.76, 0.5), (0, 0, 0.55), 'leaf_dark', bevel=0.05, taper=(0.98, 0.98))
m.box((1.96, 0.7, 0.45), (0, 0, 1.0), 'leaf_green', bevel=0.06, taper=(0.93, 0.85))
for i in range(6):
    x = -0.8 + i * 0.32
    m.blob((0.4, 0.36, 0.22), (x, 0, 1.16), 'leaf_green', rough=0.18, colors=['leaf_green', 'leaf_dark', 'leaf_green'])
for i in range(4):
    m.blob((0.3, 0.2, 0.25), (-0.7 + i * 0.5, -0.3, 0.6 + (i % 2) * 0.2), 'leaf_green', rough=0.2, colors=['leaf_green', 'leaf_dark'])
finish(m, bounds={'x': (-1, 1), 'y': (-0.4, 0.4), 'z': (0, 1.3)})
