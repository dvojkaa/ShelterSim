import os, sys, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
# Intact brick wall: same brick size/bond/colours as _m27_wall_damage.brick, nothing missing.
m = Model('piece_wall_brick', seed=2690)
rng = random.Random(2690)
cw, ch = .25, .2
for i in range(8):
    m.box((cw, .10, 3.0), (-1 + cw * (i + .5), 0, 1.5), 'plaster_dirty')
for r in range(15):
    z = r * ch + ch / 2
    off = .125 if r % 2 else 0
    for i in range(-1, 8):
        x0 = max(-1 + i * cw + off, -.99)
        x1 = min(-1 + i * cw + off + cw, .99)
        if x1 - x0 < .06:
            continue
        c = rng.choice(['brick', 'brick', 'brick', 'brick_dark'])
        m.box(((x1 - x0) - .02, .17, ch - .03), ((x0 + x1) / 2, 0, z), c)
finish(m, bounds={'x': (-1, 1), 'y': (-.1, .1), 'z': (0, 3)})
