import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('furn_bookcase', seed=303)
r = random.Random(303)
W, D, H = 1.0, 0.35, 1.9
# sides, back, top, plinth
for s in (-1, 1):
    m.box((0.04, D, H), (s * 0.47, 0, H / 2), 'wood')
m.box((0.92, 0.02, H - 0.1), (0, 0.165, H / 2), 'wood_dark')
m.box((1.0, D, 0.04), (0, 0, H - 0.02), 'wood_light', bevel=0.01)
m.box((0.92, 0.3, 0.08), (0, -0.01, 0.04), 'wood_dark')
zs = [0.08, 0.5, 0.92, 1.34, 1.74]
for z in zs[1:]:
    m.box((0.92, D - 0.03, 0.03), (0, -0.01, z), 'wood_light' if z != 1.34 else 'wood')
# crooked broken shelf: second collapsed to one side
cols = ['cloth_red', 'fabric_blue', 'leather', 'army_green', 'mustard_dark', 'fabric_grey', 'rust_dark', 'plastic_blue', 'leaf_brown', 'wood_dark']
for si, z in enumerate(zs[:-1]):
    top = zs[si + 1] - 0.015
    base = z + (0.015 if si else 0.04)
    x = -0.43
    while x < 0.43:
        w = r.uniform(0.045, 0.08)
        h = r.uniform(0.24, 0.34) if top - base > 0.38 else 0.28
        h = min(h, top - base - 0.03)
        if r.random() < (0.18 + 0.12 * si) and si != 0:
            x += r.uniform(0.08, 0.2); continue  # gap
        if si == 3 and x > 0.1:
            break
        m.box((w, 0.22, h), (x + w / 2, -0.015, base + h / 2), r.choice(cols))
        x += w + 0.003
    # leaning books at the end of shelf row
    if si in (0, 2, 3):
        lw = 0.04
        x = min(x, 0.3)
        m.box((lw, 0.22, 0.28), (x + 0.07, -0.015, base + 0.14), r.choice(cols), rot=(0, 22, 0))
        m.box((lw, 0.22, 0.26), (x + 0.11, -0.015, base + 0.13), r.choice(cols), rot=(0, 28, 0))
# top shelf mostly bare: stack of flat books and a jar
m.box((0.28, 0.22, 0.04), (-0.25, -0.015, 1.775), 'leather')
m.box((0.24, 0.2, 0.035), (-0.25, -0.015, 1.815), 'cloth_red', rot=(0, 0, 6))
m.cyl(0.05, 0.14, (0.25, 0, 1.82), 'glass_dirty', seg=8)
# toppled books on top of the bookcase and fallen scattered at foot (inside footprint)
m.box((0.2, 0.26, 0.04), (0.3, 0, H + 0.0 - 0.0), 'fabric_blue', rot=(0, 0, 0)) if False else None
for i, (x, c) in enumerate(((-0.3, 'cloth_red'), (0.0, 'army_green'), (0.3, 'mustard_dark'))):
    pass
# empty gap shelf 3 right: toppled books lying flat
m.box((0.22, 0.2, 0.04), (0.3, -0.015, 1.375), 'leather_dark', rot=(0, 0, 4))
m.box((0.2, 0.18, 0.04), (0.31, -0.015, 1.415), 'fabric_grey', rot=(0, 0, -7))
# diagonal broken shelf tilting down on one side (shelf 1)
m.box((0.5, 0.3, 0.03), (0.2, -0.01, 0.53), 'wood_dark', rot=(0, 12, 0))
# fallen books at floor in front
m.box((0.2, 0.28, 0.04), (-0.2, -0.0, 0.12), 'cloth_red', rot=(0, 0, 15))
# grime: dark soot on side and a crack
m.box((0.012, 0.2, 0.5), (0.494, 0.0, 1.5), 'wood_grey')
m.box((0.5, 0.012, 0.1), (-0.2, -0.169, 1.85), 'wood_dark')
finish(m, bounds={'x': (-0.5, 0.5), 'y': (-0.175, 0.175), 'z': (0, 1.9)})
