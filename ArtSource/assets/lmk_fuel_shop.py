import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_fuel_shop', seed=1206)
r = random.Random(1206)
for sx in (-1, 1):
    cx = sx * (1.7 + 6.3 / 2)
    m.box((6.3, 0.3, 2.8), (cx, -0.15, 1.4), 'plaster', bevel=0.02)
    m.box((6.3, 0.34, 0.4), (cx, -0.17, 0.2), 'concrete_dark')
    m.box((6.3, 0.34, 0.12), (cx, -0.17, 3.0), 'concrete')
    # window
    for wx in (2.7, 5.2):
        x = sx * wx
        m.box((2.0, 0.14, 1.5), (x, -0.36, 1.6), 'steel_dark', bevel=0.02)
        m.box((1.76, 0.1, 1.26), (x, -0.4, 1.6), 'glass_dirty')
        m.box((2.2, 0.16, 0.08), (x, -0.4, 0.82), 'concrete')
        for k in range(r.randint(1, 2)):
            m.box((1.8, 0.04, 0.2), (x, -0.5 - 0.01 * k, 1.1 + k * 0.5 + r.uniform(0, 0.2)), r.choice(['wood', 'wood_dark', 'wood_grey']), rot=(0, r.uniform(-12, 12), 0))
    m.box((1.4, 0.03, 0.9), (sx * 7.0, -0.32, 1.3), 'brick_dark')
    m.box((0.12, 0.34, 2.8), (sx * 1.76, -0.17, 1.4), 'concrete_dark')
# sign band over everything
m.box((16.0, 0.3, 0.6), (0, -0.15, 3.3), 'rust_dark', bevel=0.02)
m.box((15.6, 0.05, 0.4), (0, -0.33, 3.3), 'mustard_dark')
for i in range(8):
    m.box((0.9, 0.06, 0.3), (-7.0 + i * 2.0, -0.36, 3.3), 'black' if i % 2 == 0 else 'rust')
m.box((3.4, 0.3, 0.3), (0, -0.15, 2.95), 'concrete_dark')     # lintel
# awning: slanted over the front, above gap height
m.box((16.0, 0.75, 0.1), (0, -0.55, 3.05), 'warning_red', rot=(-10, 0, 0))
for i in range(8):
    m.box((1.0, 0.77, 0.02), (-7.0 + i * 2.0, -0.55, 3.12), 'fabric_white' if i % 2 else 'plaster_dirty', rot=(-10, 0, 0))
for x in (-7.8, -3.0, 3.0, 7.8):
    m.box((0.1, 0.1, 2.9), (x, -0.85, 1.45), 'steel_dark')
# stains / drips
for i in range(8):
    m.box((r.uniform(0.2, 0.6), 0.03, r.uniform(0.6, 1.4)), (r.choice([-1, 1]) * r.uniform(2.0, 7.5), -0.32, r.uniform(1.0, 2.6)), 'rust_dark')
finish(m, bounds={'x': (-8.0, 8.0), 'y': (-1.0, 0.0), 'z': (0, 3.7)})
