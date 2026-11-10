import random
from ss_art import Model, finish
m = Model('piece_foundation_brick', seed=41)
r = random.Random(4101)
H = 0.45
T = 0.012            # side brick relief
m.box((1.98, 1.98, H - 0.006), (0, 0, (H - 0.006) / 2), 'plaster_dirty')   # mortar core, inset 0.01

cols = ['brick', 'brick', 'brick_dark', 'brick', 'rust_dark']
# side courses: 3 courses of 0.15, bricks 0.5 long, running bond, flush to +-1 at the tile edges
for c in range(3):
    z = 0.075 + c * 0.15
    xs = [(-1.0, 0.0)] if False else None
    if c % 2 == 0:
        spans = [(-1 + 0.5 * i, -1 + 0.5 * (i + 1)) for i in range(4)]
    else:
        spans = [(-1, -0.75)] + [(-0.75 + 0.5 * i, -0.25 + 0.5 * i) for i in range(3)] + [(0.75, 1)]
    for a, b in spans:
        for s in (-1, 1):
            col = r.choice(cols)
            m.box((b - a - 0.014, T, 0.136), ((a + b) / 2, s * (1 - T / 2), z), col)
            col = r.choice(cols)
            lo, hi = max(a, -0.99) , min(b, 0.99)
            if hi - lo > 0.05:
                m.box((T, hi - lo - 0.014 if (a > -1 and b < 1) else hi - lo - 0.007, 0.136), (s * (1 - T / 2), (lo + hi) / 2, z), col)
# top: laid running-bond pavers 0.5 x 0.25, flat at z=H, 6 mm proud of the core
PH = 0.006
for row in range(8):
    y = -1 + 0.25 * (row + 0.5)
    if row % 2 == 0:
        spans = [(-1 + 0.5 * i, -1 + 0.5 * (i + 1)) for i in range(4)]
    else:
        spans = [(-1, -0.75)] + [(-0.75 + 0.5 * i, -0.25 + 0.5 * i) for i in range(3)] + [(0.75, 1)]
    for a, b in spans:
        col = r.choice(['brick', 'brick_dark', 'brick', 'rust_dark', 'brick'])
        m.box((b - a - 0.014, 0.236, PH), ((a + b) / 2, y, H - PH / 2), col)
finish(m, bounds={'x': (-1, 1), 'y': (-1, 1), 'z': (0, H)})
