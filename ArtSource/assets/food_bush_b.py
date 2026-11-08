import math, random
import bmesh
from mathutils import Matrix, Vector
from ss_art import Model, finish

def skirt(m, cx, cy, z_tip, z_top, r, teeth, colors, rng, droop=0.09):
    """Shaggy spruce tier: star-shaped cone with drooping tips, capped underneath. Returns tip positions + apex for berry placing."""
    t = bmesh.new(); t.loops.layers.uv.new('UVMap')
    apex = t.verts.new((cx, cy, z_top))
    centre = t.verts.new((cx, cy, z_tip + droop * 0.8))
    ring, tips = [], []
    start = rng.uniform(0, 6.28)
    for k in range(teeth * 2):
        a = start + math.pi * k / teeth
        outer = k % 2 == 0
        rr = r * (rng.uniform(0.92, 1.0) if outer else rng.uniform(0.62, 0.7))
        z = z_tip if outer else z_tip + droop
        v = t.verts.new((cx + math.cos(a) * rr, cy + math.sin(a) * rr, z))
        ring.append(v)
        if outer:
            tips.append(Vector(v.co))
    n = len(ring)
    for k in range(n):
        t.faces.new((ring[k], ring[(k + 1) % n], apex))
        t.faces.new((ring[(k + 1) % n], ring[k], centre))
    bmesh.ops.recalc_face_normals(t, faces=t.faces)
    for f in t.faces:
        m.default._paint(t, [f], rng.choice(colors))
    m.default._commit(t, Matrix.Identity(4))
    return tips, Vector((cx, cy, z_top))

def berry(m, pos, d, color, rng):
    m.blob((d, d, d), tuple(pos), color, rough=0.05, subdiv=1)

def berries_on(m, tips, apex, rng, fracs, d=0.1):
    """Sit berries on each tooth ridge, half sunk so nothing floats. Mostly deep blue with light highlights."""
    for tip in tips:
        for f in fracs:
            p = tip.lerp(apex, f + rng.uniform(-0.04, 0.04))
            col = 'berry_light' if rng.random() < 0.35 else 'berry_blue'
            berry(m, p, d * rng.uniform(0.9, 1.15), col, rng)

# food_bush_b: squatter, wider, slightly lopsided bush with a smaller second leader at its side.
m = Model('food_bush_b', seed=107)
rng = random.Random(1107)
G = ['spruce', 'spruce_light', 'spruce_light', 'leaf_dark']
GD = ['spruce', 'spruce', 'leaf_dark', 'spruce_light']
m.cyl(0.08, 0.26, (0.0, 0.0, 0.13), 'bark', seg=6, r2=0.06)
for z0, z1, r, n, cols, fr in [(0.12, 0.72, 0.5, 8, GD, (0.12, 0.42)), (0.44, 1.04, 0.4, 7, G, (0.12, 0.42)), (0.8, 1.4, 0.27, 6, G, (0.18,))]:
    tips, apex = skirt(m, 0.08, 0.03, z0, z1, r, n, cols, rng)
    berries_on(m, tips, apex, rng, fr, d=0.11)
for z0, z1, r, n, cols, fr in [(0.1, 0.62, 0.3, 6, GD, (0.14,)), (0.38, 0.92, 0.2, 5, G, (0.18,))]:
    tips, apex = skirt(m, -0.24, -0.2, z0, z1, r, n, cols, rng)
    berries_on(m, tips, apex, rng, fr, d=0.11)
finish(m, bounds={'x': (-0.6, 0.6), 'y': (-0.6, 0.6), 'z': (0, 1.8)})
