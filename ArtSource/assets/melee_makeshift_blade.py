"""First-person makeshift scrap blade. Grip middle at origin, length +Z, edge toward +Y, flat faces +-X."""
import math
from mathutils import Vector
from ss_art import Model, finish

m = Model('melee_makeshift_blade', seed=62)


def seg(a, b, ra, rb, color, n=8):
    a, b = Vector(a), Vector(b)
    d = b - a
    e = d.to_track_quat('Z', 'Y').to_euler()
    m.cyl(ra, d.length, (a + b) / 2, color, rot=tuple(math.degrees(x) for x in e), seg=n, r2=rb)


def blade(profile, thick, x, color):
    # profile in (y, z); poly rotated so extrusion runs along X.
    m.poly(profile, thick, (x, 0, 0), color, rot=(0, 0, 90))


# Blade: spine at y=-0.045, ragged chipped edge at y=+0.045, ground tip.
P = [(-0.045, 0.06), (-0.046, 0.30), (-0.043, 0.50), (-0.03, 0.62), (-0.005, 0.68), (0.02, 0.60),
     (0.032, 0.55), (0.026, 0.50), (0.040, 0.44), (0.036, 0.36), (0.045, 0.30), (0.038, 0.22), (0.045, 0.12), (0.04, 0.06)]
blade(P, 0.012, 0, 'steel')
# Ground bevel near the edge: thinner brighter strip.
E = [(0.034, 0.12), (0.040, 0.30), (0.030, 0.44), (0.020, 0.58), (0.0, 0.66), (0.02, 0.52), (0.034, 0.36), (0.030, 0.2)]
blade(E, 0.0145, 0, 'chrome')
# Rust patches and dark spine.
blade([(-0.045, 0.10), (-0.046, 0.40), (-0.036, 0.40), (-0.036, 0.10)], 0.0135, 0, 'steel_dark')
for (y, z, w, h, c) in [(-0.01, 0.22, 0.03, 0.06, 'rust'), (0.01, 0.46, 0.025, 0.05, 'rust_dark'), (-0.02, 0.56, 0.02, 0.04, 'rust'), (0.0, 0.12, 0.04, 0.03, 'rust_light')]:
    m.box((0.0135, w, h), (0, y, z), c, rot=(0, 0, 0))
# Saw teeth remnants along the spine.
for i in range(4):
    m.box((0.011, 0.012, 0.016), (0, -0.052, 0.17 + i * 0.07), 'steel_dark', rot=(0, 0, 35))

# Tang inside the handle + wrapped handle.
m.box((0.01, 0.04, 0.18), (0, 0, -0.03), 'steel_dark')
for i in range(12):
    z = -0.19 + i * 0.024
    c = 'fabric_stain' if i % 3 else 'cloth_dark'
    seg((-0.001, -0.001, z - 0.015), (0.001, 0.001, z + 0.015), 0.0225, 0.0235, c, 8)
m.box((0.05, 0.01, 0.08), (0.022, 0.0, -0.17), 'fabric_stain', rot=(0, 0, 40))
# Pommel lump of tape.
m.blob((0.04, 0.04, 0.03), (0, 0, -0.20), 'cloth_dark', rough=0.1, subdiv=0)
# Bent washer guard (tilted ring).
m.cyl(0.036, 0.008, (0, 0.002, 0.07), 'steel_dark', rot=(8, 6, 0), seg=10)
m.cyl(0.036, 0.006, (0, 0.002, 0.081), 'rust', rot=(-6, 10, 0), seg=10)
m.cyl(0.0225, 0.014, (0, 0.002, 0.075), 'rust_dark', rot=(8, 6, 0), seg=8)
# Hose clamp on handle top and rivet heads.
seg((0, 0, 0.035), (0, 0, 0.05), 0.0262, 0.0262, 'steel', 8)
for z in (-0.05, -0.12):
    m.cyl(0.006, 0.05, (0, 0, z), 'chrome', rot=(0, 90, 0), seg=5)

finish(m, bounds={'x': (-0.06, 0.06), 'y': (-0.07, 0.07), 'z': (-0.22, 0.69)})
