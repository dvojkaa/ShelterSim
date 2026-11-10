import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('furn_sofa', seed=301)
F, FD, FS = 'fabric_blue', 'fabric_grey', 'fabric_stain'
# legs
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((0.08, 0.08, 0.14), (sx * 0.9, sy * 0.33, 0.07), 'wood_dark')
# base frame
m.box((1.96, 0.84, 0.18), (0, 0, 0.23), 'wood_dark')
m.box((1.96, 0.02, 0.14), (0, -0.43, 0.24), 'wood')
# arms
for s in (-1, 1):
    m.box((0.24, 0.88, 0.5), (s * 0.88, 0, 0.42), F, bevel=0.03)
    m.box((0.26, 0.9, 0.1), (s * 0.87, 0, 0.69), FD, bevel=0.03)
# arm tear on left
m.box((0.02, 0.3, 0.16), (-0.985, -0.1, 0.5), 'fabric_stain', rot=(0, 0, 0))
# backrest
m.box((1.6, 0.26, 0.5), (0, 0.31, 0.6), F, bevel=0.03)
for i, x in enumerate((-0.53, 0, 0.53)):
    m.box((0.5, 0.2, 0.4), (x, 0.28, 0.6), FD if i != 1 else F, bevel=0.04, rot=(-8, 0, 0))
# seat cushions: left and right present, centre missing
for x, c in ((-0.53, F), (0.53, FD)):
    m.box((0.52, 0.56, 0.16), (x, -0.08, 0.40), c, bevel=0.035)
# centre: bare frame, springs and stuffing
m.box((0.52, 0.56, 0.02), (0, -0.08, 0.33), 'cloth_dark')
for i in range(5):
    m.cyl(0.04, 0.05, (-0.2 + i * 0.1, -0.08 + (i % 2) * 0.12 - 0.06, 0.37), 'steel_dark', seg=6)
m.tube([(-0.22, -0.3, 0.34), (-0.1, -0.25, 0.44), (0.05, -0.3, 0.36), (0.2, -0.22, 0.45)], 0.012, 'rust', seg=4)
m.blob((0.3, 0.22, 0.14), (0.08, 0.1, 0.4), 'bone', rough=0.2, colors=['bone', 'fabric_white'])
m.blob((0.22, 0.18, 0.12), (-0.18, 0.2, 0.4), 'fabric_white', rough=0.25)
# stuffing out of right cushion tear and backrest tear
m.box((0.26, 0.02, 0.05), (0.56, -0.37, 0.46), 'cloth_dark', rot=(0, 0, 8))
m.blob((0.2, 0.12, 0.1), (0.58, -0.38, 0.45), 'fabric_white', rough=0.25)
m.box((0.3, 0.02, 0.18), (-0.5, 0.17, 0.66), 'cloth_dark', rot=(-8, 0, -15))
m.blob((0.22, 0.12, 0.14), (-0.5, 0.15, 0.68), 'bone', rough=0.25)
# stains and burn
m.box((0.22, 0.2, 0.012), (-0.58, -0.1, 0.485), FS)
m.box((0.14, 0.14, 0.012), (0.6, 0.05, 0.485), 'leather_dark')
m.box((0.2, 0.02, 0.2), (0.45, 0.17, 0.7), FS, rot=(-8, 0, 0))
finish(m, bounds={'x': (-1.0, 1.0), 'y': (-0.45, 0.45), 'z': (0, 0.85)})
