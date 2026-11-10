"""Industrial pendant ceiling light. Origin = ceiling mount point (z=0 = ceiling surface); hangs down to z=-0.45."""
import math
from ss_art import Model, finish
m = Model('piece_light_ceiling', seed=201)


def fr(r_bottom, r_top, z_bottom, z_top, c, seg=12, x=0.0, y=0.0):
    m.cyl(r_bottom, z_top - z_bottom, (x, y, (z_bottom + z_top) / 2), c, seg=seg, r2=r_top)


def ring(r, z, pr, c, n=8, seg=3):
    pts = [(r * math.cos(2 * math.pi * i / n), r * math.sin(2 * math.pi * i / n), z) for i in range(n + 1)]
    m.tube(pts, pr, c, seg=seg)


# Ceiling canopy plate with bolts
fr(0.09, 0.075, -0.02, 0.0, 'steel_dark', seg=8)
fr(0.095, 0.095, -0.016, -0.008, 'rust_dark', seg=8)
for a in (30, 150, 270):
    r = math.radians(a)
    fr(0.009, 0.009, -0.027, -0.02, 'chrome', seg=6, x=0.062 * math.cos(r), y=0.062 * math.sin(r))
# Stem (0.12 m) with a collar
fr(0.014, 0.014, -0.14, -0.02, 'gunmetal', seg=8)
fr(0.026, 0.022, -0.075, -0.06, 'rust_dark', seg=8)
# Conical shade holder / socket
fr(0.03, 0.022, -0.17, -0.14, 'steel_dark', seg=8)
# Cone metal shade (top of cage zone)
fr(0.16, 0.035, -0.23, -0.165, 'steel_dark', seg=8)
fr(0.165, 0.16, -0.24, -0.23, 'rust', seg=8)
# Bulb
fr(0.03, 0.035, -0.255, -0.2, 'lamp_warm', seg=8)
fr(0.085, 0.035, -0.31, -0.255, 'lamp_warm', seg=8)
fr(0.095, 0.085, -0.355, -0.31, 'lamp_warm', seg=8)
fr(0.06, 0.095, -0.395, -0.355, 'lamp_warm', seg=8)
# Cage: top ring, belly ring, 8 ribs into rusty bottom cap
ring(0.14, -0.245, 0.007, 'gunmetal')
ring(0.14, -0.33, 0.007, 'gunmetal')
for i in range(6):
    a = 2 * math.pi * i / 6
    c, s = math.cos(a), math.sin(a)
    m.tube([(0.14 * c, 0.14 * s, -0.245), (0.14 * c, 0.14 * s, -0.33),
            (0.1 * c, 0.1 * s, -0.395), (0.02 * c, 0.02 * s, -0.425)], 0.006, 'gunmetal', seg=3)
fr(0.045, 0.05, -0.44, -0.415, 'rust', seg=8)
fr(0.02, 0.02, -0.45, -0.44, 'rust_dark', seg=8)
# Trailing cable coil off the canopy
m.tube([(0.07, 0.0, -0.012), (0.11, 0.03, -0.035), (0.13, 0.0, -0.06), (0.11, -0.04, -0.08),
        (0.15, -0.06, -0.095), (0.17, -0.02, -0.11), (0.18, 0.04, -0.125)], 0.008, 'black', seg=3)
finish(m, bounds={'x': (-0.2, 0.2), 'y': (-0.2, 0.2), 'z': (-0.45, 0)})
