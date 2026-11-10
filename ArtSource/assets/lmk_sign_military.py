import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

from _lmk_sign import sign_base
m = Model('lmk_sign_military', seed=1201)
sign_base(m, 'army_green_dark')
# star
pts = []
for i in range(10):
    a = math.radians(90 + i * 36); rr = 0.6 if i % 2 == 0 else 0.25
    pts.append((rr * math.cos(a), rr * math.sin(a)))
m.poly(pts, 0.05, (-1.2, -0.12, 2.55), 'fabric_white')
m.cyl(0.8, 0.04, (-1.2, -0.115, 2.55), 'fabric_white', rot=(90, 0, 0), seg=14, r2=0.8) if False else None
# stencil glyph blocks: RELAY-ish
x = -0.35
for i, w in enumerate((0.3, 0.22, 0.3, 0.26, 0.3)):
    m.box((w, 0.05, 0.5), (x + w / 2, -0.11, 2.85), 'fabric_white')
    m.box((w * 0.45, 0.06, 0.2), (x + w / 2, -0.115, 2.85 + (0.1 if i % 2 else -0.1)), 'army_green_dark')
    x += w + 0.12
m.box((1.9, 0.05, 0.12), (0.6, -0.11, 2.3), 'hazard_yellow')
m.box((1.4, 0.05, 0.12), (0.35, -0.11, 2.05), 'fabric_white')
m.box((0.5, 0.05, 0.12), (1.45, -0.11, 2.05), 'hazard_yellow')
finish(m, bounds={'x': (-2.0, 2.0), 'y': (-0.3, 0.4), 'z': (0, 3.6)})
