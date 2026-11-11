import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_factory_facade', seed=2207)
r = random.Random(2207)
GAP = 1.7
# --- wall slabs left/right of the gap (y -0.3..0), plinth, panel seams
for sx in (-1, 1):
    x0 = sx * (GAP + 8.3 / 2)
    m.box((8.3, 0.3, 7.0), (x0, -0.15, 3.5), 'steel_dark')
    m.box((8.3, 0.34, 0.5), (x0, -0.17, 0.25), 'concrete_dark')
    # riveted panel rows: horizontal seams + lighter panel plates
    for row, z in enumerate((1.3, 2.7)):
        for i in range(4):
            px = sx * (GAP + 0.2 + 1.0 + i * 2.0)
            m.box((1.9, 0.05, 1.2), (px, -0.32, z), r.choice(['steel', 'steel_dark', 'rust_dark']))
            for rx in (0.8,):
                for rz in (0.5,):
                    m.box((0.07, 0.05, 0.07), (px + rx, -0.37, z + rz), 'gunmetal')
    # grimy clerestory windows, high along the top (z 5.0..6.3)
    for i in range(4):
        wx = sx * (GAP + 1.2 + i * 2.0 + 0.0)
        m.box((1.7, 0.12, 1.2), (wx, -0.36, 5.5), 'gunmetal')
        broken = (i + (0 if sx < 0 else 1)) % 3 == 0
        m.box((1.5, 0.08, 1.0), (wx, -0.42, 5.5), 'glass' if broken else 'glass_dirty')
        m.box((0.05, 0.1, 1.0), (wx, -0.45, 5.5), 'gunmetal')
        m.box((1.5, 0.1, 0.05), (wx, -0.45, 5.5), 'gunmetal')
        if broken:   # jagged hole + shards
            m.poly([(-0.4, -0.3), (0.2, -0.45), (0.45, 0.1), (0.1, 0.4), (-0.25, 0.2)], 0.04, (wx, -0.47, 5.5), 'black', rot=(90, 0, 0))
# --- header above gap (>= 2.8) is part of the slab strip
m.box((2 * GAP, 0.3, 4.2), (0, -0.15, 4.9), 'steel_dark')
m.box((2 * GAP + 0.6, 0.45, 0.3), (0, -0.25, 3.0), 'steel')     # lintel
# hazard-stripe surround (outside the gap and above 2.8)
for k in range(8):
    z = 0.2 + k * 0.4
    col = 'hazard_yellow' if k % 2 == 0 else 'black'
    for sx in (-1, 1):
        m.box((0.3, 0.1, 0.4), (sx * (GAP + 0.15), -0.35, z + 0.2 if z + 0.4 <= 2.8 else 2.6), col) if z + 0.4 <= 2.8 else None
for k in range(9):
    m.box((0.4, 0.1, 0.3), (-1.6 + k * 0.4, -0.5, 3.0), 'hazard_yellow' if k % 2 == 0 else 'black')
# --- sign band
m.box((16.0, 0.12, 1.3), (0, -0.36, 4.0), 'rust_dark')
m.box((15.6, 0.06, 1.0), (0, -0.42, 4.0), 'mustard_dark')
m.box((16.0, 0.14, 0.08), (0, -0.37, 4.7), 'black'); m.box((16.0, 0.14, 0.08), (0, -0.37, 3.3), 'black')
letters = [-6.6, -5.4, -4.2, -3.0, -1.9, 1.9, 3.0, 4.2, 5.4, 6.6]
for i, lx in enumerate(letters):   # stencil blocks, faded
    c = 'fabric_white' if i % 3 else 'plaster_dirty'
    m.box((0.7, 0.05, 0.6), (lx, -0.47, 4.0), c)
    m.box((0.2, 0.06, 0.62), (lx + r.choice([-0.12, 0.12]), -0.49, 4.0), 'mustard_dark')
    m.box((0.5, 0.06, 0.12), (lx, -0.49, 4.0 + r.choice([-0.2, 0, 0.2])), 'mustard_dark')
# --- buttresses every 4 m
for bx in (-9.7, -6, -2, 2, 6, 9.7):
    m.box((0.45, 0.5, 7.0), (bx, -0.55, 3.5), 'steel')
    m.box((0.6, 0.6, 0.25), (bx, -0.55, 0.125), 'concrete_dark')
    m.box((0.6, 0.6, 0.2), (bx, -0.55, 6.9), 'steel_dark')
    for z in (1.0, 2.0, 4.8, 5.8):
        m.box((0.3, 0.05, 0.08), (bx, -0.82, z), 'gunmetal')
    for k in range(5):
        m.box((0.47, 0.52, 0.2), (bx, -0.55, 0.6 + k * 0.4), 'hazard_yellow' if k % 2 == 0 else 'black') if bx in (-2, 2) and k < 0 else None
# --- pipes / ducts along upper wall
m.box((16.0, 0.4, 0.5), (0, -0.55, 6.55), 'steel_dark')       # duct
for x in range(-7, 8, 2):
    m.box((0.1, 0.44, 0.54), (x, -0.55, 6.55), 'gunmetal')
m.tube([(-9.5, -0.4, 6.0), (9.5, -0.4, 6.0)], 0.1, 'rust', seg=6)
m.tube([(-9.5, -0.5, 6.25), (-3, -0.5, 6.25), (-3, -0.5, 4.8)], 0.07, 'steel', seg=6)
m.tube([(9.5, -0.52, 4.9), (4, -0.52, 4.9), (4, -0.52, 6.0)], 0.08, 'steel', seg=6)
# --- lamp above the gap
m.box((0.12, 0.5, 0.06), (0, -0.55, 3.55), 'gunmetal')
m.box((0.3, 0.3, 0.06), (0, -0.8, 3.5), 'gunmetal')
m.cyl(0.14, 0.25, (0, -0.8, 3.35), 'lamp_warm', seg=8)
m.cyl(0.2, 0.06, (0, -0.8, 3.52), 'gunmetal', seg=8)
# --- rust streaks (below clerestory, buttress feet)
for i in range(8):
    m.box((r.uniform(0.15, 0.4), 0.03, r.uniform(0.8, 2.2)), (r.choice([-1, 1]) * r.uniform(2.3, 9.4), -0.34, r.uniform(1.2, 5.0)), r.choice(['rust', 'rust_dark']))
m.box((0.4, 0.04, 0.6), (-5.9, -0.9, 0.3), 'rust_dark')
finish(m, bounds={'x': (-10.0, 10.0), 'y': (-1.2, 0.0), 'z': (0, 7.0)})
