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

# tree_b: the sparse/dry one. Twisted leaning trunk, wide open fork, a few flat wind-sheared dry clumps; branches stay visible.
m = Model('tree_b', seed=102)
rng = random.Random(1102)
BARK = ['bark', 'bark_light', 'bark_light', 'bark_dead']
DRY = ['leaf_dry', 'leaf_dry', 'leaf_dry', 'leaf_dry', 'leaf_brown', 'leaf_green']
BROWN = ['leaf_brown', 'leaf_brown', 'leaf_brown', 'leaf_dry']

limb(m, [(0, 0, 0), (0.05, 0.02, 0.4), (0.16, -0.03, 0.95), (0.12, -0.12, 1.45), (0.2, -0.15, 1.9)],
     [0.18, 0.155, 0.135, 0.13, 0.095], BARK, rng, seg=7, gnarl=0.14)
for a, l in ((70, 0.36), (190, 0.4), (310, 0.34)):
    c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
    limb(m, [(c * 0.05, s * 0.05, 0.36), (c * 0.2, s * 0.2, 0.12), (c * l, s * l, 0.01)], [0.085, 0.07, 0.03], BARK, rng, seg=5)
main = [
    [(0.14, -0.1, 1.35), (-0.3, -0.15, 1.95), (-0.75, 0.05, 2.5), (-1.0, 0.2, 3.25)],
    [(0.18, -0.14, 1.75), (0.65, -0.35, 2.3), (1.0, -0.55, 2.95), (1.1, -0.75, 3.5)],
    [(0.2, -0.14, 1.8), (0.28, 0.25, 2.5), (0.2, 0.7, 3.2), (0.3, 0.95, 3.75)],
    [(0.18, -0.15, 1.85), (0.02, -0.5, 2.6), (-0.15, -0.85, 3.3), (-0.2, -1.0, 3.95)],
]
for pts in main:
    limb(m, pts, [0.085, 0.07, 0.05, 0.025], BARK, rng, seg=5, gnarl=0.14)
for pts in (
    [(-0.75, 0.05, 2.5), (-1.2, -0.3, 2.85), (-1.45, -0.45, 2.95)],
    [(0.65, -0.35, 2.3), (1.1, 0.05, 2.65), (1.4, 0.3, 2.8)],
    [(0.28, 0.25, 2.5), (-0.25, 0.6, 2.95), (-0.5, 0.95, 3.05)],
    [(0.02, -0.5, 2.6), (0.5, -0.8, 3.0), (0.65, -1.15, 3.1)],
    [(0.2, 0.7, 3.2), (0.65, 0.8, 3.6), (0.8, 0.9, 3.7)],
):
    limb(m, pts, [0.04, 0.028, 0.012], ['bark_dead', 'bark_light'], rng, seg=4, gnarl=0.1)

clump(m, (1.25, 1.15, 0.6), (-1.0, 0.22, 3.4), DRY)
clump(m, (1.1, 1.0, 0.55), (1.15, -0.8, 3.6), DRY)
clump(m, (1.3, 1.2, 0.65), (0.3, 1.0, 3.9), DRY)
clump(m, (1.2, 1.1, 0.6), (-0.2, -1.05, 4.1), BROWN)
clump(m, (0.7, 0.65, 0.35), (0.9, 0.95, 3.75), BROWN, subdiv=1)
# Off-centre secondary clumps so each crown is an irregular shelf rather than a disc on a stick
tufts(m, [(0.62, (-1.3, -0.1, 3.3)), (0.55, (-0.75, 0.6, 3.55)), (0.6, (1.3, -0.45, 3.45)), (0.5, (0.85, -1.15, 3.7)),
          (0.65, (-0.15, 1.2, 3.95)), (0.6, (0.3, -1.35, 4.05)), (0.55, (-0.55, -0.85, 4.25)), (0.45, (1.32, 0.3, 2.85)), (0.42, (-1.42, -0.45, 3.0))], DRY)
finish(m, bounds={'x': (-1.8, 1.8), 'y': (-1.8, 1.8), 'z': (0, 4.8)})
