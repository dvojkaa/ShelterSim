import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_fuel_canopy', seed=1203)
r = random.Random(1203)
ZR = 4.5
m.box((32.0, 12.0, 0.3), (0, 0, ZR), 'concrete', bevel=0.02)                 # roof slab
m.box((32.6, 12.6, 0.1), (0, 0, ZR + 0.2), 'steel_dark')                       # roof cap
# fascia with faded stripes
for i in range(16):
    c = 'paint_white' if i % 2 else 'warning_red'
    for y, ys in ((-6.15, -1), (6.15, 1)):
        m.box((2.0, 0.12, 0.7), (-15.0 + i * 2.0, y, ZR + 0.05), c if r.random() > 0.2 else 'plaster_dirty')
for x in (-16.15, 16.15):
    m.box((0.12, 12.4, 0.7), (x, 0, ZR + 0.05), 'concrete_dark')
# beams under roof
for y in (-5.8, 0, 5.8):
    m.box((32.0, 0.4, 0.4), (0, y, ZR - 0.35), 'steel_dark')
for x in (-14.0, 14.0):
    m.box((0.5, 12.0, 0.4), (x, 0, ZR - 0.35), 'steel_dark')
# light strips, mostly dead
for x in (-10, -5, 5, 10):
    for y in (-3, 3):
        m.box((3.0, 0.3, 0.1), (x, y, ZR - 0.2), 'steel')
        m.box((2.8, 0.2, 0.05), (x, y, ZR - 0.27), 'lamp_warm' if (x, y) == (5, -3) else 'glass_dirty')
# columns
for x in (-14.0, 14.0):
    for y in (-5.0, 5.0):
        m.box((0.7, 0.7, ZR - 0.4), (x, y, (ZR - 0.4) / 2), 'concrete_light', bevel=0.03)
        m.box((0.78, 0.78, 0.9), (x, y, 0.45), 'hazard_yellow' if y < 0 else 'concrete_dark')
        m.box((0.9, 0.9, 0.2), (x, y, ZR - 0.5), 'steel_dark')
        m.box((0.8, 0.8, 0.12), (x, y, 0.06), 'concrete_dark')
        m.box((0.3, 0.04, 1.0), (x - 0.1, y - 0.37 if y < 0 else y + 0.37, 2.2), 'rust_dark')
finish(m, bounds={'x': (-16.4, 16.4), 'y': (-6.4, 6.4), 'z': (0, 4.9)})
