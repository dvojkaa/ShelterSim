import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_mil_tower', seed=1204)
def legpos(z):
    s = 2.0 - (2.0 - 0.45) * min(z, 8.0) / 8.0   # half-spread 2.0 at base -> 0.45 at platform... 
    return s
# four legs: slanted boxes from (+-2,+-2,0) to (+-0.9,+-0.9,8)
top = 0.9
for sx in (-1, 1):
    for sy in (-1, 1):
        a = (sx * 2.0, sy * 2.0, 0.15); b = (sx * top, sy * top, 8.0)
        m.tube([a, b], 0.09, 'steel_dark', seg=6)
        m.box((0.5, 0.5, 0.2), (a[0], a[1], 0.1), 'concrete_dark')
# bracing per level
levels = [0, 2.0, 4.0, 6.0, 8.0]
def hs(z): return 2.0 + (top - 2.0) * z / 8.0
for z in levels[1:]:
    h = hs(z)
    for (p, q) in (((-h, -h), (h, -h)), ((h, -h), (h, h)), ((h, h), (-h, h)), ((-h, h), (-h, -h))):
        m.tube([(p[0], p[1], z), (q[0], q[1], z)], 0.04, 'steel', seg=4)
for z0, z1 in zip([0.15] + levels[1:-1], levels[1:]):
    h0, h1 = hs(z0), hs(z1)
    for sx in (-1, 1):   # X faces diagonal
        m.tube([(sx * h0, -h0, z0), (sx * h1, h1, z1)], 0.03, 'rust', seg=4)
        m.tube([(sx * h0, h0, z0), (sx * h1, -h1, z1)], 0.03, 'rust', seg=4)
    for sy in (-1, 1):
        m.tube([(-h0, sy * h0, z0), (h1, sy * h1, z1)], 0.03, 'rust', seg=4)
        m.tube([(h0, sy * h0, z0), (-h1, sy * h1, z1)], 0.03, 'rust', seg=4)
# platform 6x6 at 8 m
m.box((6.0, 6.0, 0.15), (0, 0, 8.0), 'steel_dark', bevel=0.02)
m.box((5.6, 5.6, 0.04), (0, 0, 8.1), 'rust_dark')
for sx in (-1, 1):
    m.box((6.0, 0.05, 0.05), (0, sx * 2.95, 9.0), 'hazard_yellow')
    m.box((0.05, 6.0, 0.05), (sx * 2.95, 0, 9.0), 'hazard_yellow')
    m.box((6.0, 0.04, 0.05), (0, sx * 2.95, 8.5), 'steel')
    m.box((0.04, 6.0, 0.05), (sx * 2.95, 0, 8.5), 'steel')
    for sy in (-1, 1):
        m.box((0.06, 0.06, 1.0), (sx * 2.95, sy * 2.95, 8.55), 'steel_dark')
        for t in (-1, 0, 1):
            m.box((0.06, 0.06, 1.0), (sx * 2.95, t * 1.5, 8.55), 'steel_dark')
            m.box((0.06, 0.06, 1.0), (t * 1.5, sx * 2.95, 8.55), 'steel_dark')
# equipment cabin on platform
m.box((1.8, 1.4, 1.4), (-1.6, 1.4, 8.85), 'army_green', bevel=0.03)
m.box((1.9, 1.5, 0.1), (-1.6, 1.4, 9.6), 'army_green_dark')
m.box((0.5, 0.05, 0.8), (-1.6, 0.68, 8.75), 'gunmetal')
# mast
m.cyl(0.14, 8.0, (0, 0, 12.0), 'steel', seg=8, r2=0.07)
for z in (9.0, 11.0, 13.0):
    for s in (-1, 1):
        m.tube([(0, 0, z), (s * 1.6, 0, 8.2)], 0.02, 'steel_dark', seg=4)
        m.tube([(0, 0, z), (0, s * 1.6, 8.2)], 0.02, 'steel_dark', seg=4)
m.box((2.4, 0.06, 0.06), (0, 0, 14.5), 'steel')
m.box((0.06, 1.6, 0.06), (0, 0, 13.4), 'steel')
m.cyl(0.04, 1.4, (0, 0, 15.3), 'steel', seg=5)
m.cyl(0.07, 0.07, (0, 0, 16.0), 'red_bright', seg=6)
# aerial dish
m.cyl(0.9, 0.3, (0, -0.8, 11.4), 'paint_white', rot=(90 + 0, 0, 0), seg=12, r2=0.05)
m.cyl(0.06, 0.8, (0, -0.4, 11.4), 'steel_dark', rot=(90, 0, 0), seg=5)
m.cyl(0.05, 0.4, (0, -1.1, 11.4), 'steel', rot=(90, 0, 0), seg=5)
# ladder on one leg line
for k in range(26):
    z = 0.4 + k * 0.3
    h = hs(z)
    m.box((0.5, 0.04, 0.04), (0, -h, z), 'rust')
finish(m, bounds={'x': (-3.0, 3.0), 'y': (-3.0, 3.0), 'z': (0, 16.2)})
