import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_fuel_tank', seed=1204)
R = 2.5
m.cyl(2.8, 0.3, (0, 0, 0.15), 'concrete_dark', seg=16)                         # base ring
m.cyl(R, 3.5, (0, 0, 2.05), 'steel', seg=16)
m.cyl(R, 0.3, (0, 0, 3.9), 'rust_light', seg=16, r2=R * 0.8)
m.cyl(R * 0.8, 0.25, (0, 0, 4.15), 'steel_dark', seg=16, r2=R * 0.6)
m.cyl(0.5, 0.3, (0, 0, 4.4), 'steel_dark', seg=8)
for z in (0.6, 1.8, 3.0):
    m.cyl(R + 0.05, 0.18, (0, 0, z), 'rust_dark', seg=16)
for i, a in enumerate(range(0, 360, 45)):
    m.box((0.5, 0.05, 2.8), (2.5 * math.cos(math.radians(a)), 2.5 * math.sin(math.radians(a)), 2.0), 'rust' if i % 2 else 'steel_dark', rot=(0, 0, a + 90)) if a % 90 else None
# warning band
m.cyl(R + 0.06, 0.4, (0, 0, 2.4), 'hazard_yellow', seg=16)
m.cyl(R + 0.07, 0.15, (0, 0, 2.4), 'black', seg=16)
# ladder on -Y
for sx in (-1, 1):
    m.box((0.06, 0.08, 4.0), (sx * 0.28, -2.7, 2.1), 'steel_dark')
for i in range(13):
    m.box((0.56, 0.05, 0.05), (0, -2.7, 0.35 + i * 0.29), 'rust')
for z in (2.0, 3.0):
    m.box((0.7, 0.05, 0.05), (0, -3.0, z), 'steel_dark')
# catwalk rail on top
m.cyl(R * 0.6 + 0.1, 0.05, (0, 0, 4.0), 'steel_dark', seg=16, caps=False) if False else None
# pipes
m.tube([(1.8, -1.8, 0.5), (3.0, -1.8, 0.5), (3.0, -0.5, 0.5), (3.0, -0.5, 1.5)], 0.14, 'steel_dark', seg=6)
m.tube([(0.0, 2.5, 0.6), (0.0, 3.0, 0.6), (0.0, 3.0, 0.15), (-2.0, 3.0, 0.15)], 0.12, 'rust_dark', seg=6)
m.cyl(0.25, 0.08, (3.0, -0.5, 1.0), 'warning_red', rot=(0, 90, 0), seg=8)
m.cyl(0.3, 0.3, (-1.3, -2.2, 0.5), 'steel', rot=(90, 0, 0), seg=8)
finish(m, bounds={'x': (-3.1, 3.2), 'y': (-3.2, 3.2), 'z': (0, 4.8)})
