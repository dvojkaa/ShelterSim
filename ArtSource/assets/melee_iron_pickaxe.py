"""First-person iron pickaxe: double curved pick head along Y. Upright, grip at origin, +Z up."""
import math
from mathutils import Vector
from ss_art import Model, finish

m = Model('melee_iron_pickaxe', seed=213)
def seg(a, b, ra, rb, color, n=8):
    a, b = Vector(a), Vector(b); d = b - a
    e = d.to_track_quat('Z', 'Y').to_euler()
    m.cyl(ra, d.length, (a + b) / 2, color, rot=tuple(math.degrees(x) for x in e), seg=n, r2=rb)
def ax(z0, z1, r0, r1, color, n=8): seg((0, 0, z0), (0, 0, z1), r0, r1, color, n)

ax(-0.28, -0.265, 0.03, 0.034, 'wood_dark')
ax(-0.265, -0.25, 0.034, 0.024, 'wood_dark')
ax(-0.25, 0.2, 0.021, 0.025, 'wood')
ax(0.2, 0.72, 0.025, 0.027, 'wood_light')
for i in range(6):
    z = -0.17 + i * 0.045
    seg((-0.002, 0, z - 0.024), (0.002, 0, z + 0.024), 0.0262, 0.0275, 'rubber' if i % 2 == 0 else 'fabric_grey', 8)
m.box((0.012, 0.02, 0.03), (-0.026, 0.0, 0.4), 'wood_dark', rot=(0, 0, 20))
m.box((0.02, 0.012, 0.04), (0.0, 0.026, 0.52), 'wood_dark')
# head: central eye block, two tapering curved arms drooping at the tips
zc = 0.71
m.box((0.05, 0.09, 0.08), (0, 0, zc), 'rust_dark', bevel=0.008)
m.box((0.058, 0.04, 0.086), (0, 0, zc), 'gunmetal', bevel=0.006)
for s in (1, -1):
    pts = [(0.04, zc + 0.002, 0.034), (0.11, zc + 0.006, 0.03), (0.17, zc - 0.014, 0.02), (0.205, zc - 0.05, 0.011), (0.22, zc - 0.085, 0.0025)]
    P = [(0, s * y, z) for y, z, _ in pts]
    R = [r for _, _, r in pts]
    for i in range(4):
        col = 'steel_dark' if i < 2 else 'steel'
        if i == 1: col = 'rust'
        seg(P[i], P[i + 1], R[i], R[i + 1], col, 6)
m.box((0.02, 0.03, 0.02), (0.027, 0.11, zc + 0.02), 'rust_light', rot=(0, 0, 8))
# binding where head meets haft
for i in range(5):
    z = 0.59 + i * 0.022
    seg((0, 0, z), (0, 0, z + 0.024), 0.0335, 0.0335, 'rope' if i % 2 == 0 else 'leather', 8)
m.box((0.01, 0.02, 0.012), (0, 0.0, zc + 0.043), 'wood_light')   # wedge
finish(m, bounds={'x': (-0.08, 0.08), 'y': (-0.24, 0.24), 'z': (-0.28, 0.78)})
