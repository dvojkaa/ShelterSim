import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('furn_shop_shelf', seed=903)
r = random.Random(903)
# uprights (each end: two posts front/back of centre spine), base, top
for sx in (-1, 1):
    m.box((0.05, 0.5, 1.6), (sx * 0.95, 0, 0.8), 'steel_dark')
    m.box((0.08, 0.56, 0.06), (sx * 0.95, 0, 0.03), 'rust_dark')
    m.box((0.08, 0.56, 0.05), (sx * 0.95, 0, 1.64), 'steel')
m.box((1.9, 0.02, 1.4), (0, 0, 0.9), 'steel_dark')   # centre back panel
m.box((1.94, 0.04, 0.08), (0, 0, 1.66), 'rust')       # header
m.box((1.9, 0.5, 0.05), (0, 0, 0.1), 'steel_dark')
levels = [0.12, 0.5, 0.88, 1.26]
cans = ['red_bright', 'mustard', 'rust_light', 'army_green', 'plastic_blue', 'paint_white', 'orange']
boxc = ['cloth_red', 'mustard_dark', 'fabric_blue', 'paint_green', 'fabric_stain', 'paint_white', 'leather']
bent = 2
for li, z in enumerate(levels):
    for side in (-1, 1):
        y0 = side * 0.14
        tilt = (0, 5, 0) if (li == bent and side == -1) else (0, 0, 0)
        if li > 0:
            m.box((1.9, 0.24, 0.03), (0, side * 0.15, z - 0.015), 'steel', rot=tilt)
            # lip and price-tag strip
            m.box((1.9, 0.02, 0.04), (0, side * 0.27, z + 0.0), 'rust_dark', rot=tilt)
            m.box((1.7, 0.01, 0.025), (0, side * 0.282, z + 0.005), 'fabric_white', rot=tilt)
        top = z + 0.015 if li == 0 else z
        x = -0.88
        while x < 0.88:
            roll = r.random()
            if roll < 0.45:
                x += r.uniform(0.1, 0.3); continue
            if (li == bent and side == -1) and x > 0:
                break
            zz = top + (0.0 if not (li == bent and side == -1) else 0.0)
            ty = math.radians(5) * x if (li == bent and side == -1) else 0
            zz += ty
            if roll < 0.75:
                h = r.uniform(0.11, 0.15); rad = 0.04
                m.cyl(rad, h, (x, y0 + r.uniform(-0.03, 0.03), zz + h / 2), r.choice(cans), seg=7)
                m.cyl(rad + 0.002, 0.015, (x, y0, zz + h - 0.007), 'chrome', seg=7) if False else None
                x += 0.1
                if r.random() < 0.4:
                    m.cyl(rad, h, (x, y0, zz + h / 2), r.choice(cans), seg=7); x += 0.1
            else:
                w, h = r.uniform(0.14, 0.24), r.uniform(0.12, 0.24)
                m.box((w, 0.14, h), (x + w / 2, y0, zz + h / 2), r.choice(boxc), rot=(0, 0, r.uniform(-4, 4)))
                x += w + 0.05
# bent shelf: collapsed corner + fallen can
m.box((0.5, 0.24, 0.03), (0.7, -0.15, 0.78), 'steel_dark', rot=(0, -28, 0))
m.cyl(0.04, 0.12, (0.45, -0.1, 0.06), 'rust_light', rot=(0, 85, 20), seg=7)
m.box((0.2, 0.15, 0.14), (0.7, 0.0, 0.2), 'fabric_stain', rot=(0, 0, 14)) if False else None
finish(m, bounds={'x': (-1.0, 1.0), 'y': (-0.3, 0.3), 'z': (0, 1.7)})
