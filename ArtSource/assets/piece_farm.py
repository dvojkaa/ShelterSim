import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('piece_farm', seed=85)
# raised planter box 2x2, 0.45 tall
m.box((2.0, 2.0, 0.08), (0, 0, 0.04), 'wood_dark')
for s in (-1, 1):
    m.box((2.0, 0.1, 0.45), (0, s * 0.95, 0.25), 'wood', bevel=0.01)
    m.box((0.1, 1.8, 0.45), (s * 0.95, 0, 0.25), 'wood', bevel=0.01)
    m.box((2.0, 0.14, 0.04), (0, s * 0.93, 0.49), 'wood_light')
    m.box((0.14, 1.8, 0.04), (s * 0.93, 0, 0.49), 'wood_light')
m.box((1.8, 1.8, 0.35), (0, 0, 0.25), 'dirt') if False else m.box((1.8, 1.8, 0.4), (0, 0, 0.26), 'dirt')
# rows of leafy vegetables
for r in range(4):
    for c in range(5):
        x = -0.72 + c * 0.36 + (0.12 if r % 2 else 0)
        y = -0.65 + r * 0.43
        if x > 0.8: continue
        kind = (r + c) % 3
        if kind == 0:
            m.blob((0.28, 0.28, 0.22), (x, y, 0.5), 'leaf_green', rough=0.2, colors=['leaf_green', 'leaf_dark', 'leaf_green'], flat_bottom=-0.02)
        elif kind == 1:
            m.cyl(0.1, 0.18, (x, y, 0.55), 'leaf_dark', seg=6, r2=0.02)
            m.blob((0.2, 0.2, 0.15), (x, y, 0.5), 'leaf_green', rough=0.2, colors=['leaf_green', 'spruce_light'])
        else:
            m.blob((0.22, 0.22, 0.14), (x, y, 0.48), 'leaf_green', rough=0.15, colors=['leaf_dry', 'leaf_green'])
            m.blob((0.1, 0.1, 0.1), (x, y, 0.6), 'orange', rough=0.1, subdiv=1)
# grow lamp frame
for sx in (-1, 1):
    m.box((0.05, 0.05, 0.2), (sx * 0.95, 0.9, 0.58), 'steel_dark') if False else None
    m.box((0.05, 0.05, 0.2), (sx * 0.9, 0.9, 0.59), 'steel_dark')
    m.box((0.05, 0.05, 0.2), (sx * 0.9, 0.9, 0.59), 'steel_dark')
for sx in (-1, 1):
    m.box((0.05, 0.05, 0.19), (sx * 0.95, -0.9, 0.59), 'steel_dark')
for sx in (-1, 1):
    m.box((0.05, 0.05, 0.19), (sx * 0.95, 0.9, 0.59), 'steel_dark')
m.box((0.05, 1.85, 0.05), (-0.95, 0.0, 0.67), 'steel')
m.box((0.05, 1.85, 0.05), (0.95, 0.0, 0.67), 'steel')
m.box((1.9, 0.05, 0.05), (0, -0.9, 0.67), 'steel')
m.box((1.9, 0.05, 0.05), (0, 0.9, 0.67), 'steel')
for y in (-0.4, 0.4):
    m.box((1.6, 0.14, 0.04), (0, y, 0.64), 'gunmetal')
    m.box((1.5, 0.1, 0.02), (0, y, 0.61), 'lamp_warm')
finish(m, bounds={'x': (-1.0, 1.0), 'y': (-1.0, 1.0), 'z': (0, 0.7)})
