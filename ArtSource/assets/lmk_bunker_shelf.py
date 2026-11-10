import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_bunker_shelf', seed=1304)
r = random.Random(1304)
# uprights at the ends and the middle; shelves run along Y (6 m), 1 m deep in X
ys = (-2.95, 0, 2.95)
for y in ys:
    for sx in (-1, 1):
        m.box((0.08, 0.08, 3.0), (sx * 0.46, y, 1.5), 'steel_dark')
    m.box((0.9, 0.05, 0.05), (0, y, 0.06), 'steel_dark')
for z in (0.3, 1.0, 1.7, 2.4):
    m.box((1.0, 6.0, 0.06), (0, 0, z), 'wood_grey' if z > 0.4 else 'steel_dark', bevel=0.01)
    m.box((0.04, 6.0, 0.04), (0.0, 0, z - 0.04), 'rust_dark')
# diagonal bracing on both sides
for sx in (-1, 1):
    m.tube([(sx * 0.5, -2.9, 0.3), (sx * 0.5, 0, 2.9)], 0.015, 'rust', seg=4) if False else None
cols = ['mustard', 'army_green', 'wood', 'plastic_blue', 'rust_light', 'paint_white', 'warning_red']
for z in (0.33, 1.03, 1.73, 2.43):
    y = -2.85
    while y < 2.4:
        k = r.random()
        if k < 0.35:
            w = r.uniform(0.35, 0.6); hh = r.uniform(0.25, 0.45)
            m.box((r.uniform(0.5, 0.8), w, hh), (r.uniform(-0.05, 0.05), y + w / 2, z + hh / 2), r.choice(['wood', 'wood_light', 'army_green', 'cardboard' if False else 'wood_grey']), bevel=0.015, rot=(0, 0, r.uniform(-4, 4)))
            m.box((0.04, w * 0.6, 0.05), (0.43, y + w / 2, z + hh * 0.7), 'paint_white') if False else None
            y += w + 0.08
        elif k < 0.8:
            n = r.randint(3, 5)
            for j in range(n):
                m.cyl(0.07, 0.17, (r.uniform(-0.35, 0.35), y + 0.09 + j * 0.0, z + 0.115), r.choice(['rust_light', 'steel', 'mustard', 'chrome', 'warning_red']), seg=7) if False else None
            for j in range(n):
                xx = -0.3 + (j % 3) * 0.3
                yy = y + 0.1 + (j // 3) * 0.2
                m.cyl(0.07, 0.17, (xx, yy, z + 0.115), r.choice(['rust_light', 'steel', 'mustard', 'paint_white']), seg=7)
            y += 0.5
        else:
            y += r.uniform(0.2, 0.5)   # gap
# one shelf leaning/fallen box
m.box((0.5, 0.4, 0.3), (0.15, 1.3, 0.15), 'wood_dark', rot=(0, 0, 20), bevel=0.01)
m.box((1.0, 0.04, 0.02), (0, 0, 0.5), 'rust_dark') if False else None
finish(m, bounds={'x': (-0.5, 0.5), 'y': (-3.0, 3.0), 'z': (0, 3.1)})
