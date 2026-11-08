import random
from ss_art import Model, finish
m = Model('ruin_parapet_a', seed=26)
r = random.Random(2601)

# The wall is literally built of courses: the broken top is the toothed stair-step of real brickwork.
COL = 0.125     # half a brick
ROW = 0.09      # course height
# Courses standing in each 0.125 m column, left to right. Two peaks, a deep notch between them.
HEIGHTS = [6, 7, 7, 9, 10, 10, 10, 8, 5, 4, 4, 6, 7, 10, 9, 7]
# Plaster skin line along X: breaks off below the brick and well below the peaks.
SKIN = [(-1.0, -0.08), (-0.86, -0.02), (-0.74, -0.14), (-0.6, 0.04), (-0.48, -0.03), (-0.36, -0.16), (-0.2, -0.1), (-0.06, -0.26),
        (0.1, -0.3), (0.26, -0.2), (0.4, -0.25), (0.54, -0.06), (0.66, 0.0), (0.8, -0.12), (0.9, -0.05), (1.0, -0.12)]


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
for s, c in ((-1, 'concrete'), (1, 'plaster_dirty')):
    m.poly(skin_pts, 0.03, (0, s * 0.135, 0), c)
    m.box((1.98, 0.006, 0.14), (0, s * 0.152, -0.43), 'concrete_dark')
    # Spalled chips in the plaster
    m.poly([(-0.84, -0.3), (-0.62, -0.28), (-0.56, -0.2), (-0.74, -0.16), (-0.86, -0.22)], 0.004, (0, s * 0.1505, 0), 'concrete_dark')
    m.poly([(0.42, -0.42), (0.62, -0.4), (0.7, -0.3), (0.5, -0.28)], 0.004, (0, s * 0.1505, 0), 'plaster')


def lay(k0, n_cols, row):
    """One brick spanning n_cols half-brick columns, in course `row`, if the course stands there."""
    if any(HEIGHTS[k] <= row for k in range(k0, k0 + n_cols)):
        return
    x = col_x(k0) + n_cols * COL / 2
    z = row_z(row) + ROW / 2
    w = n_cols * COL - 0.012
    top = all(HEIGHTS[k] == row + 1 for k in range(k0, k0 + n_cols))
    c = r.choice(('brick', 'brick', 'brick', 'rust_dark', 'brick_dark'))
    if top:
        # Topmost course: a whole header brick through the wall thickness, so the broken top shows brick from above.
        m.box((w, 0.25, ROW - 0.012), (x, r.uniform(-0.002, 0.002), z), r.choice(('brick', 'brick', 'rust_dark')))
        return
    skin = max(skin_at(x - w / 2), skin_at(x + w / 2))
    if z + ROW / 2 < skin:
        return      # hidden behind the plaster
    if r.random() < 0.12:
        return      # missing brick: dark mortar shows
    for s in (-1, 1):
        m.box((w, 0.012, ROW - 0.012), (x, s * (0.124 + r.uniform(0, 0.002)), z), c)


for row in range(11):
    k = 0
    if row % 2:
        lay(0, 1, row)
        k = 1
    while k < len(HEIGHTS):
        if k + 1 < len(HEIGHTS) and HEIGHTS[k] > row and HEIGHTS[k + 1] > row:
            lay(k, 2, row)
            k += 2
        else:
            lay(k, 1, row)
            k += 1

# Broken concrete coping still bedded on the left peak (columns 4-6 stand level at z=0.4).
m.poly([(-0.5, 0.39), (-0.08, 0.39), (-0.13, 0.43), (-0.18, 0.47), (-0.3, 0.45), (-0.36, 0.49), (-0.46, 0.46)], 0.32, (0, 0, 0), 'concrete_light')

# A loose brick slumped into the notch
m.box((0.24, 0.11, 0.075), (0.1, 0.03, -0.09), 'brick', rot=(4, -12, 18))
m.box((0.24, 0.11, 0.075), (0.3, -0.04, 0.03), 'brick_dark', rot=(-5, 16, -32))

# Rebar: rusty rods, each growing out of the masonry and bending away on its own.
for pts in ([(-0.62, 0.0, 0.0), (-0.62, 0.0, 0.3), (-0.72, 0.03, 0.42)],
            [(0.06, -0.05, -0.2), (0.07, -0.05, 0.18), (0.17, -0.1, 0.3)],
            [(0.24, 0.06, -0.2), (0.25, 0.06, 0.32), (0.21, 0.13, 0.44)],
            [(0.92, -0.04, 0.0), (0.93, -0.04, 0.32), (0.97, -0.11, 0.46)],
            [(-0.02, 0.08, -0.2), (-0.03, 0.08, 0.08), (-0.14, 0.13, 0.18)]):
    m.tube(pts, 0.014, 'rust', seg=5)

finish(m, bounds={'x': (-1.0, 1.0), 'y': (-0.2, 0.2), 'z': (-0.5, 0.5)})
