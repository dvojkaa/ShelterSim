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
m = Model('furn_bed_single', seed=62)
P = m.default
rng = random.Random(6202)
# Simple painted frame with a rounded headboard (+Y)
m.box((1.0, 0.07, 0.8), (0, 0.965, 0.4), 'paint_green', bevel=0.02, taper=(0.9, 1.0))
m.box((0.76, 0.07, 0.06), (0, 0.965, 0.77), 'paint_green', bevel=0.02)
m.box((0.5, 0.02, 0.18), (0, 0.925, 0.58), 'mustard', bevel=0.01)       # faded sticker panel
m.box((1.0, 0.06, 0.34), (0, -0.97, 0.25), 'paint_green', bevel=0.02)
m.box((0.96, 0.06, 0.04), (0, -0.97, 0.44), 'paint_white', bevel=0.01)
for x in (-0.46, 0.46):
    m.box((0.06, 1.9, 0.12), (x, 0, 0.26), 'paint_green', bevel=0.015)
    m.box((0.07, 0.07, 0.2), (x*0.95, 0.95, 0.1), 'wood_dark')
    m.box((0.07, 0.07, 0.2), (x*0.95, -0.95, 0.1), 'wood_dark')
m.box((0.88, 1.88, 0.03), (0, 0, 0.23), 'wood_grey')
# chipped paint flecks
for (x, y, z) in ((-0.2, 0.93, 0.35), (0.3, 0.93, 0.7), (0.455, -0.2, 0.3)):
    m.box((0.08, 0.075, 0.05), (x, y, z), 'wood_light')
xs = [-0.44 + 0.88 * i / 5 for i in range(6)]
ys = [-0.9 + 1.8 * j / 8 for j in range(9)]
hm = {}
def mt(x, y):
    k = (round(x, 3), round(y, 3))
    if k not in hm:
        edge = abs(x) > 0.43 or abs(y) > 0.89
        hm[k] = (0.36 if edge else 0.39) + (0 if edge else rng.uniform(-0.008, 0.008))
    return hm[k]
sheet(P, xs, ys, mt, 0.14, lambda i, j: 'fabric_stain' if i < 0 or (i, j) in ((1, 2), (3, 5)) else 'fabric_white')
# pillow
def pt(x, y):
    u, v = x / 0.27, (y - 0.7) / 0.17
    r = max(0, 1 - 0.8 * u * u) * max(0, 1 - 0.8 * v * v)
    return 0.4 + 0.09 * r
sheet(P, [-0.27 + 0.54 * i / 4 for i in range(5)], [0.55 + 0.3 * j / 3 for j in range(4)], pt, 0.03,
      lambda i, j: 'paint_white' if i >= 0 else 'fabric_grey')
# thin blanket
bx = [-0.49, -0.4, -0.15, 0.1, 0.35, 0.49]
by = [-0.95, -0.7, -0.35, 0.0, 0.25]
bm = {}
def bt(x, y):
    k = (round(x, 3), round(y, 3))
    if k not in bm:
        if abs(x) > 0.48: z = 0.25 + rng.uniform(0, 0.03)
        elif y < -0.94: z = 0.3 + rng.uniform(0, 0.03)
        else: z = 0.405 + rng.uniform(-0.008, 0.012)
        bm[k] = z
    return bm[k]
sheet(P, bx, by, bt, 0.02, lambda i, j: 'fabric_blue' if i < 0 or (i + j) % 2 == 0 else 'fabric_grey')
# Teddy: blob body, head, ears, limbs
T = (0.12, 0.0)
m.blob((0.24, 0.2, 0.26), (0.12, 0.0, 0.440), 'leather', rough=0.1, colors=['leather', 'leather_dark'])
m.blob((0.19, 0.18, 0.18), (0.12, 0.02, 0.640), 'leather', rough=0.08, colors=['leather', 'bark_light'])
m.blob((0.1, 0.09, 0.1), (0.04, 0.02, 0.715), 'leather_dark', rough=0.05)
m.blob((0.1, 0.09, 0.1), (0.2, 0.02, 0.715), 'leather_dark', rough=0.05)
m.blob((0.08, 0.06, 0.06), (0.12, -0.08, 0.620), 'bone', rough=0.04)        # muzzle
m.box((0.03, 0.02, 0.03), (0.12, -0.12, 0.640), 'black')
m.box((0.025, 0.02, 0.025), (0.07, -0.085, 0.690), 'black')
m.box((0.025, 0.02, 0.025), (0.17, -0.085, 0.690), 'black')
for dx, dz, rz in ((-0.14, 0.0, 30), (0.14, 0.0, -30)):
    m.blob((0.09, 0.09, 0.2), (0.12 + dx, -0.04, 0.47), 'leather', rough=0.05, rot=(-60, 0, rz) if False else (0, 0, 0))
for dx in (-0.08, 0.08):
    m.blob((0.09, 0.12, 0.09), (0.12 + dx, -0.1, 0.43), 'leather', rough=0.05)
m.box((0.05, 0.01, 0.05), (0.2, -0.1, 0.470), 'mustard_dark')               # patch
finish(m, bounds={'x': (-0.5, 0.5), 'y': (-1.0, 1.0), 'z': (0, 0.8)})
