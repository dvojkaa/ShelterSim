"""Caged hanging bunker lamp. Origin = ceiling attach point; hangs down to z=-0.5."""
import math
import random
from ss_art import Model, finish
m = Model('bunker_lamp', seed=23)
flake_rng = random.Random(2301)


def fr(r_bottom, r_top, z_bottom, z_top, c, seg=12, x=0.0, y=0.0):
    m.cyl(r_bottom, z_top - z_bottom, (x, y, (z_bottom + z_top) / 2), c, seg=seg, r2=r_top)


def ring(r, z, pr, c, n=12, seg=4):
    pts = [(r * math.cos(2 * math.pi * i / n), r * math.sin(2 * math.pi * i / n), z) for i in range(n + 1)]
    m.tube(pts, pr, c, seg=seg)


def patch(angle, z, w, h, c):
    """Thin flake of paint/rust lying on the sloped shade cone (r 0.215 @ z=-0.275 up to r 0.085 @ z=-0.165)."""
    a = math.radians(angle)
    r = 0.215 + (z + 0.275) * (0.085 - 0.215) / 0.11
    elev = math.degrees(math.atan2(0.13, 0.11))         # normal elevation of the cone surface
    rr = r * 0.985
    # Jagged outline rather than a clean rectangle, so it reads as flaked enamel, not a sticker.
    pts = []
    for i in range(7):
        t = 2 * math.pi * i / 7
        k = flake_rng.uniform(0.55, 1.0)
        pts.append((0.5 * w * k * math.cos(t), 0.5 * h * k * math.sin(t)))
    m.poly(pts, 0.016, (rr * math.cos(a), rr * math.sin(a), z), c, rot=(-elev, 0, angle + 90))


# Ceiling canopy, bolts, conduit stub feeding it along the ceiling
fr(0.085, 0.070, -0.025, 0.0, 'steel_dark', seg=10)
for a in (45, 225):
    fr(0.011, 0.011, -0.031, -0.02, 'chrome', seg=6, x=0.058 * math.cos(math.radians(a)), y=0.058 * math.sin(math.radians(a)))
m.tube([(0.03, 0.03, -0.012), (0.03, 0.03, -0.045), (0.18, 0.10, -0.045), (0.285, 0.10, -0.012)], 0.010, 'black', seg=5)

# Drop rod and knuckle: long enough that the lamp clearly hangs
m.tube([(0, 0, -0.02), (0, 0, -0.12)], 0.017, 'gunmetal', seg=8)
fr(0.032, 0.028, -0.078, -0.055, 'rust_dark', seg=8)

# Deep enamel shade, stepped like a pressed industrial reflector
fr(0.060, 0.040, -0.135, -0.11, 'steel_dark', seg=10)
fr(0.085, 0.060, -0.165, -0.135, 'army_green_dark', seg=12)
fr(0.215, 0.085, -0.275, -0.165, 'army_green', seg=12)
fr(0.240, 0.215, -0.312, -0.275, 'army_green_dark', seg=12)
fr(0.250, 0.250, -0.322, -0.312, 'steel_dark', seg=12)            # rolled rim
# Chipped enamel and rust, lying flat on the cone
patch(285, -0.235, 0.075, 0.05, 'rust_dark')
patch(135, -0.245, 0.06, 0.04, 'rust_dark')
patch(45, -0.21, 0.05, 0.03, 'steel_dark')

# Reflector underside lit by the bulb, and the socket
fr(0.238, 0.238, -0.328, -0.320, 'fabric_white', seg=12)
fr(0.180, 0.180, -0.334, -0.326, 'lamp_warm', seg=12)
fr(0.050, 0.044, -0.368, -0.333, 'black', seg=10)

# Fat bulb, large enough to read through the cage
fr(0.046, 0.052, -0.385, -0.362, 'lamp_warm', seg=10)
fr(0.086, 0.046, -0.412, -0.385, 'lamp_warm', seg=10)
fr(0.072, 0.086, -0.440, -0.412, 'lamp_warm', seg=10)
fr(0.018, 0.072, -0.458, -0.440, 'lamp_warm', seg=10)

# Wide cage: collar under the rim, belly ring, ribs into a bottom cap
ring(0.165, -0.33, 0.007, 'gunmetal')
ring(0.170, -0.410, 0.008, 'gunmetal')
for i in range(6):
    a = 2 * math.pi * i / 6 + 0.26
    c, s = math.cos(a), math.sin(a)
    m.tube([(0.165 * c, 0.165 * s, -0.33), (0.172 * c, 0.172 * s, -0.41),
            (0.115 * c, 0.115 * s, -0.468), (0.02 * c, 0.02 * s, -0.487)], 0.006, 'gunmetal', seg=4)
fr(0.030, 0.034, -0.498, -0.478, 'steel_dark', seg=8)

# Short pull chain off the rim
for i in range(5):
    m.box((0.008, 0.008, 0.024), (0.222, 0.0, -0.335 - i * 0.026), 'steel_dark' if i % 2 else 'chrome')
fr(0.014, 0.008, -0.495, -0.462, 'rust', seg=6, x=0.222)

finish(m, bounds={'x': (-0.3, 0.3), 'y': (-0.3, 0.3), 'z': (-0.5, 0)})
