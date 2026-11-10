"""First-person spiked club. Grip middle at origin, length +Z, spikes toward +Y."""
import math
from mathutils import Vector
from ss_art import Model, finish

m = Model('melee_spiked_club', seed=61)


def seg(a, b, ra, rb, color, n=8):
    a, b = Vector(a), Vector(b)
    d = b - a
    e = d.to_track_quat('Z', 'Y').to_euler()
    m.cyl(ra, d.length, (a + b) / 2, color, rot=tuple(math.degrees(x) for x in e), seg=n, r2=rb)


def ax(z0, z1, r0, r1, color, n=8):
    seg((0, 0, z0), (0, 0, z1), r0, r1, color, n)


# Rough-hewn club: knob, thin neck, fat lumpy head.
ax(-0.22, -0.20, 0.030, 0.036, 'wood_dark')
ax(-0.20, -0.18, 0.036, 0.024, 'wood_dark')
ax(-0.18, 0.10, 0.022, 0.026, 'wood')
ax(0.10, 0.24, 0.026, 0.042, 'wood_light')
ax(0.24, 0.40, 0.042, 0.058, 'wood')
ax(0.40, 0.54, 0.058, 0.062, 'wood_dark')
ax(0.54, 0.60, 0.062, 0.050, 'wood')
ax(0.60, 0.62, 0.050, 0.026, 'wood_dark')
# chopped lump and split
m.blob((0.03, 0.03, 0.05), (0.045, -0.03, 0.46), 'wood_light', rough=0.15, subdiv=0)
m.box((0.012, 0.03, 0.10), (-0.055, 0.0, 0.52), 'bark', rot=(0, 0, 10))

# Wrapped grip: cloth turns + tape.
for i in range(9):
    z = -0.165 + i * 0.026
    seg((-0.002, -0.002, z - 0.016), (0.002, 0.002, z + 0.016), 0.0275, 0.0285, 'cloth_red' if i % 3 == 0 else ('fabric_stain' if i % 3 == 1 else 'cloth_dark'), 8)
# tail of cloth
m.box((0.02, 0.004, 0.07), (0.026, -0.022, -0.02), 'cloth_red', rot=(0, 18, 35))

# Leather wrist loop from the butt.
m.tube([(0, -0.018, -0.17), (0.012, -0.06, -0.20), (0.03, -0.095, -0.24), (0.0, -0.11, -0.275), (-0.03, -0.085, -0.24), (-0.015, -0.05, -0.19), (0, -0.02, -0.165)], 0.0085, 'leather', seg=4)
ax(0.095, 0.105, 0.0275, 0.0275, 'leather_dark')

# Iron bands on the head.
ax(0.27, 0.29, 0.0455, 0.0465, 'steel_dark')
ax(0.50, 0.52, 0.0625, 0.0635, 'rust_dark')

# Spikes: (z, angle deg in XY, reach, tilt up). 90deg = +Y (cutting side) heavily populated.
SPIKES = [
    (0.34, 90, 0.125, 0.0), (0.42, 70, 0.14, 0.02), (0.46, 110, 0.14, 0.02), (0.52, 90, 0.15, 0.03),
    (0.58, 90, 0.11, 0.05), (0.38, 40, 0.12, 0.0), (0.45, 150, 0.12, 0.0), (0.50, 20, 0.12, 0.02),
    (0.55, 165, 0.12, 0.02), (0.30, 120, 0.10, 0.0),
]
for z, ang, reach, up in SPIKES:
    c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    r = 0.040 + (z - 0.24) * 0.12
    col = 'chrome' if int(z * 100) % 2 else 'steel'
    seg((c * (r - 0.01), s * (r - 0.01), z), (c * reach, s * reach, z + up), 0.0075, 0.0012, "rust_light" if col == "chrome" else "rust", 4)
    seg((c * (r - 0.003), s * (r - 0.003), z), (c * (r + 0.007), s * (r + 0.007), z), 0.0125, 0.0115, "rust_dark", 5)
# Spike capping the top.
seg((0, 0.005, 0.60), (0, 0.01, 0.66), 0.0085, 0.0012, 'rust', 5)
# Long bolt through the head with a nut.
seg((-0.075, 0, 0.36), (0.09, 0, 0.36), 0.006, 0.006, 'steel_dark', 6)
m.cyl(0.011, 0.012, (-0.072, 0, 0.36), 'steel', rot=(0, 90, 0), seg=6)

finish(m, bounds={'x': (-0.18, 0.18), 'y': (-0.16, 0.18), 'z': (-0.3, 0.68)})
