import random
from ss_art import Model, finish
m = Model('ruin_doorway', seed=28)
r = random.Random(2803)

OPEN_X, OPEN_Z = 0.86, 2.2
TOOTH = 0.18    # two courses: the toothing that outlines a wall torn out of the masonry around it
ROW = 0.09

# Right post is hit harder than the left. Outer x per tooth, bottom to top.
RIGHT = [1.03, 0.97, 1.05, 0.98, 1.03, 0.95, 1.0, 0.94, 0.99, 0.95, 1.04, 0.98, 1.02]
LEFT = [-1.04, -0.98, -1.05, -1.0, -1.04, -0.97, -1.03, -0.99, -1.05, -0.98, -1.03, -1.0, -1.05]
# Column tops across the lintel, from +x to -x, each 0.15 wide.
TOPS = [2.52, 2.7, 2.61, 2.43, 2.61, 2.79, 2.7, 2.88, 2.88, 2.97, 2.88, 2.97, 2.97, 2.88]
COL_X = [1.05 - 0.15 * i for i in range(len(TOPS) + 1)]


def col_top(x):
    for i, t in enumerate(TOPS):
        if COL_X[i + 1] <= x <= COL_X[i]:
            return t
    return TOPS[-1]


def core_outline():
    # From the right foot, up the toothing, over the stepped top, down the left toothing.
    pts = []
    for i, x in enumerate(RIGHT):
        pts += [(x, i * TOOTH), (x, (i + 1) * TOOTH)]
    for i, t in enumerate(TOPS):
        pts += [(COL_X[i], t), (COL_X[i + 1], t)]
    for i in range(len(LEFT) - 1, -1, -1):
        x = LEFT[i]
        pts += [(x, (i + 1) * TOOTH), (x, i * TOOTH)]
    return pts


# The arch stops 2-4 cm behind the jamb liner: its inner faces are buried, nothing coplanar with the timber.
INNER = [(-OPEN_X - 0.02, 0.0), (-OPEN_X - 0.02, OPEN_Z + 0.04), (OPEN_X + 0.02, OPEN_Z + 0.04), (OPEN_X + 0.02, 0.0)]
m.poly(core_outline() + INNER, 0.2, (0, 0, 0), 'brick_dark')

# Plaster skin, sharp-edged. Missing over the right lintel and along the right post.
SKIN_OUT = [(0.97, 0.0), (0.97, 0.32), (0.92, 0.46), (0.95, 0.8), (0.9, 1.02), (0.93, 1.4), (0.895, 1.62), (0.895, 2.3),
            (0.74, 2.3), (0.6, 2.44), (0.46, 2.34), (0.3, 2.5), (0.1, 2.46), (-0.04, 2.66), (-0.28, 2.74), (-0.56, 2.84), (-0.82, 2.78),
            (-0.96, 2.82), (-0.96, 2.3), (-0.94, 1.88), (-0.97, 1.42), (-0.93, 0.92), (-0.97, 0.44), (-0.96, 0.0)]
for s, c in ((-1, 'plaster_dirty'), (1, 'concrete')):
    m.poly(SKIN_OUT + INNER, 0.026, (0, s * 0.113, 0), c)
    m.poly([(-0.72, 2.46), (-0.34, 2.42), (-0.24, 2.56), (-0.52, 2.62)], 0.004, (0, s * 0.1275, 0), 'concrete_dark')
    m.poly([(-0.96, 1.1), (-0.92, 1.06), (-0.915, 1.3), (-0.95, 1.36)], 0.004, (0, s * 0.1275, 0), 'plaster')


def skin_top(x):
    line = [(-0.96, 2.82), (-0.82, 2.78), (-0.56, 2.84), (-0.28, 2.74), (-0.04, 2.66), (0.1, 2.46), (0.3, 2.5), (0.46, 2.34), (0.6, 2.44), (0.74, 2.3), (1.05, 2.3)]
    for (x0, z0), (x1, z1) in zip(line[:-1], line[1:]):
        if x0 <= x <= x1:
            return z0 + (z1 - z0) * (x - x0) / (x1 - x0)
    return 2.3


# Exposed coursework where the plaster is gone, on both faces.
BW = 0.24
for row in range(9):
    z = 2.26 + row * ROW + ROW / 2
    off = BW / 2 if row % 2 else 0.0
    for i in range(8):
        x = -0.1 + off + i * (BW + 0.012)
        x0, x1 = x - BW / 2, x + BW / 2
        if x1 > 1.04 or x0 < -1.04:
            continue
        top = min(col_top(x0), col_top(x1), col_top(x))
        if z + ROW / 2 > top - 0.006 or z + ROW / 2 < min(skin_top(x0), skin_top(x1)):
            continue
        if r.random() < 0.12:
            continue
        c = r.choice(('brick', 'brick', 'brick', 'rust_dark'))
        for s in (-1, 1):
            m.box((BW, 0.012, ROW - 0.012), (x, s * 0.104, z), c)
# Toothing bricks down the battered right post, between the skin edge and the outer tooth.
for i, tx in enumerate(RIGHT[:12]):
    for j in (0, 1):
        z = i * TOOTH + (j + 0.5) * ROW
        x0 = 0.9
        w = tx - x0 - 0.01 - (0.02 if j else 0.0)
        if z < 0.34 or r.random() < 0.18:
            continue
        for s in (-1, 1):
            m.box((w, 0.012, ROW - 0.012), (x0 + w / 2, s * 0.104, z), r.choice(('brick', 'brick', 'rust_dark')))

# Timber jambs and lintel: charred and cracked, the clean inner edge of the opening. Their y sits
# inside the +-0.14 bound with overlays allowed to reach it.
for sx, c in ((-1, 'wood_grey'), (1, 'wood_dark')):
    m.box((0.05, 0.27, OPEN_Z), (sx * (OPEN_X + 0.025), 0, OPEN_Z / 2), c)
m.box((1.82, 0.27, 0.1), (0, 0, OPEN_Z + 0.05), 'wood_dark')
for s in (-1, 1):
    # Cracked end of the lintel: a burnt wedge laid on both faces
    m.poly([(0.3, 2.205), (0.4, 2.205), (0.46, 2.295), (0.32, 2.295)], 0.004, (0, s * 0.137, 0), 'black')

# Concrete corner chunks at the feet, outside the opening.
m.poly([(0.92, 0.0), (1.05, 0.0), (1.03, 0.18), (0.95, 0.22)], 0.27, (0, 0, 0), 'concrete_dark')
m.poly([(-1.05, 0.0), (-0.92, 0.0), (-0.92, 0.26), (-1.02, 0.34)], 0.27, (0, 0, 0), 'concrete')

# Bent rebar out of the broken lintel (upwards only; nothing droops into the opening).
for pts in ([(0.62, 0.05, 2.42), (0.62, 0.05, 2.82), (0.72, 0.08, 2.95)],
            [(0.92, -0.06, 2.4), (0.94, -0.06, 2.78), (1.03, -0.08, 2.9)],
            [(0.27, 0.07, 2.5), (0.27, 0.07, 2.93)],
            [(0.46, -0.08, 2.46), (0.48, -0.08, 2.76), (0.38, -0.11, 2.86)]):
    m.tube(pts, 0.013, 'rust', seg=5)

# Rusty hinge plates left behind on the post, door long gone
for z in (0.5, 1.8):
    m.box((0.1, 0.008, 0.14), (-0.92, -0.135, z), 'rust')
    m.cyl(0.012, 0.004, (-0.92, -0.137, z), 'gunmetal', rot=(90, 0, 0), seg=5)

# Opening clearance proof: fail the build if anything enters it (the arch n-gons are concave, so verts and edges only).
eps = 1e-4
def inside(v):
    return abs(v[0]) < OPEN_X - eps and eps < v[2] < OPEN_Z - eps
bad = 0
bm = m.default.bm
for v in bm.verts:
    bad += inside(v.co)
for e in bm.edges:
    a, b = e.verts[0].co, e.verts[1].co
    for k in range(1, 8):
        bad += inside(a.lerp(b, k / 8))
print(f"ASSET_INFO ruin_doorway opening check: {bad} samples inside |x|<{OPEN_X}, z<{OPEN_Z}")
assert bad == 0

finish(m, bounds={'x': (-1.05, 1.05), 'y': (-0.14, 0.14), 'z': (0, 3.0)})
