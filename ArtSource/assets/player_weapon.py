"""First-person scavenger bat. Origin at grip middle, length along +Z, handle at -Z. Seen from behind/above."""
import math
from mathutils import Vector
from ss_art import Model, finish

m = Model('player_weapon', seed=37)
N = 10


def seg(a, b, ra, rb, color, n=N):
    a, b = Vector(a), Vector(b)
    d = b - a
    e = d.to_track_quat('Z', 'Y').to_euler()
    m.cyl(ra, d.length, (a + b) / 2, color, rot=tuple(math.degrees(x) for x in e), seg=n, r2=rb)


def ax(z0, z1, r0, r1, color, n=N):
    seg((0, 0, z0), (0, 0, z1), r0, r1, color, n)


# Bat body: knob, thin handle, swelling barrel, domed end. Stacked frustums read as turned wood.
ax(-0.28, -0.262, 0.03, 0.036, 'wood_dark')
ax(-0.262, -0.245, 0.036, 0.024, 'wood_dark')
ax(-0.25, 0.10, 0.021, 0.024, 'wood')
ax(0.10, 0.22, 0.024, 0.033, 'wood_light')
ax(0.22, 0.42, 0.033, 0.044, 'wood_light')
ax(0.42, 0.56, 0.044, 0.047, 'wood')
ax(0.56, 0.585, 0.047, 0.04, 'wood')
ax(0.585, 0.6, 0.04, 0.022, 'wood_dark')

# Grip wrap: overlapping tape turns, slightly tilted so it reads as wound.
for i in range(9):
    z = -0.225 + i * 0.039
    seg((-0.002, -0.002, z - 0.022), (0.002, 0.002, z + 0.022), 0.0275, 0.029, 'black' if i % 2 == 0 else 'rubber', 8)
ax(0.128, 0.142, 0.0275, 0.029, 'cloth_red')
ax(0.142, 0.16, 0.029, 0.03, 'fabric_grey')
# Loose tape tail off the top of the grip.
m.box((0.024, 0.004, 0.06), (0.022, -0.02, 0.135), 'fabric_grey', rot=(0, 18, 40))

# Barrel: two tape bands, a strapped scrap sleeve held with hose clamps, wood damage.
ax(0.255, 0.283, 0.0375, 0.039, 'fabric_grey')
ax(0.278, 0.296, 0.039, 0.04, 'fabric_stain')
ax(0.515, 0.535, 0.047, 0.048, 'fabric_grey')
# Scrap sheet riveted down one flank of the barrel.
m.box((0.012, 0.05, 0.14), (0.0415, 0.0, 0.40), 'rust', rot=(0, -2, 0))
m.box((0.012, 0.018, 0.14), (0.0455, -0.012, 0.40), 'rust_dark', rot=(0, -2, 0))
for z in (0.345, 0.455):
    ax(z - 0.008, z + 0.008, 0.0475 if z > 0.4 else 0.0435, 0.048 if z > 0.4 else 0.044, 'steel', N)
# Knock marks and stains.
m.box((0.05, 0.01, 0.024), (-0.024, -0.034, 0.34), 'wood_dark', rot=(0, 0, 25))
m.box((0.012, 0.022, 0.04), (-0.0395, 0.012, 0.47), 'wood_dark')
m.box((0.03, 0.012, 0.012), (0.012, 0.042, 0.495), 'cloth_red', rot=(0, 0, -20))

# Nails: driven right through, head on one side. Angle in XY, z along the barrel, slight bends.
NAILS = [
    (0.335, 30, 0.078, 0.0), (0.385, 115, 0.076, 0.004), (0.43, 205, 0.078, -0.004), (0.475, 300, 0.078, 0.006),
    (0.51, 65, 0.078, 0.0), (0.55, 160, 0.078, -0.006), (0.46, 20, 0.076, 0.0), (0.56, 340, 0.078, 0.004),
    (0.37, 245, 0.074, 0.0),
]
for z, ang, reach, bend in NAILS:
    c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    r = 0.033 + (z - 0.22) * 0.06
    seg((c * r, s * r, z), (c * reach, s * reach, z + bend), 0.0065, 0.0014, 'chrome', 5)
    seg((c * (r - 0.002), s * (r - 0.002), z), (c * (r + 0.0035), s * (r + 0.0035), z), 0.0105, 0.0095, 'rust_dark', 5)

finish(m, bounds={'x': (-0.08, 0.08), 'y': (-0.08, 0.08), 'z': (-0.28, 0.6)})
