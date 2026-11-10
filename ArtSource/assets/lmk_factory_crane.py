import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('lmk_factory_crane', seed=1111)
L = 4.7
# four corner legs (open underneath), base plates, hazard bands
for sx in (-1, 1):
    for sy in (-1, 1):
        x, y = sx * L, sy * L
        m.box((0.5, 0.5, 0.15), (x, y, 0.075), 'concrete_dark')
        m.box((0.36, 0.36, 3.85), (x, y, 2.075), 'mustard_dark', bevel=0.02)
        for k in range(4):
            c = 'hazard_yellow' if k % 2 == 0 else 'black'
            m.box((0.39, 0.39, 0.2), (x, y, 0.35 + k * 0.2), c)
        m.box((0.5, 0.5, 0.12), (x, y, 3.95), 'steel_dark')
        for dx in (-0.2, 0.2):
            for dy in (-0.2, 0.2):
                m.cyl(0.03, 0.06, (x + dx, y + dy, 0.18), 'steel', seg=5)
# diagonal braces (in the plane of each side wall, high up only)
def brace(a, b, axis, fixed):
    import math
    (u0, z0), (u1, z1) = a, b
    ln = math.hypot(u1 - u0, z1 - z0); ang = math.degrees(math.atan2(z1 - z0, u1 - u0))
    if axis == 'x':
        m.box((ln, 0.08, 0.08), ((u0 + u1) / 2, fixed, (z0 + z1) / 2), 'rust', rot=(0, -ang, 0))
    else:
        m.box((0.08, ln, 0.08), (fixed, (u0 + u1) / 2, (z0 + z1) / 2), 'rust', rot=(ang, 0, 0))
for s in (-1, 1):
    brace((-L + 0.2, 3.9), (-L + 1.2, 3.0), 'x', s * L) ; brace((L - 0.2, 3.9), (L - 1.2, 3.0), 'x', s * L)
    brace((-L + 0.2, 3.9), (-L + 1.2, 3.0), 'y', s * L) ; brace((L - 0.2, 3.9), (L - 1.2, 3.0), 'y', s * L)
# runway beams along Y on both sides
for sx in (-1, 1):
    m.box((0.45, 9.8, 0.5), (sx * L, 0, 4.2), 'mustard', bevel=0.02)
    m.box((0.55, 9.8, 0.08), (sx * L, 0, 4.49), 'steel_dark')
    for i in range(10):
        m.box((0.46, 0.4, 0.51), (sx * L, -4.5 + i, 4.2), 'hazard_yellow' if i % 2 == 0 else 'black') if i in (0, 9) else None
# bridge beam along X, with trolley, hoist and hook
yb = 0.8
m.box((9.8, 0.5, 0.6), (0, yb, 4.9), 'mustard', bevel=0.02)
m.box((9.8, 0.62, 0.08), (0, yb, 5.24), 'steel_dark')
for sx in (-1, 1):
    m.box((0.6, 0.7, 0.5), (sx * L, yb, 4.9), 'gunmetal')       # end trucks
for i in range(12):
    m.box((0.3, 0.52, 0.5), (-4.4 + i * 0.8, yb, 4.9), 'hazard_yellow' if i % 2 == 0 else 'black') if i in (2, 3, 8, 9) else None
tx = 1.2
m.box((1.0, 1.0, 0.45), (tx, yb, 5.5), 'rust', bevel=0.03)        # trolley
m.box((0.7, 0.7, 0.4), (tx, yb, 5.9), 'steel_dark')
m.cyl(0.3, 0.7, (tx, yb, 5.75), 'steel_dark', rot=(0, 90, 0), seg=10) if False else None
m.tube([(tx, yb, 5.3), (tx, yb, 2.3)], 0.025, 'gunmetal', seg=5)                       # hoist chain
for dx in (-0.06, 0.06):
    m.tube([(tx + dx, yb + 0.02, 5.3), (tx + dx, yb + 0.02, 2.6)], 0.02, 'steel_dark', seg=5)
m.box((0.35, 0.3, 0.4), (tx, yb, 2.55), 'mustard', bevel=0.02)       # hook block
m.cyl(0.04, 0.2, (tx, yb, 2.25), 'steel', seg=6)
m.tube([(tx, yb, 2.2), (tx, yb, 1.95), (tx + 0.18, yb, 1.78), (tx + 0.1, yb, 1.6)], 0.05, 'steel', seg=6)   # hook
# cab/controller on a pendant
m.box((0.3, 0.2, 0.3), (-3.0, yb - 0.4, 3.6), 'army_green_dark') if False else None
m.tube([(-3.0, yb, 4.6), (-3.1, yb - 0.3, 3.4)], 0.015, 'rubber', seg=4)
m.box((0.2, 0.15, 0.28), (-3.1, yb - 0.3, 3.2), 'army_green_dark')
m.cyl(0.04, 0.03, (-3.1, yb - 0.4, 3.25), 'red_bright', rot=(90, 0, 0), seg=6)
finish(m, bounds={'x': (-5.0, 5.0), 'y': (-5.0, 5.0), 'z': (0, 6.2)})
