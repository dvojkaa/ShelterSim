"""First-person claw/sledge hammer. Upright, grip middle at origin, length +Z, striking face +Y, claw -Y."""
import math
from mathutils import Vector
from ss_art import Model, finish

m = Model('melee_hammer', seed=211)
def seg(a, b, ra, rb, color, n=8):
    a, b = Vector(a), Vector(b); d = b - a
    e = d.to_track_quat('Z', 'Y').to_euler()
    m.cyl(ra, d.length, (a + b) / 2, color, rot=tuple(math.degrees(x) for x in e), seg=n, r2=rb)
def ax(z0, z1, r0, r1, color, n=8): seg((0, 0, z0), (0, 0, z1), r0, r1, color, n)

# haft: oval-ish wood, swelling knob at the butt
ax(-0.25, -0.235, 0.03, 0.034, 'wood_dark')
ax(-0.235, -0.22, 0.034, 0.026, 'wood_dark')
ax(-0.22, 0.2, 0.022, 0.025, 'wood')
ax(0.2, 0.5, 0.025, 0.03, 'wood_light')
# worn grip tape, wound turns
for i in range(7):
    z = -0.2 + i * 0.04
    seg((-0.002, 0, z - 0.02), (0.002, 0, z + 0.02), 0.0285, 0.0295, 'rubber' if i % 2 == 0 else 'fabric_grey', 8)
m.box((0.03, 0.004, 0.05), (0.025, 0.02, 0.1), 'fabric_grey', rot=(0, 20, 35))
m.box((0.02, 0.02, 0.03), (-0.027, 0.0, 0.3), 'wood_dark')   # nick
# head: steel block through the haft, collar plate, face and claw
zc = 0.53
m.box((0.06, 0.11, 0.075), (0, 0.0, zc), 'steel_dark', bevel=0.008)
m.box((0.066, 0.05, 0.082), (0, 0.0, zc), 'gunmetal', bevel=0.006)
m.cyl(0.043, 0.05, (0, 0.06, zc), 'steel', rot=(-90, 0, 0), seg=10, r2=0.04)   # striking neck
m.cyl(0.046, 0.012, (0, 0.092, zc), 'chrome', rot=(-90, 0, 0), seg=10, r2=0.044)  # worn face
for sx in (-1, 1):   # twin claw tines curving down and back
    x = sx * 0.012
    seg((x, -0.05, zc + 0.012), (x, -0.095, zc + 0.006), 0.021, 0.014, 'steel_dark', 6)
    seg((x, -0.095, zc + 0.006), (x, -0.125, zc - 0.022), 0.014, 0.004, 'steel', 6)
m.box((0.062, 0.02, 0.004), (0, 0.0, zc + 0.04), 'rust', rot=(0, 0, 0))
m.box((0.02, 0.03, 0.02), (0.03, 0.03, zc + 0.01), 'rust_dark')
# wedge in the eye
m.box((0.01, 0.03, 0.012), (0, -0.0, zc + 0.0415), 'wood_light')
finish(m, bounds={'x': (-0.08, 0.08), 'y': (-0.16, 0.12), 'z': (-0.25, 0.58)})
