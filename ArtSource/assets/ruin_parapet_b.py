import random
from ss_art import Model, finish
m = Model('ruin_parapet_b', seed=27)
r = random.Random(2702)

# A collapse: tall pier on the left, stairs down into a gap, a lone stub on the right.
COL = 0.125
ROW = 0.09
HEIGHTS = [10, 11, 11, 10, 9, 8, 8, 6, 4, 3, 2, 2, 3, 5, 8, 6]
SKIN = [(-1.0, 0.02), (-0.86, 0.16), (-0.72, -0.02), (-0.56, 0.02), (-0.42, -0.14), (-0.26, -0.2), (-0.14, -0.32),
        (0.04, -0.38), (0.22, -0.4), (0.36, -0.38), (0.5, -0.26), (0.64, -0.18), (0.74, -0.04), (0.88, -0.2), (1.0, -0.22)]


def col_x(k):
    return -1.0 + k * COL


def row_z(n):
    return -0.5 + n * ROW


def skin_at(x):
    for (x0, z0), (x1, z1) in zip(SKIN[:-1], SKIN[1:]):
        if x0 <= x <= x1:
            return z0 + (z1 - z0) * (x - x0) / (x1 - x0)
    return SKIN[-1][1]


def core_outline():
    pts = [(-0.995, -0.5), (0.995, -0.5)]
    for k in range(len(HEIGHTS) - 1, -1, -1):
        z = row_z(HEIGHTS[k]) - 0.012
        pts.append((min(col_x(k + 1), 0.995), z))
        pts.append((max(col_x(k), -0.995), z))
    out = [pts[0], pts[1]]
    for p in pts[2:]:
        if abs(p[0] - out[-1][0]) > 1e-6 or abs(p[1] - out[-1][1]) > 1e-6:
            out.append(p)
    return out


m.poly(core_outline(), 0.24, (0, 0, 0), 'brick_dark')
skin_pts = [(-1.0, -0.5), (1.0, -0.5)] + SKIN[::-1]
for s, c in ((-1, 'plaster_dirty'), (1, 'concrete')):
    m.poly(skin_pts, 0.03, (0, s * 0.135, 0), c)
    m.box((1.98, 0.006, 0.08), (0, s * 0.152, -0.46), 'concrete_dark')
    m.poly([(0.46, -0.42), (0.7, -0.4), (0.78, -0.3), (0.56, -0.26)], 0.004, (0, s * 0.1505, 0), 'concrete_dark')
    m.poly([(-0.7, -0.26), (-0.46, -0.28), (-0.4, -0.2), (-0.62, -0.14)], 0.004, (0, s * 0.1505, 0), 'plaster')


def lay(k0, n_cols, row):
    if any(HEIGHTS[k] <= row for k in range(k0, k0 + n_cols)):
        return
    x = col_x(k0) + n_cols * COL / 2
    z = row_z(row) + ROW / 2
    w = n_cols * COL - 0.012
    top = all(HEIGHTS[k] == row + 1 for k in range(k0, k0 + n_cols))
    c = r.choice(('brick', 'brick', 'brick', 'rust_dark', 'brick_dark'))
    if top:
        m.box((w, 0.25, ROW - 0.012), (x, r.uniform(-0.002, 0.002), z), r.choice(('brick', 'brick', 'rust_dark')))
        return
    skin = max(skin_at(x - w / 2), skin_at(x + w / 2))
    if z + ROW / 2 < skin:
        return
    if r.random() < 0.14:
        return
    for s in (-1, 1):
        m.box((w, 0.012, ROW - 0.012), (x, s * (0.124 + r.uniform(0, 0.002)), z), c)


for row in range(11):
    k = 0
    if row % 2 == 0:
        lay(0, 1, row)
        k = 1
    while k < len(HEIGHTS):
        if k + 1 < len(HEIGHTS) and HEIGHTS[k] > row and HEIGHTS[k + 1] > row:
            lay(k, 2, row)
            k += 2
        else:
            lay(k, 1, row)
            k += 1

# A bed of crumbled mortar and brick in the gap (columns 9-12)
m.blob((0.62, 0.34, 0.16), (0.2, 0, -0.27), 'stone_dark', rough=0.22, subdiv=1, colors=['concrete_dark', 'brick_dark', 'stone_dark'])
for x, y, z, rz, ry in ((0.04, 0.05, -0.2, 25, 12), (0.2, -0.06, -0.24, -15, 30), (0.36, 0.03, -0.22, 20, -24)):
    m.box((0.24, 0.11, 0.075), (x, y, z), 'brick', rot=(0, ry, rz))

# Rusted I-beam end sheared off, sticking out of the pier over the gap: this variant's signature.
BZ = 0.16
m.box((0.6, 0.13, 0.02), (-0.28, 0, BZ + 0.06), 'rust', rot=(0, -6, 0))
m.box((0.6, 0.13, 0.02), (-0.28, 0, BZ - 0.06), 'rust', rot=(0, -6, 0))
m.box((0.6, 0.02, 0.11), (-0.28, 0, BZ), 'rust_dark', rot=(0, -6, 0))

# Rebar fanned out across the gap
for pts in ([(-0.06, 0.05, -0.3), (-0.04, 0.05, 0.02), (0.06, 0.08, 0.2)],
            [(0.14, -0.07, -0.34), (0.16, -0.07, 0.06), (0.28, -0.1, 0.18)],
            [(0.32, 0.06, -0.34), (0.32, 0.06, 0.3), (0.42, 0.11, 0.42)],
            [(0.92, 0.0, -0.1), (0.94, 0.0, 0.24), (0.9, 0.08, 0.34)]):
    m.tube(pts, 0.014, 'rust', seg=5)

finish(m, bounds={'x': (-1.0, 1.0), 'y': (-0.2, 0.2), 'z': (-0.5, 0.5)})
