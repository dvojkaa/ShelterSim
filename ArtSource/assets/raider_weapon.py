"""Raider's spiked pipe club. Origin at grip, length along +Y."""
import math
from mathutils import Vector
from ss_art import Model, finish

m = Model('raider_weapon', seed=31)


def seg(a, b, ra, rb, color, n=8):
    a, b = Vector(a), Vector(b)
    d = b - a
    e = d.to_track_quat('Z', 'Y').to_euler()
    m.cyl(ra, d.length, (a + b) / 2, color, rot=tuple(math.degrees(x) for x in e), seg=n, r2=rb)


# Pipe shaft: pommel nut, wrapped grip, bare pipe.
seg((0, -0.25, 0), (0, 0.46, 0), 0.026, 0.028, 'steel_dark')
seg((0, -0.285, 0), (0, -0.235, 0), 0.042, 0.042, 'gunmetal', 6)
for i in range(6):
    y = -0.19 + i * 0.058
    seg((0, y - 0.022, 0), (0, y + 0.024, 0), 0.036, 0.034, 'leather' if i % 2 == 0 else 'leather_dark', 8)
seg((0, 0.135, 0), (0, 0.165, 0), 0.041, 0.041, 'black', 8)

# Red rag tied below the head, tails hanging down (-Z).
seg((0, 0.385, 0), (0, 0.44, 0), 0.037, 0.035, 'warning_red', 8)
m.poly([(-0.03, 0.0), (0.03, 0.0), (0.038, -0.11), (0.012, -0.075), (-0.012, -0.15), (-0.035, -0.1)], 0.01,
       (0, 0.41, -0.028), 'warning_red', rot=(0, 0, 90))

# Head: fat threaded coupling on a rusted pipe, flared and capped.
seg((0, 0.44, 0), (0, 0.50, 0), 0.05, 0.062, 'rust_dark', 8)
seg((0, 0.50, 0), (0, 0.80, 0), 0.066, 0.07, 'rust', 8)
seg((0, 0.53, 0), (0, 0.575, 0), 0.076, 0.076, 'steel_dark', 8)
seg((0, 0.70, 0), (0, 0.745, 0), 0.08, 0.08, 'steel_dark', 8)
seg((0, 0.80, 0), (0, 0.84, 0), 0.08, 0.06, 'gunmetal', 8)

# Spikes: long in the +-Z plane, shorter sideways so the head stays inside x 0.25. Welded with collars.
SPIKES = [
    (0.61, 90, 0.18), (0.61, 270, 0.18),
    (0.65, 0, 0.11), (0.65, 180, 0.11),
    (0.78, 40, 0.15), (0.78, 140, 0.15), (0.78, 220, 0.15), (0.78, 320, 0.15),
    (0.56, 35, 0.13), (0.56, 215, 0.13),
]
for y, ang, reach in SPIKES:
    c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    seg((c * 0.05, y, s * 0.05), (c * reach, y + 0.012, s * reach), 0.025, 0.002, 'steel', 5)
    seg((c * 0.058, y, s * 0.058), (c * 0.082, y + 0.003, s * 0.082), 0.03, 0.022, 'rust_dark', 5)

# Tip spike out of the cap, jagged sheet fin welded along the top.
seg((0, 0.83, 0), (0, 0.9, 0), 0.034, 0.002, 'steel', 6)
m.poly([(-0.14, 0.0), (0.14, 0.0), (0.10, 0.045), (0.06, 0.02), (0.03, 0.06), (-0.02, 0.025), (-0.06, 0.05), (-0.09, 0.02)], 0.012,
       (0, 0.66, 0.06), 'steel_dark', rot=(0, 0, 90))

finish(m, bounds={'x': (-0.125, 0.125), 'y': (-0.3, 0.9), 'z': (-0.2, 0.2)})
