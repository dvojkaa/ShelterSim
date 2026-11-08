import math
import bmesh
from ss_art import Model, finish
from mathutils import Matrix
m = Model('piece_water_collector', seed=59)
P = m.default

def lathe(part, profile, loc, seg, colorfn):
    """Revolve a (r, z) profile about Z. Start/end with r=0 for a closed solid. colorfn(band, slice)."""
    t = bmesh.new(); t.loops.layers.uv.new('UVMap')
    rings = []
    for r, z in profile:
        if r <= 1e-6:
            rings.append([t.verts.new((0, 0, z))])
        else:
            rings.append([t.verts.new((r * math.cos(2 * math.pi * s / seg), r * math.sin(2 * math.pi * s / seg), z)) for s in range(seg)])
    faces = []
    for b in range(len(rings) - 1):
        a, c = rings[b], rings[b + 1]
        for s in range(seg):
            n = (s + 1) % seg
            if len(a) == 1:
                f = t.faces.new((a[0], c[s], c[n]))
            elif len(c) == 1:
                f = t.faces.new((a[s], a[n], c[0]))
            else:
                f = t.faces.new((a[s], a[n], c[n], c[s]))
            faces.append((f, b, s))
    bmesh.ops.recalc_face_normals(t, faces=t.faces)
    for f, b, s in faces:
        part._paint(t, [f], colorfn(b, s))
    part._commit(t, Matrix.Translation(loc))

# --- Scrap pallet
for x, col in ((-0.33, 'wood_dark'), (0.0, 'wood_dark'), (0.33, 'wood_dark')):
    m.box((0.1, 0.78, 0.035), (x, 0, 0.0175), col)
for y, col in ((-0.34, 'wood_grey'), (-0.17, 'wood'), (0.0, 'wood_grey'), (0.17, 'wood_light'), (0.34, 'wood')):
    m.box((0.78, 0.12, 0.022), (0, y, 0.046), col)
BASE = 0.057

# --- The barrel, raised on two blocks so the bucket fits under the tap
BY = 0.05
for x in (-0.14, 0.14):
    m.box((0.1, 0.42, 0.045), (x, BY, BASE + 0.0225), 'concrete')
R = 0.27
Z0 = BASE + 0.045
Z1 = 0.82
ribs = (Z0 + 0.22, Z0 + 0.46)
prof = [(0, Z0), (R, Z0 + 0.02), (R, Z0 + 0.11)]
for rz in ribs:
    prof += [(R, rz - 0.018), (R + 0.012, rz - 0.01), (R + 0.012, rz + 0.01), (R, rz + 0.018)]
    prof.append((R, rz + 0.12))
prof += [(R, Z1), (R - 0.03, Z1 + 0.012), (0, Z1 + 0.012)]
rust_cells = {(2, 2), (2, 3), (7, 8), (7, 9), (8, 9), (2, 7), (12, 5), (12, 4), (7, 0), (13, 10)}
def barrel_color(b, s):
    z = (prof[b][1] + prof[b + 1][1]) / 2
    if b == 0 or z > Z1 + 0.001:
        return 'steel_dark'
    if any(abs(z - rz) < 0.013 for rz in ribs):
        return 'steel_dark'
    if (b, s) in rust_cells:
        return 'rust'
    return 'plastic_blue'
lathe(P, prof, (0, BY, 0), 12, barrel_color)
# Sight tube strapped to the side
m.tube([(0.2, BY - 0.205, Z0 + 0.1), (0.205, BY - 0.21, Z1 - 0.08)], 0.011, 'glass_dirty', seg=6)
m.box((0.05, 0.04, 0.02), (0.2, BY - 0.19, Z0 + 0.1), 'steel_dark', rot=(0, 0, 45))
m.box((0.05, 0.04, 0.02), (0.205, BY - 0.19, Z1 - 0.08), 'steel_dark', rot=(0, 0, 45))

# --- Tap at -Y with a red wheel, dripping into a battered bucket
TZ = 0.335
FRONT = BY - R
m.cyl(0.042, 0.02, (0, FRONT - 0.005, TZ), 'gunmetal', rot=(90, 0, 0), seg=8)
m.cyl(0.022, 0.07, (0, FRONT - 0.04, TZ), 'mustard', rot=(90, 0, 0), seg=8)
m.tube([(0, FRONT - 0.07, TZ), (0, FRONT - 0.095, TZ - 0.025), (0, FRONT - 0.095, TZ - 0.07)], 0.016, 'mustard', seg=7)
m.cyl(0.012, 0.05, (0, FRONT - 0.045, TZ + 0.04), 'steel', seg=6)
m.cyl(0.043, 0.012, (0, FRONT - 0.045, TZ + 0.065), 'red_bright', seg=8)
m.box((0.075, 0.012, 0.012), (0, FRONT - 0.045, TZ + 0.074), 'warning_red')
m.box((0.012, 0.075, 0.012), (0, FRONT - 0.045, TZ + 0.074), 'warning_red')
# Bucket: truly hollow, with water inside and a wire bail
BUCK = (0, BY - R - 0.095, BASE)
lathe(P, [(0, 0), (0.065, 0), (0.078, 0.11), (0.083, 0.115), (0.078, 0.12), (0.07, 0.11), (0.066, 0.075), (0, 0.075)],
      BUCK, 10, lambda b, s: ('water' if b == 6 else 'rust_dark' if b == 5 else 'steel') if b != 1 or s % 4 else 'rust')
m.tube([(-0.08, BUCK[1], BASE + 0.115), (-0.05, BUCK[1] - 0.008, BASE + 0.16), (0.05, BUCK[1] - 0.008, BASE + 0.16), (0.08, BUCK[1], BASE + 0.115)], 0.004, 'steel_dark', seg=5)

# --- Funnel neck rising to the tarp's drain
NZ0, NZ1 = Z1 + 0.012, 0.96
lathe(P, [(0, NZ0), (0.08, NZ0), (0.075, NZ0 + 0.04), (0.14, NZ1 - 0.03), (0.15, NZ1), (0.12, NZ1), (0.05, NZ1 - 0.05), (0, NZ1 - 0.05)],
      (0, BY, 0), 10, lambda b, s: 'steel_dark' if b < 2 else ('water' if b >= 5 else 'rust' if s % 3 == 0 else 'steel'))

# --- Four scavenged poles from the pallet corners
POLE = 0.37
for sx in (-1, 1):
    for sy in (-1, 1):
        x, y = sx * POLE, sy * POLE
        m.cyl(0.022, 1.2 - BASE, (x, y, (1.2 + BASE) / 2), 'wood_grey' if sx * sy > 0 else 'wood', seg=7)
        m.cyl(0.028, 0.035, (x, y, 1.13), 'rope', seg=6)        # lashing
        # Cross-braces between poles, back and sides
m.tube([(-POLE, POLE, 0.3), (POLE, POLE, 0.3)], 0.012, 'rust', seg=6)
m.tube([(-POLE, -POLE, 0.72), (-POLE, POLE, 0.72)], 0.012, 'rust', seg=6)
m.tube([(POLE, -POLE, 0.72), (POLE, POLE, 0.72)], 0.012, 'rust', seg=6)
m.tube([(-POLE, POLE, 0.72), (POLE, POLE, 0.72)], 0.012, 'rust', seg=6)

# --- Sagging tarp: corners tied high, edges slack, the belly draining toward the neck
HIGH, LOW = 1.15, 0.965
def tarp_z(u, v):
    au, av = abs(u), abs(v)
    return LOW + (HIGH - LOW) * (0.5 * max(au, av) ** 1.6 + 0.5 * au * av) + 0.006 * math.sin(u * 7.3 + v * 3.1)
N = 6
H = POLE - 0.006
t = bmesh.new(); t.loops.layers.uv.new('UVMap')
def v2(i, j, dz):
    u, v = -1 + 2 * i / N, -1 + 2 * j / N
    return t.verts.new((u * H, v * H + 0, tarp_z(u, v) + dz))
up = [[v2(i, j, 0) for j in range(N + 1)] for i in range(N + 1)]
dn = [[v2(i, j, -0.016) for j in range(N + 1)] for i in range(N + 1)]
tops = []
for i in range(N):
    for j in range(N):
        tops.append((t.faces.new((up[i][j], up[i + 1][j], up[i + 1][j + 1], up[i][j + 1])), i, j))
        t.faces.new((dn[i][j + 1], dn[i + 1][j + 1], dn[i + 1][j], dn[i][j]))
for i in range(N):
    t.faces.new((dn[i][0], dn[i + 1][0], up[i + 1][0], up[i][0]))
    t.faces.new((up[i][N], up[i + 1][N], dn[i + 1][N], dn[i][N]))
    t.faces.new((up[0][i], up[0][i + 1], dn[0][i + 1], dn[0][i]))
    t.faces.new((dn[N][i], dn[N][i + 1], up[N][i + 1], up[N][i]))
bmesh.ops.recalc_face_normals(t, faces=t.faces)
P._paint(t, t.faces, 'army_green_dark')
PATCHES = {(1, 4), (0, 4), (4, 1)}
for f, i, j in tops:
    if 2 <= i <= 3 and 2 <= j <= 3:
        c = 'water'
    elif (i, j) in PATCHES:
        c = 'fabric_stain'
    elif (1 <= i <= 4 and 1 <= j <= 4):
        c = 'army_green_dark'
    else:
        c = 'army_green'
    P._paint(t, [f], c)
P._commit(t, Matrix.Identity(4))

finish(m, bounds={'x': (-0.4, 0.4), 'y': (-0.4, 0.4), 'z': (0, 1.2)})
