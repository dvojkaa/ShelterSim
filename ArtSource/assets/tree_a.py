import math, random
import bmesh
from mathutils import Matrix, Vector
from ss_art import Model, finish

def limb(m, pts, radii, colors, rng, seg=6, gnarl=0.1):
    """Continuous tapered lofted limb: one ring per point, jittered for a gnarled look, closed both ends."""
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
            a = 2 * math.pi * k / seg
            r = radii[i] * (1 + rng.uniform(-gnarl, gnarl))
            c = p + (u * math.cos(a) + v * math.sin(a)) * r
            c.z = max(c.z, 0.0)
            ring.append(t.verts.new(c))
        rings.append(ring)
    for a, b in zip(rings[:-1], rings[1:]):
        for k in range(seg):
            t.faces.new((a[k], a[(k + 1) % seg], b[(k + 1) % seg], b[k]))
    t.faces.new(rings[0]); t.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(t, faces=t.faces)
    for f in t.faces:
        m.default._paint(t, [f], rng.choice(colors))
    m.default._commit(t, Matrix.Identity(4))

def clump(m, size, loc, colors, rough=0.14, subdiv=2, flat=0.62):
    """Faceted leaf mass with a flattened underside so it reads as a canopy, not a ball."""
    m.blob(size, loc, colors[0], rough=rough, subdiv=subdiv, colors=colors, flat_bottom=-size[2] * 0.5 * flat)

def tufts(m, spots, colors):
    """Small faceted leaf tufts that break up the round outline of the big masses."""
    for size, loc in spots:
        m.blob((size, size, size * 0.72), loc, colors[0], rough=0.2, subdiv=1, colors=colors)

# tree_a: the lush-ish one. Broad, dense, multi-lobed crown on a forked gnarled trunk.
m = Model('tree_a', seed=101)
rng = random.Random(1101)
BARK = ['bark', 'bark', 'bark_light']
LOW = ['leaf_dark', 'leaf_dark', 'leaf_dark', 'leaf_dark', 'leaf_green']
MID = ['leaf_green', 'leaf_green', 'leaf_green', 'leaf_dark', 'leaf_dark', 'leaf_dark']
TOP = ['leaf_green', 'leaf_green', 'leaf_green', 'leaf_green', 'leaf_green', 'leaf_dark', 'leaf_dark', 'leaf_dry']

limb(m, [(0, 0, 0), (0.02, 0, 0.45), (0.09, 0.03, 1.0), (0.04, 0.09, 1.5), (-0.03, 0.05, 2.05), (0.02, 0, 2.75)],
     [0.19, 0.17, 0.16, 0.15, 0.125, 0.08], BARK, rng, seg=7)
for a, l in ((20, 0.42), (140, 0.38), (255, 0.4)):
    c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
    limb(m, [(c * 0.05, s * 0.05, 0.42), (c * 0.2, s * 0.2, 0.14), (c * l, s * l, 0.01)], [0.09, 0.075, 0.03], BARK, rng, seg=5)
for pts, r in (
    ([(0.05, 0.08, 1.45), (-0.3, 0.2, 2.05), (-0.75, 0.25, 2.65), (-0.9, 0.3, 3.05)], 0.09),
    ([(0.06, 0.04, 1.65), (0.42, -0.15, 2.15), (0.8, -0.3, 2.75), (0.95, -0.35, 3.1)], 0.09),
    ([(0, 0.08, 1.95), (0.1, 0.5, 2.5), (0.2, 0.85, 3.05)], 0.07),
    ([(0, 0.02, 2.05), (-0.1, -0.5, 2.5), (-0.2, -0.9, 2.95)], 0.07),
    ([(-0.55, 0.23, 2.4), (-0.85, -0.25, 2.5), (-0.95, -0.55, 2.6)], 0.045),
    ([(0.6, -0.22, 2.45), (0.9, 0.3, 2.5), (0.85, 0.7, 2.6)], 0.045),
):
    limb(m, pts, [r * (1 - 0.72 * i / (len(pts) - 1)) for i in range(len(pts))], BARK, rng, seg=5)

clump(m, (1.95, 1.95, 1.5), (0.05, -0.05, 3.9), TOP)
clump(m, (1.6, 1.5, 1.25), (-0.85, 0.25, 3.25), MID)
clump(m, (1.6, 1.6, 1.25), (0.85, -0.3, 3.3), MID)
clump(m, (1.5, 1.4, 1.2), (0.2, 0.9, 3.3), MID)
clump(m, (1.4, 1.4, 1.1), (-0.2, -0.95, 3.15), MID)
clump(m, (1.1, 1.1, 0.75), (0.85, 0.7, 2.75), LOW)
clump(m, (1.0, 1.0, 0.7), (-0.95, -0.6, 2.75), LOW)
clump(m, (0.8, 0.8, 0.55), (1.2, -0.75, 2.8), LOW, subdiv=1)
tufts(m, [(0.7, (0.95, 0.35, 3.95)), (0.75, (-0.75, -0.3, 4.0)), (0.65, (-0.2, 0.75, 4.05)), (0.6, (0.6, -0.8, 3.95)),
          (0.6, (-1.25, 0.55, 2.95)), (0.55, (0.35, 1.3, 2.95)), (0.6, (-0.55, -1.25, 2.85)), (0.55, (1.35, 0.15, 2.95))], MID)
finish(m, bounds={'x': (-1.8, 1.8), 'y': (-1.8, 1.8), 'z': (0, 4.8)})
