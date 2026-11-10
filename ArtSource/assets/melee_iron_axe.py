"""First-person woodcutting axe, dark iron single bit. Upright, grip at origin, +Z up, edge toward +Y."""
import math
from mathutils import Vector
from ss_art import Model, finish

m = Model('melee_iron_axe', seed=212)
def seg(a, b, ra, rb, color, n=8):
    a, b = Vector(a), Vector(b); d = b - a
    e = d.to_track_quat('Z', 'Y').to_euler()
    m.cyl(ra, d.length, (a + b) / 2, color, rot=tuple(math.degrees(x) for x in e), seg=n, r2=rb)
def ax(z0, z1, r0, r1, color, n=8): seg((0, 0, z0), (0, 0, z1), r0, r1, color, n)
def slab(profile, th, color):   # profile in (y, z), extruded along x
    m.poly(profile, th, (0, 0, 0), color, rot=(0, 0, 90))

ax(-0.28, -0.265, 0.03, 0.034, 'wood_dark')
ax(-0.265, -0.25, 0.034, 0.024, 'wood_dark')
ax(-0.25, 0.2, 0.021, 0.024, 'wood_light')
ax(0.2, 0.58, 0.024, 0.026, 'wood')
ax(0.58, 0.66, 0.026, 0.022, 'wood_light')
# worn hand wrap, haft nicks and a grease stain
for i in range(5):
    z = -0.16 + i * 0.045
    seg((-0.002, 0, z - 0.024), (0.002, 0, z + 0.024), 0.0262, 0.0272, 'leather_dark' if i % 2 == 0 else 'leather', 8)
m.box((0.012, 0.02, 0.03), (-0.024, 0.0, 0.34), 'wood_dark', rot=(0, 0, 20))
m.box((0.012, 0.02, 0.02), (0.025, 0.0, 0.42), 'wood_dark')
m.box((0.01, 0.02, 0.04), (0.0, -0.025, 0.12), 'wood_dark')
# head: eye block + cheeks, flared bit, bevelled edge (three stacked slabs thinning to the edge)
slab([(-0.06, 0.565), (0.07, 0.56), (0.07, 0.69), (-0.06, 0.68)], 0.046, 'gunmetal')            # eye / poll
slab([(0.05, 0.575), (0.16, 0.52), (0.16, 0.74), (0.05, 0.675)], 0.026, 'rust_dark')             # blade body
slab([(0.12, 0.535), (0.19, 0.505), (0.19, 0.745), (0.12, 0.71)], 0.012, 'steel_dark')           # bevel
slab([(0.17, 0.515), (0.205, 0.505), (0.205, 0.745), (0.17, 0.725)], 0.004, 'steel')             # bright edge
slab([(-0.045, 0.6), (-0.075, 0.6), (-0.075, 0.65), (-0.045, 0.65)], 0.04, 'rust')               # poll cap
# wedge in the eye, plus rust blotches
m.box((0.012, 0.03, 0.02), (0, 0.0, 0.695), 'wood_light')
m.box((0.05, 0.03, 0.03), (0.0, 0.04, 0.575), 'rust_light', rot=(0, 0, 0)) if False else None
m.box((0.004, 0.05, 0.05), (0.0145, 0.1, 0.65), 'rust', rot=(0, 0, 10))
finish(m, bounds={'x': (-0.08, 0.08), 'y': (-0.1, 0.22), 'z': (-0.28, 0.75)})
