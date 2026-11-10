"""First-person machete. Grip middle at origin, length +Z, edge toward +Y, flat faces +-X."""
import math
from mathutils import Vector
from ss_art import Model, finish

m = Model('melee_machete', seed=63)


def seg(a, b, ra, rb, color, n=8):
    a, b = Vector(a), Vector(b)
    d = b - a
    e = d.to_track_quat('Z', 'Y').to_euler()
    m.cyl(ra, d.length, (a + b) / 2, color, rot=tuple(math.degrees(x) for x in e), seg=n, r2=rb)


def blade(profile, thick, x, color):
    m.poly(profile, thick, (x, 0, 0), color, rot=(0, 0, 90))


# Cleaver blade: widens toward the tip (spine -0.030 .. edge +0.038 at z 0.50), clipped point.
P = [(-0.022, 0.06), (-0.024, 0.20), (-0.030, 0.40), (-0.036, 0.52), (-0.032, 0.58), (-0.004, 0.60),
     (0.030, 0.55), (0.040, 0.48), (0.036, 0.30), (0.030, 0.14), (0.026, 0.06)]
blade(P, 0.008, 0, 'steel')
# Bright sharpened edge strip along +Y.
E = [(0.030, 0.14), (0.036, 0.30), (0.040, 0.48), (0.030, 0.55), (-0.004, 0.60), (0.018, 0.52), (0.026, 0.40), (0.022, 0.20), (0.020, 0.14)]
blade(E, 0.0092, 0, 'chrome')
# Dark thick spine and fuller.
blade([(-0.022, 0.08), (-0.024, 0.20), (-0.030, 0.40), (-0.034, 0.52), (-0.030, 0.58), (-0.022, 0.56), (-0.018, 0.40), (-0.015, 0.08)], 0.011, 0, 'steel_dark')
# Rust blotches.
for (y, z, w, h, c) in [(-0.005, 0.18, 0.025, 0.05, 'rust'), (0.0, 0.36, 0.02, 0.06, 'rust_dark'), (-0.012, 0.50, 0.025, 0.04, 'rust'), (0.01, 0.10, 0.02, 0.025, 'rust_light')]:
    m.box((0.0094, w, h), (0, y, z), c)

# Handle: dark wood scales with rivets, tang between.
m.box((0.006, 0.034, 0.20), (0, 0, -0.06), 'steel_dark')
for sx in (-1, 1):
    m.box((0.014, 0.042, 0.20), (sx * 0.011, 0, -0.06), 'wood_dark', bevel=0.004, taper=(0.95, 0.9))
m.box((0.03, 0.048, 0.02), (0, 0, -0.17), 'rubber', bevel=0.004)  # butt cap
m.box((0.036, 0.05, 0.012), (0, 0, -0.18), 'rubber', bevel=0.004)
# Finger guard lip and ferrule.
m.box((0.032, 0.056, 0.014), (0, 0, 0.045), 'steel_dark', bevel=0.003)
m.box((0.032, 0.030, 0.02), (0, -0.025, 0.06), 'steel_dark')
for z in (-0.03, -0.11):
    m.cyl(0.0065, 0.044, (0, 0, z), 'chrome', rot=(0, 90, 0), seg=6)
# Wear: scratched wood highlights, hole for lanyard.
m.box((0.004, 0.02, 0.05), (0.019, 0.0, -0.075), 'wood', rot=(0, 0, 0))
m.cyl(0.008, 0.04, (0, -0.02, -0.15), 'black', rot=(0, 90, 0), seg=6)

finish(m, bounds={'x': (-0.04, 0.04), 'y': (-0.05, 0.06), 'z': (-0.19, 0.61)})
