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

# tree_c: the half-dead one. Living crown on the +X side only; -X side is bare grey snapped branches, top of the trunk is broken off.
m = Model('tree_c', seed=103)
rng = random.Random(1103)
BARK = ['bark', 'bark', 'bark_light', 'bark_dead']
DEAD = ['bark_dead', 'bark_dead', 'wood_grey']
LIVE = ['leaf_green', 'leaf_green', 'leaf_green', 'leaf_dark', 'leaf_dark', 'leaf_dry']
LIVE_LOW = ['leaf_dark', 'leaf_dark', 'leaf_dark', 'leaf_green', 'leaf_dry', 'leaf_brown']

limb(m, [(0, 0, 0), (-0.02, 0.02, 0.5), (-0.1, 0.05, 1.1), (-0.06, 0, 1.7), (-0.12, -0.05, 2.4), (-0.1, -0.08, 3.2)],
     [0.19, 0.16, 0.145, 0.13, 0.11, 0.08], BARK, rng, seg=7, gnarl=0.13)
# Splintered top
for d, h in (((0.05, 0.02), 0.5), ((-0.05, 0.05), 0.32), ((0.0, -0.07), 0.4)):
    limb(m, [(-0.1 + d[0], -0.08 + d[1], 3.1), (-0.1 + d[0] * 1.6, -0.08 + d[1] * 1.6, 3.2 + h)], [0.06, 0.008], ['wood_grey', 'bark_dead'], rng, seg=4, gnarl=0.05)
for a, l in ((40, 0.38), (165, 0.42), (285, 0.36)):
    c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
    limb(m, [(c * 0.05, s * 0.05, 0.42), (c * 0.2, s * 0.2, 0.13), (c * l, s * l, 0.01)], [0.09, 0.075, 0.03], BARK, rng, seg=5)

# Living side (+X)
for pts, r in (
    ([(-0.05, 0, 1.75), (0.35, -0.1, 2.2), (0.75, -0.2, 2.75), (0.95, -0.25, 3.2)], 0.095),
    ([(-0.1, -0.03, 2.2), (0.2, 0.3, 2.7), (0.55, 0.65, 3.3), (0.6, 0.75, 3.6)], 0.08),
    ([(-0.1, -0.05, 2.5), (0.2, -0.45, 3.0), (0.45, -0.8, 3.55)], 0.07),
):
    limb(m, pts, [r * (1 - 0.7 * i / (len(pts) - 1)) for i in range(len(pts))], BARK, rng, seg=5)
clump(m, (1.45, 1.4, 1.1), (0.92, -0.25, 3.3), LIVE_LOW)
clump(m, (1.35, 1.3, 1.1), (0.6, 0.7, 3.75), LIVE)
clump(m, (1.2, 1.2, 0.95), (0.45, -0.85, 3.8), LIVE)
clump(m, (0.8, 0.75, 0.55), (1.15, 0.4, 2.85), LIVE_LOW, subdiv=1)
tufts(m, [(0.6, (0.25, 0.3, 4.1)), (0.62, (1.1, 0.25, 3.8)), (0.55, (0.95, -0.75, 3.85)), (0.55, (1.2, -0.75, 2.95)), (0.5, (0.2, -0.45, 3.35)), (0.5, (0.4, 1.15, 3.35))], LIVE)

# Dead side (-X): grey, forked, snapped ends showing pale wood
for pts, r in (
    ([(-0.08, 0.03, 1.6), (-0.55, 0.1, 2.15), (-1.0, 0.05, 2.75), (-1.3, -0.1, 3.3)], 0.085),
    ([(-0.12, -0.05, 2.35), (-0.5, -0.4, 2.85), (-0.8, -0.75, 3.5)], 0.065),
    ([(-0.11, -0.03, 2.75), (-0.4, 0.4, 3.3), (-0.65, 0.7, 4.0)], 0.055),
):
    limb(m, pts, [r * (1 - 0.72 * i / (len(pts) - 1)) for i in range(len(pts))], DEAD, rng, seg=5)
for pts in (
    [(-0.55, 0.1, 2.15), (-0.95, 0.55, 2.4), (-1.3, 0.75, 2.45)],
    [(-1.0, 0.05, 2.75), (-1.45, 0.3, 2.95), (-1.6, 0.35, 3.15)],
    [(-0.5, -0.4, 2.85), (-1.0, -0.6, 2.95), (-1.25, -0.9, 3.05)],
    [(-0.4, 0.4, 3.3), (-0.95, 0.45, 3.7)],
):
    limb(m, pts, [0.035, 0.012] if len(pts) == 2 else [0.038, 0.024, 0.01], DEAD, rng, seg=4, gnarl=0.06)
finish(m, bounds={'x': (-1.8, 1.8), 'y': (-1.8, 1.8), 'z': (0, 4.8)})
