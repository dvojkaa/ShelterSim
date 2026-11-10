import math, random
import bmesh
from ss_art import Model, finish
from mathutils import Matrix

def sheet(part, xs, ys, top, thick, colorfn):
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
    for i in range(nx - 1):
        t.faces.new((dn[i][0], dn[i + 1][0], up[i + 1][0], up[i][0]))
        t.faces.new((up[i][ny - 1], up[i + 1][ny - 1], dn[i + 1][ny - 1], dn[i][ny - 1]))
    for j in range(ny - 1):
        t.faces.new((up[0][j], up[0][j + 1], dn[0][j + 1], dn[0][j]))
        t.faces.new((dn[nx - 1][j], dn[nx - 1][j + 1], up[nx - 1][j + 1], up[nx - 1][j]))
    bmesh.ops.recalc_face_normals(t, faces=t.faces)
    part._paint(t, t.faces, colorfn(-1, -1))
    for f, i, j in tops:
        part._paint(t, [f], colorfn(i, j))
    part._commit(t, Matrix.Identity(4))
m = Model('furn_bed_double', seed=61)
P = m.default
rng = random.Random(6101)
# Wooden frame: headboard at +Y
m.box((1.6, 0.08, 0.9), (0, 1.0, 0.45), 'wood_dark', bevel=0.02)
m.box((1.5, 0.05, 0.5), (0, 0.955, 0.62), 'wood', bevel=0.015)           # panel
for x in (-0.55, 0, 0.55):
    m.box((0.06, 0.03, 0.46), (x, 0.93, 0.62), 'wood_dark')
m.box((1.7 - 0.1, 0.05, 0.08), (0, 1.0, 0.9 - 0.04), 'wood_light', bevel=0.01)
m.box((1.6, 0.07, 0.42), (0, -1.0, 0.31), 'wood_dark', bevel=0.02)         # footboard
m.box((1.6, 0.05, 0.05), (0, -1.0, 0.54), 'wood', bevel=0.01)
for x in (-0.75, 0.75):
    m.box((0.06, 2.0, 0.12), (x, 0, 0.3), 'wood', bevel=0.015)               # side rails
    m.box((0.1, 0.1, 0.5), (x*0.95, -1.0, 0.25), 'wood_dark')
    m.box((0.1, 0.1, 0.16), (x*0.95, 1.0, 0.08), 'wood_dark')
m.box((1.5, 1.9, 0.03), (0, 0, 0.27), 'wood_grey')                            # slats board
# Mattress: lumpy, one stained edge (+X side)
xs = [-0.74 + 1.48 * i / 8 for i in range(9)]
ys = [-0.95 + 1.9 * j / 10 for j in range(11)]
hm = {}
def mt(x, y):
    k = (round(x, 3), round(y, 3))
    if k not in hm:
        edge = abs(x) > 0.73 or abs(y) > 0.94
        hm[k] = (0.46 if edge else 0.5) + (0 if edge else rng.uniform(-0.012, 0.012))
    return hm[k]
def mc(i, j):
    if i < 0: return 'fabric_stain'
    if i >= 6 and j in (1, 2, 3, 4, 5, 6): return 'dirt' if (i, j) in ((7, 3), (7, 4), (6, 3)) else 'fabric_stain'
    if (i + j) % 5 == 0: return 'plaster'
    return 'fabric_white'
sheet(P, xs, ys, mt, 0.2, mc)
# Pillows (two) near +Y
def pillow(cx):
    def pt(x, y):
        u, v = (x - cx) / 0.3, (y - 0.7) / 0.2
        r = max(0, 1 - 0.8 * u * u) * max(0, 1 - 0.8 * v * v)
        return 0.51 + 0.11 * r + 0.012 * math.sin(x * 17 + y * 8)
    px = [cx - 0.3 + 0.6 * i / 4 for i in range(5)]
    py = [0.5 + 0.4 * j / 3 for j in range(4)]
    sheet(P, px, py, pt, 0.04, lambda i, j: 'fabric_grey' if i < 0 else ('fabric_stain' if (i, j) == (1, 2) and cx > 0 else 'paint_white'))
pillow(-0.38); pillow(0.4)
# Blanket: rumpled, draped on -X side & foot, half thrown back
bx = [-0.79, -0.7, -0.45, -0.2, 0.05, 0.3, 0.45, 0.52]
by = [-1.02, -0.8, -0.5, -0.2, 0.1, 0.3]
bm = {}
def bt(x, y):
    k = (round(x, 3), round(y, 3))
    if k not in bm:
        if x <= -0.78: z = 0.3 + rng.uniform(0, 0.05)
        elif y <= -1.0: z = 0.34 + rng.uniform(0, 0.05)
        else: z = 0.535 + rng.uniform(-0.02, 0.03) + 0.025 * math.sin(y * 7 + x * 4)
        bm[k] = z
    return bm[k]
def bc(i, j):
    if i < 0: return 'cloth_red'
    if j == 2 and i % 2 == 0: return 'mustard_dark'
    if (i, j) == (4, 4): return 'fabric_grey'
    return 'cloth_red'
sheet(P, bx, by, bt, 0.035, bc)
# Folded-back turn
sheet(P, [-0.77, -0.3, 0.2, 0.5], [0.28, 0.36, 0.44],
      lambda x, y: 0.56 + 0.035 * max(0, 1 - ((y - 0.36) / 0.08) ** 2), 0.05,
      lambda i, j: 'fabric_white' if i >= 0 else 'fabric_stain')
finish(m, bounds={'x': (-0.8, 0.8), 'y': (-1.05, 1.05), 'z': (0, 0.9)})
