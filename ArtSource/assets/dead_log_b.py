import math, random
import bmesh
from mathutils import Matrix, Vector
from ss_art import Model, finish

def limb(m, pts, radii, colors, rng, seg=6, gnarl=0.1, pale=None, pinch=None, twist=0.0):
    """Tapered lofted limb. pale(ring_i, face_centre) -> True paints that facet raw wood (bark wound / notch).
    pinch = {ring_i: (direction, depth)} pulls that ring's verts on one side inward, carving a V notch.
    twist = radians per ring: spirals the facets so the trunk doesn't read as stacked bands."""
    pts = [Vector(p) for p in pts]
    t = bmesh.new(); t.loops.layers.uv.new('UVMap')
    rings = []
    u = None
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        if u is None:
            u = d.orthogonal().normalized()
        u = (u - d * u.dot(d)).normalized()
        v = d.cross(u)
        ring = []
        for k in range(seg):
            a = 2 * math.pi * k / seg + twist * i
            r = radii[i] * (1 + rng.uniform(-gnarl, gnarl))
            off = (u * math.cos(a) + v * math.sin(a)) * r
            if pinch and i in pinch:
                dirn, depth = pinch[i]
                dirn = Vector(dirn).normalized()
                s = off.dot(dirn)
                if s > 0:
                    off = off - dirn * s * depth
            c = p + off
            c.z = max(c.z, 0.0)
            ring.append(t.verts.new(c))
        rings.append(ring)
    painted = []
    for i, (a, b) in enumerate(zip(rings[:-1], rings[1:])):
        for k in range(seg):
            f = t.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
            painted.append((f, i))
    t.faces.new(rings[0]); t.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(t, faces=t.faces)
    for f in t.faces:
        m.default._paint(t, [f], rng.choice(colors))
    if pale:
        for f, i in painted:
            if pale(i, f.calc_center_median()):
                m.default._paint(t, [f], 'wood_light')
    m.default._commit(t, Matrix.Identity(4))


def cut_end(m, r, h, loc, rot=(0, 0, 0), seg=8):
    """Pale sawn face with a darker heart: says 'wood' from a distance."""
    m.cyl(r, h, loc, 'wood_light', rot=rot, seg=seg)
    from mathutils import Euler
    n = Euler([math.radians(a) for a in rot]).to_matrix() @ Vector((0, 0, 1))
    m.cyl(r * 0.45, h, tuple(Vector(loc) + n * h * 0.4), 'wood', rot=rot, seg=6)

# dead_log_b: half-chopped trunk with a big pale axe notch, sawn stump with a rusty hatchet in it, a small stack of split logs.
m = Model('dead_log_b', seed=105)
rng = random.Random(1105)
BARK = ['bark', 'bark', 'bark_dead', 'bark_light']
BX, BY = 0.12, 0.14
# One continuous trunk with a one-sided V notch chopped into its front (-Y), so the back stays intact bark
UX, UY = BX + 0.03, BY + 0.02
def notch_faces(i, c):
    return i in (3, 4) and c.y < BY + 0.03
limb(m, [(BX, BY, 0), (BX, BY, 0.3), (BX + 0.02, BY + 0.01, 0.8), (BX + 0.02, BY + 0.01, 1.18), (BX + 0.025, BY + 0.015, 1.4), (BX + 0.03, BY + 0.02, 1.62),
         (UX + 0.005, UY + 0.02, 2.0), (UX + 0.05, UY + 0.03, 2.4), (UX + 0.06, UY + 0.03, 2.62)],
     [0.25, 0.205, 0.185, 0.175, 0.17, 0.16, 0.145, 0.12, 0.11], BARK, rng, seg=8, gnarl=0.06, pale=notch_faces, twist=0.22,
     pinch={4: ((0, -1, 0), 1.25)})
# Pale flat lips above and below the cut
m.box((0.2, 0.13, 0.03), (BX + 0.02, BY - 0.07, 1.19), 'wood_light', taper=(0.95, 0.9))
m.box((0.2, 0.12, 0.03), (BX + 0.03, BY - 0.07, 1.61), 'wood_light', taper=(1.0, 1.0))
for a, h, c in ((40, 0.26, 'wood_grey'), (130, 0.14, 'wood_light'), (230, 0.22, 'wood_grey'), (320, 0.12, 'wood_light')):
    ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
    limb(m, [(UX + 0.06 + ca * 0.06, UY + 0.03 + sa * 0.06, 2.55), (UX + 0.06 + ca * 0.07, UY + 0.03 + sa * 0.07, 2.62 + h)], [0.055, 0.006], [c], rng, seg=4, gnarl=0.05)
# Branch stubs
for pts, r in (
    ([(UX, UY, 2.05), (UX - 0.26, UY + 0.06, 2.3), (UX - 0.42, UY + 0.05, 2.62)], 0.06),
    ([(BX, BY, 0.85), (BX + 0.22, BY + 0.14, 1.05), (BX + 0.34, BY + 0.2, 1.2)], 0.055),
):
    limb(m, pts, [r, r * 0.72, r * 0.5], BARK, rng, seg=5, gnarl=0.06)
    limb(m, [pts[-1], (Vector(pts[-1]) + (Vector(pts[-1]) - Vector(pts[-2])) * 0.25)], [r * 0.48, 0.005], ['wood_light'], rng, seg=5, gnarl=0.0)
# Chip spill below the notch
for x, y, z, rz in ((0.1, -0.28, 0.02, 20), (0.28, -0.22, 0.02, 75), (0.02, -0.2, 0.02, 130), (0.32, -0.4, 0.02, 45), (0.16, -0.42, 0.02, 100), (0.42, -0.1, 0.02, 10)):
    m.box((0.13, 0.06, 0.03), (x, y, z), 'wood_light', rot=(0, 0, rz))
# Sawn stump with hatchet
SX, SY = -0.34, 0.3
limb(m, [(SX, SY, 0), (SX, SY, 0.2), (SX, SY, 0.38)], [0.2, 0.165, 0.14], BARK, rng, seg=8, gnarl=0.06)
cut_end(m, 0.135, 0.03, (SX, SY, 0.385))
m.box((0.13, 0.03, 0.11), (SX + 0.03, SY, 0.43), 'steel_dark', taper=(1.25, 1), bevel=0.004)
m.box((0.05, 0.035, 0.05), (SX + 0.03, SY, 0.5), 'rust')
m.tube([(SX + 0.03, SY, 0.5), (SX + 0.17, SY - 0.02, 0.73)], 0.018, 'wood', seg=5)
# Stack of split logs
for x, y, z, rz in ((-0.38, -0.34, 0.075, 8), (-0.14, -0.38, 0.075, -6), (-0.26, -0.36, 0.2, 3)):
    limb(m, [(x + 0.2 * math.cos(math.radians(rz)), y + 0.2 * math.sin(math.radians(rz)), z), (x - 0.2 * math.cos(math.radians(rz)), y - 0.2 * math.sin(math.radians(rz)), z)], [0.075, 0.075], ['bark', 'bark_dead'], rng, seg=6, gnarl=0.03)
    d = Vector((math.cos(math.radians(rz)), math.sin(math.radians(rz)), 0))
    for s in (-1, 1):
        q = Vector((x, y, z)) + d * (s * 0.205)
        m.cyl(0.062, 0.012, tuple(q), 'wood_light', rot=(90, 0, rz + 90), seg=6)
finish(m, bounds={'x': (-0.6, 0.6), 'y': (-0.6, 0.6), 'z': (0, 3.0)})
