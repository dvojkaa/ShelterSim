"""First-person red fire axe. Grip middle at origin, length +Z, cutting edge toward +Y, pick on -Y."""
import math
from mathutils import Vector
from ss_art import Model, finish

m = Model('melee_fire_axe', seed=73)
R = (0, 0, 90)


def seg(a, b, ra, rb, color, n=8):
    a, b = Vector(a), Vector(b)
    d = b - a
    e = d.to_track_quat('Z', 'Y').to_euler()
    m.cyl(ra, d.length, (a + b) / 2, color, rot=tuple(math.degrees(x) for x in e), seg=n, r2=rb)


def ax(z0, z1, r0, r1, color, n=8):
    seg((0, 0, z0), (0, 0, z1), r0, r1, color, n)


# Haft: butt knob, slim hand section, gentle swell, narrowing into the head
ax(-0.28, -0.265, 0.026, 0.032, 'wood_dark')
ax(-0.265, -0.2, 0.032, 0.024, 'wood_dark')
ax(-0.2, -0.02, 0.024, 0.023, 'wood')
ax(-0.02, 0.2, 0.023, 0.028, 'wood_light')
ax(0.2, 0.42, 0.028, 0.025, 'wood')
ax(0.42, 0.56, 0.025, 0.022, 'wood_light')
# Worn grip wrap
for i in range(7):
    z = -0.19 + i * 0.03
    seg((-0.002, 0, z - 0.012), (0.002, 0, z + 0.012), 0.0265, 0.0265, 'leather_dark' if i % 2 else 'rubber', 8)
ax(-0.22, -0.2, 0.0275, 0.0275, 'fabric_grey')
# Scuffs / grime
m.box((0.012, 0.02, 0.05), (0.027, 0.0, 0.3), 'wood_dark', rot=(0, 0, 8))
m.box((0.02, 0.012, 0.03), (-0.002, -0.029, 0.12), 'wood_dark')
m.box((0.03, 0.012, 0.02), (0.01, 0.03, 0.4), 'wood_dark', rot=(0, 0, 20))
# Eye collar/wedge (steel) where haft meets head
ax(0.455, 0.5, 0.032, 0.026, 'steel_dark')

# Head: red painted steel; pick spike toward -Y, wide curved blade toward +Y
head = [(-0.09, 0.60), (-0.04, 0.575), (-0.03, 0.50), (0.03, 0.50), (0.10, 0.44), (0.135, 0.47),
        (0.15, 0.575), (0.135, 0.69), (0.10, 0.72), (0.03, 0.68), (-0.04, 0.67)]
m.poly(head, 0.034, (0, 0, 0), 'warning_red', rot=R)
# Raised eye boss
m.box((0.05, 0.075, 0.18), (0, -0.002, 0.585), 'warning_red', bevel=0.006)
# Bare bright steel edge (broken by a chip) and steel pick tip
m.poly([(0.12, 0.455), (0.135, 0.47), (0.15, 0.575), (0.135, 0.69), (0.105, 0.70), (0.115, 0.575), (0.105, 0.47)], 0.036, (0, 0, 0), 'chrome', rot=R)
m.poly([(-0.09, 0.60), (-0.07, 0.59), (-0.07, 0.608)], 0.036, (0, 0, 0), 'steel', rot=R)
# Chips in the paint on the head, dark scratch, chipped edge nick
m.box((0.036, 0.03, 0.02), (0, 0.07, 0.69), 'rust_dark', rot=(0, 0, 0))
m.box((0.037, 0.02, 0.016), (0, 0.09, 0.50), 'steel', rot=(25, 0, 0))
m.box((0.036, 0.04, 0.008), (0, -0.07, 0.60), 'steel_dark', rot=(-15, 0, 0))
m.box((0.036, 0.012, 0.03), (0, 0.145, 0.62), 'rust', rot=(0, 0, 0))
m.box((0.035, 0.02, 0.012), (0, 0.126, 0.53), 'rust_dark')

finish(m, bounds={'x': (-0.04, 0.04), 'y': (-0.10, 0.16), 'z': (-0.28, 0.72)})
