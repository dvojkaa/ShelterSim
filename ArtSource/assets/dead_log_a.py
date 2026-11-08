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

# dead_log_a: tall splintered standing snag, broken branch stubs, sawn stump and a cut round at its foot.
m = Model('dead_log_a', seed=104)
rng = random.Random(1104)
BARK = ['bark_dead', 'bark_dead', 'wood_grey', 'bark']
BX, BY = -0.14, 0.12
def wounds(i, c):
    # Stripped-bark scars follow real trunk facets instead of being stuck-on plates
    dx, dy = c.x - BX, c.y - BY
    if i == 2 and dy < -0.11 and -0.02 < dx < 0.12:
        return True
    return i == 4 and dx < -0.08 and dy > 0.02
limb(m, [(BX, BY, 0), (BX + 0.01, BY, 0.35), (BX + 0.05, BY + 0.02, 0.8), (BX + 0.03, BY + 0.05, 1.35), (BX + 0.02, BY + 0.05, 1.8), (BX + 0.06, BY + 0.03, 2.25), (BX + 0.05, BY + 0.05, 2.55)],
     [0.25, 0.2, 0.18, 0.165, 0.155, 0.14, 0.13], BARK, rng, seg=8, gnarl=0.08, pale=wounds, twist=0.3)
for a, l in ((60, 0.36), (200, 0.34), (320, 0.38)):
    c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
    limb(m, [(BX + c * 0.08, BY + s * 0.08, 0.3), (BX + c * 0.24, BY + s * 0.24, 0.1), (BX + c * l, BY + s * l, 0.01)], [0.1, 0.075, 0.03], BARK, rng, seg=5)
# Splintered top: pale shards of different height, ringed around the broken end
TX, TY = BX + 0.05, BY + 0.05
for a, h, c in ((10, 0.42, 'wood_grey'), (80, 0.25, 'wood_light'), (150, 0.36, 'wood_grey'), (225, 0.2, 'wood_light'), (300, 0.3, 'wood_grey')):
    ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
    limb(m, [(TX + ca * 0.07, TY + sa * 0.07, 2.45), (TX + ca * 0.08, TY + sa * 0.08, 2.5 + h * 0.5), (TX + ca * 0.09, TY + sa * 0.09, 2.5 + h)], [0.07, 0.045, 0.006], [c], rng, seg=4, gnarl=0.05)
m.cyl(0.1, 0.05, (TX, TY, 2.56), 'wood_light', seg=7)
# Broken branch stubs with pale snapped tips
for pts, r in (
    ([(BX + 0.05, BY, 1.25), (BX + 0.28, BY - 0.1, 1.5), (BX + 0.5, BY - 0.15, 1.85)], 0.07),
    ([(BX, BY + 0.05, 1.75), (BX - 0.2, BY + 0.14, 2.05), (BX - 0.34, BY + 0.2, 2.4)], 0.06),
    ([(BX + 0.04, BY + 0.03, 0.8), (BX - 0.2, BY - 0.15, 0.98), (BX - 0.3, BY - 0.26, 1.08)], 0.06),
):
    limb(m, pts, [r, r * 0.72, r * 0.5], BARK, rng, seg=5, gnarl=0.06)
    limb(m, [pts[-1], (Vector(pts[-1]) + (Vector(pts[-1]) - Vector(pts[-2])) * 0.25)], [r * 0.48, 0.005], ['wood_light'], rng, seg=5, gnarl=0.0)
# Small sawn stump beside it
SX, SY = 0.33, -0.28
limb(m, [(SX, SY, 0), (SX, SY, 0.18), (SX, SY, 0.4)], [0.2, 0.16, 0.14], BARK, rng, seg=8, gnarl=0.06)
cut_end(m, 0.135, 0.03, (SX, SY, 0.405))
# Cut round lying on the ground
m.cyl(0.11, 0.26, (-0.27, -0.42, 0.11), 'bark_dead', rot=(90, 0, 25), seg=8)
for s in (-1, 1):
    n = Vector((math.sin(math.radians(25)) * -1, math.cos(math.radians(25)), 0))
    pos = Vector((-0.27, -0.42, 0.11)) + n * (s * 0.132)
    m.cyl(0.095, 0.012, tuple(pos), 'wood_light', rot=(90, 0, 25), seg=8)
finish(m, bounds={'x': (-0.6, 0.6), 'y': (-0.6, 0.6), 'z': (0, 3.0)})
