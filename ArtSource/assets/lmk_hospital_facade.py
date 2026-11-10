import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('lmk_hospital_facade', seed=1106)
r = random.Random(1106)
# wall slabs either side of the 3.4 x 2.8 gap, lintel above it
for sx in (-1, 1):
    cx = sx * (1.7 + 6.3 / 2)
    m.box((6.3, 0.2, 4.3), (cx, -0.1, 2.15), 'plaster', bevel=0.02)
    m.box((6.3, 0.22, 0.45), (cx, -0.11, 0.225), 'concrete_dark')            # plinth
    m.box((6.3, 0.24, 0.12), (cx, -0.12, 4.3), 'concrete')                    # cornice
m.box((3.4, 0.2, 1.5), (0, -0.1, 3.55), 'plaster', bevel=0.02)
m.box((3.6, 0.28, 0.25), (0, -0.14, 2.88), 'concrete_dark')                   # lintel beam
m.box((3.4, 0.24, 0.12), (0, -0.12, 4.3), 'concrete')
for sx in (-1, 1):
    m.box((0.18, 0.28, 2.8), (sx * 1.79, -0.14, 1.4), 'concrete_dark')       # jambs
# stains and damage
for i in range(14):
    x = r.choice([-1, 1]) * r.uniform(2.0, 7.7)
    m.box((r.uniform(0.3, 1.0), 0.03, r.uniform(0.6, 1.8)), (x, -0.215, r.uniform(1.5, 3.8)), r.choice(['plaster_dirty', 'plaster_dirty', 'rust_dark']))
for sx in (-1, 1):
    m.box((1.6, 0.03, 0.9), (sx * 5.2, -0.215, 0.9), 'brick_dark', rot=(0, 0, 0))   # exposed brick where plaster fell
    m.box((0.5, 0.03, 0.5), (sx * 2.5, -0.215, 3.5), 'brick')
# windows
for sx in (-1, 1):
    for wx in (2.8, 4.8, 6.8):
        x = sx * wx
        m.box((1.5, 0.12, 1.4), (x, -0.16, 1.9), 'concrete_dark', bevel=0.02)       # frame
        m.box((1.2, 0.1, 1.1), (x, -0.2, 1.9), 'glass')
        m.box((1.7, 0.1, 0.1), (x, -0.22, 1.2), 'concrete')                          # sill
        n = r.randint(2, 3)
        for k in range(n):
            m.box((1.5, 0.05, 0.2), (x + r.uniform(-0.05, 0.05), -0.27 - 0.01 * k, 1.45 + k * 0.4 + r.uniform(0, 0.1)), r.choice(['wood', 'wood_grey', 'wood_dark']), rot=(0, r.uniform(-14, 14), 0))
        if wx == 4.8:   # shards of a broken pane instead of boards
            pass
# red cross sign above the gap
h, w = 0.2, 0.5
m.box((1.6, 0.1, 1.1), (0, -0.3, 3.7), 'paint_white', bevel=0.02)
m.poly([(-w, -h), (-h, -h), (-h, -w), (h, -w), (h, -h), (w, -h), (w, h), (h, h), (h, w), (-h, w), (-h, h), (-w, h)], 0.06, (0, -0.37, 3.7), 'red_bright')
# awning: canopy sloping out over the gap, with posts at the ends only
m.box((16.0, 1.0, 0.12), (0, -0.65, 3.0), 'paint_green', rot=(-8, 0, 0))
for sx in (-1, 1):
    m.box((16.0, 0.1, 0.18), (0, -1.12, 2.86), 'paint_green') if sx == 1 else None
for i in range(8):
    m.box((1.0, 1.02, 0.02), (-7.5 + i * 2.0, -0.65, 3.07), 'fabric_white' if i % 2 else 'paint_green', rot=(-8, 0, 0))
for x in (-7.8, -3.0, 3.0, 7.8):
    m.box((0.1, 0.1, 2.9), (x, -1.1, 1.45), 'steel_dark')
    m.box((0.1, 0.1, 0.5), (x, -1.1, 0.25), 'rust')
m.box((0.12, 0.8, 0.12), (-3.0, -0.65, 3.65), 'steel_dark', rot=(-20, 0, 0)) if False else None
# rubble at the base, left and right
for sx in (-1, 1):
    m.blob((1.2, 0.6, 0.4), (sx * 2.2, -0.5, 0.15), 'concrete', rough=0.2, flat_bottom=0.0)
finish(m, bounds={'x': (-8.0, 8.0), 'y': (-1.2, 0.0), 'z': (0, 4.5)})
