import math, random
import bmesh
from ss_art import Model, finish
from mathutils import Matrix
m = Model('piece_bed', seed=33)
P = m.default

def sheet(part, xs, ys, top, thick, colorfn):
    """Closed lumpy slab: grid of top heights, underside thick below, per-quad colour."""
    t = bmesh.new(); t.loops.layers.uv.new('UVMap')
    up = [[t.verts.new((x, y, top(x, y))) for y in ys] for x in xs]
    dn = [[t.verts.new((x, y, top(x, y) - thick)) for y in ys] for x in xs]
    nx, ny = len(xs), len(ys)
    tops = []
    for i in range(nx - 1):
        for j in range(ny - 1):
            f = t.faces.new((up[i][j], up[i + 1][j], up[i + 1][j + 1], up[i][j + 1]))
            tops.append((f, i, j))
            t.faces.new((dn[i][j + 1], dn[i + 1][j + 1], dn[i + 1][j], dn[i][j]))
    def side(a, b, c, d):
        t.faces.new((a, b, c, d))
    for i in range(nx - 1):
        side(dn[i][0], dn[i + 1][0], up[i + 1][0], up[i][0])
        side(up[i][ny - 1], up[i + 1][ny - 1], dn[i + 1][ny - 1], dn[i][ny - 1])
    for j in range(ny - 1):
        side(up[0][j], up[0][j + 1], dn[0][j + 1], dn[0][j])
        side(dn[nx - 1][j], dn[nx - 1][j + 1], up[nx - 1][j + 1], up[nx - 1][j])
    bmesh.ops.recalc_face_normals(t, faces=t.faces)
    base = colorfn(-1, -1)
    part._paint(t, t.faces, base)
    for f, i, j in tops:
        part._paint(t, [f], colorfn(i, j))
    part._commit(t, Matrix.Identity(4))

rng = random.Random(4471)

# --- Salvaged welded tube frame
FRAME = 'steel_dark'
LX, LY = 0.455, 0.955
for x in (-LX, LX):
    m.box((0.04, 1.93, 0.04), (x, 0, 0.21), FRAME)
    m.box((0.046, 0.3, 0.046), (x, 0.42 if x > 0 else -0.25, 0.21), 'rust')
for y in (-LY, LY):
    m.box((0.95, 0.04, 0.04), (0, y, 0.21), FRAME)
# Legs with little feet
for x in (-LX, LX):
    for y in (-LY, LY):
        m.cyl(0.024, 0.2, (x, y, 0.1), FRAME, seg=6)
        m.cyl(0.04, 0.02, (x, y, 0.01), 'rust_dark', seg=6)
# Slats under the mattress
for y in (-0.7, -0.35, 0.0, 0.35, 0.7):
    m.box((0.9, 0.05, 0.012), (0, y, 0.23), 'rust_dark' if y in (-0.35, 0.35) else 'gunmetal')
# Rust-streaked bracing below, visible from the side
m.tube([(-LX, -0.5, 0.19), (LX, 0.5, 0.19)], 0.01, 'rust', seg=5)
m.tube([(LX, -0.5, 0.19), (-LX, 0.5, 0.19)], 0.01, 'rust', seg=5)

# --- Head rail at -Y (taller, arched) and foot rail at +Y
def end_rail(y, zmax, n_bars):
    col = FRAME
    arch = [(-0.47, y, 0.2), (-0.47, y, zmax - 0.07), (-0.42, y, zmax - 0.025), (0.42, y, zmax - 0.025),
            (0.47, y, zmax - 0.07), (0.47, y, 0.2)]
    m.tube(arch, 0.024, col, seg=6)
    for p in arch[1:5]:                    # welded knuckles hide the mitre gaps
        m.cyl(0.03, 0.05, p, 'gunmetal', seg=6)
    for i in range(n_bars):
        x = -0.36 + 0.72 * i / (n_bars - 1)
        m.tube([(x, y, 0.21), (x, y, zmax - 0.04)], 0.012, 'rust' if i in (1, n_bars - 1) else col, seg=4)

end_rail(-0.965, 0.5, 5)
end_rail(0.965, 0.43, 4)

# --- Stained mattress (lumpy; stains are painted into the facets so there is no z-fight)
xs = [-0.43 + 0.86 * i / 6 for i in range(7)]
ys = [-0.925 + 1.85 * j / 8 for j in range(9)]
hmap = {}
def mtop(x, y):
    k = (round(x, 3), round(y, 3))
    if k not in hmap:
        ex = abs(x) > 0.42
        ey = abs(y) > 0.9
        base = 0.335 if (ex or ey) else 0.365
        hmap[k] = base + (0 if (ex or ey) else rng.uniform(-0.009, 0.009))
    return hmap[k]
STAINS = {(1, 1), (2, 1), (1, 2), (4, 0), (5, 0), (4, 1)}
DIRTY = {(0, 0), (0, 1), (0, 2), (3, 2), (5, 1), (2, 0)}
def mcolor(i, j):
    if i < 0:
        return 'fabric_stain'
    if (i, j) in STAINS:
        return 'dirt' if (i, j) == (1, 1) else 'fabric_stain'
    if (i, j) in DIRTY:
        return 'plaster'
    return 'fabric_white'
sheet(P, xs, ys, mtop, 0.13, mcolor)
# Ticking piping along the mattress edge
for x in (-0.432, 0.432):
    m.box((0.012, 1.84, 0.012), (x, 0, 0.3), 'fabric_stain')

# --- Pillow toward -Y: squashed, slightly skewed
def ptop(x, y):
    u, v = x / 0.3, (y + 0.72) / 0.17
    r = max(0.0, 1 - 0.75 * u * u) * max(0.0, 1 - 0.75 * v * v)
    return 0.375 + 0.095 * r + 0.012 * math.sin(x * 19 + y * 9)
pxs = [-0.3 + 0.6 * i / 4 for i in range(5)]
pys = [-0.89 + 0.34 * j / 3 for j in range(4)]
sheet(P, pxs, pys, ptop, 0.03, lambda i, j: 'fabric_grey' if i < 0 else ('fabric_stain' if (i, j) in ((2, 1), (2, 2)) else 'paint_white'))

# --- Blanket: rumpled, draped over the sides, thrown back at the pillow end
BX = 0.488
bxs = [-BX, -0.45, -0.25, -0.08, 0.1, 0.27, 0.45, BX]
bys = [-0.36, -0.14, 0.1, 0.34, 0.56, 0.76, 0.92]
brng = random.Random(9021)
bmap = {}
def btop(x, y):
    k = (round(x, 3), round(y, 3))
    if k not in bmap:
        if abs(x) >= BX - 1e-3:
            z = 0.25 + brng.uniform(-0.012, 0.03)       # hanging hem
        elif abs(x) >= 0.44:
            z = 0.378 + brng.uniform(-0.008, 0.004)
        else:
            z = 0.402 + brng.uniform(-0.012, 0.016) + 0.012 * math.sin(y * 8 + x * 3)
        bmap[k] = z
    return bmap[k]
PATCH = {(2, 3), (5, 1)}
def bcolor(i, j):
    if i < 0:
        return 'fabric_blue'
    if (i, j) in PATCH:
        return 'mustard_dark' if i == 2 else 'cloth_red'
    if j == 4:
        return 'fabric_grey'                         # woven band across the foot
    return 'fabric_blue'
sheet(P, bxs, bys, btop, 0.03, bcolor)
# Folded-back turn of the blanket, showing its lighter underside
fxs = [-0.44, -0.15, 0.15, 0.44]
fys = [-0.43, -0.34, -0.25]
def ftop(x, y):
    bulge = 1 - ((y + 0.34) / 0.09) ** 2
    return 0.404 + 0.03 * max(bulge, 0) + 0.008 * math.sin(x * 14)
sheet(P, fxs, fys, ftop, 0.05, lambda i, j: 'fabric_grey' if i < 0 else 'fabric_white')

finish(m, bounds={'x': (-0.5, 0.5), 'y': (-1.0, 1.0), 'z': (0, 0.5)})
