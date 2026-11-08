"""Wall-mounted big red mushroom button. Origin = button centre, wall at y=+0.3, button faces -Y."""
import math
from ss_art import Model, finish
m = Model('apocalypse_button', seed=66)


def disc(r1, r2, y0, y1, c, x=0.0, z=0.0, seg=10):
    """Frustum along -Y: radius r1 at y0 (wall side), r2 at y1 (viewer side). y0 > y1."""
    m.cyl(r1, y0 - y1, (x, (y0 + y1) / 2, z), c, rot=(90, 0, 0), seg=seg, r2=r2)


def clip(poly, x0, x1, z0, z1):
    """Sutherland-Hodgman clip of a convex XZ polygon to a rectangle."""
    edges = [(0, x0, 1), (0, x1, -1), (1, z0, 1), (1, z1, -1)]
    for axis, lim, sign in edges:
        out = []
        for i, a in enumerate(poly):
            b = poly[(i + 1) % len(poly)]
            ina, inb = (a[axis] - lim) * sign >= 0, (b[axis] - lim) * sign >= 0
            if ina:
                out.append(a)
            if ina != inb:
                t = (lim - a[axis]) / (b[axis] - a[axis])
                out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
        poly = out
        if not poly:
            return []
    return poly


def stripes(x0, x1, z0, z1, y, width=0.07, gap=0.07):
    """Black diagonal stripes clipped to a rectangle, as thin slabs on a yellow bar. y = slab centre."""
    h = z1 - z0
    x = -1.0
    while x < 1.0:
        p = clip([(x, z0), (x + width, z0), (x + width + h, z1), (x + h, z1)], x0, x1, z0, z1)
        if len(p) >= 3:
            m.poly(p, 0.006, (0, y, 0), 'black')
        x += width + gap


# --- Housing: heavy steel box bolted to the wall ---
m.box((0.64, 0.20, 0.64), (0, 0.20, 0), 'gunmetal', bevel=0.012)
# Recessed face plate
m.box((0.56, 0.012, 0.56), (0, 0.098, 0), 'steel_dark')

# --- Hazard frame: four proud yellow bars with diagonal black stripes ---
F = 0.35; B = 0.065; FY = 0.075   # outer half-size, bar width, bar front face
BD = 0.05                          # bar depth
yc = FY + BD / 2
m.box((2 * F, BD, B), (0, yc, F - B / 2), 'hazard_yellow')
m.box((2 * F, BD, B), (0, yc, -F + B / 2), 'hazard_yellow')
m.box((B, BD, 2 * F - 2 * B), (-F + B / 2, yc, 0), 'hazard_yellow')
m.box((B, BD, 2 * F - 2 * B), (F - B / 2, yc, 0), 'hazard_yellow')
sy = FY - 0.001
stripes(-F, F, F - B, F, sy)
stripes(-F, F, -F, -F + B, sy)
# Side bars: reuse the clipper with axes swapped so stripes keep running around the frame.
for sx in (-1, 1):
    x0, x1 = (sx * F - B, sx * F) if sx > 0 else (sx * F, sx * F + B)
    z = -F + B + 0.02
    while z < F - B:
        p = clip([(x0, z), (x1, z + B), (x1, z + B + 0.07), (x0, z + 0.07)], x0, x1, -F + B, F - B)
        if len(p) >= 3:
            m.poly(p, 0.006, (0, sy, 0), 'black')
        z += 0.14
# Battered corner plates with bolts
for sx in (-1, 1):
    for sz in (-1, 1):
        m.box((0.075, 0.012, 0.075), (sx * 0.3125, FY - 0.006, sz * 0.3125), 'steel_dark')
        disc(0.014, 0.010, FY - 0.012, FY - 0.024, 'chrome', sx * 0.3125, sz * 0.3125, seg=5)

# --- Hazard ring around the button: black disc with yellow sectors ---
RY = 0.092
disc(0.205, 0.205, 0.104, RY, 'black', seg=16)
n = 12
for i in range(0, n, 2):
    a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
    pts = []
    for k in range(3):
        a = a0 + (a1 - a0) * k / 2
        pts.append((0.203 * math.cos(a), 0.203 * math.sin(a)))
    for k in range(2, -1, -1):
        a = a0 + (a1 - a0) * k / 2
        pts.append((0.165 * math.cos(a), 0.165 * math.sin(a)))
    m.poly(pts, 0.006, (0, RY - 0.002, 0), 'hazard_yellow')

# --- The button itself ---
disc(0.170, 0.150, 0.100, 0.050, 'steel', seg=12)         # bezel
disc(0.095, 0.095, 0.050, -0.005, 'chrome', seg=8)        # plunger stem
disc(0.112, 0.158, 0.012, -0.012, 'warning_red', seg=14)  # mushroom underside flare
disc(0.158, 0.158, -0.012, -0.040, 'warning_red', seg=16) # rim
disc(0.158, 0.135, -0.040, -0.074, 'red_bright', seg=16)  # dome
disc(0.135, 0.085, -0.074, -0.094, 'red_bright', seg=16)
disc(0.085, 0.000, -0.094, -0.100, 'red_bright', seg=16)

# --- Bent steel guard hoops either side: "don't hit this by accident" ---
for sx in (-1, 1):
    x = sx * 0.232
    pts = [(x, 0.095, -0.14), (x, 0.0, -0.11), (x, -0.045, -0.04), (x, -0.045, 0.04), (x, 0.0, 0.11), (x, 0.095, 0.14)]
    m.tube(pts, 0.012, 'steel', seg=4)
    for z in (-0.14, 0.14):
        disc(0.024, 0.024, 0.098, 0.086, 'steel_dark', x, z, seg=5)

# --- Warning plate below the button ---
PZ = -0.247
m.box((0.44, 0.012, 0.074), (0, 0.086, PZ), 'paint_white')
for x in (-0.205, 0.205):
    disc(0.007, 0.006, 0.081, 0.074, 'steel_dark', x, PZ, seg=4)
# Red warning triangles with a black bang
for x in (-0.15, 0.15):
    m.poly([(x - 0.038, PZ - 0.028), (x + 0.038, PZ - 0.028), (x, PZ + 0.031)], 0.004, (0, 0.079, 0), 'warning_red')
    m.box((0.009, 0.004, 0.024), (x, 0.0765, PZ - 0.002), 'black')
    m.box((0.009, 0.004, 0.008), (x, 0.0765, PZ - 0.021), 'black')
# Blocky bars suggesting stencilled warning lines (no real text)
m.box((0.17, 0.004, 0.016), (0, 0.079, PZ + 0.014), 'black')
m.box((0.13, 0.004, 0.012), (0, 0.079, PZ - 0.014), 'warning_red')

# --- Indicators and key switch above the button ---
IZ = 0.247
for x, c in ((-0.14, 'red_bright'), (0.14, 'lamp_warm')):
    disc(0.036, 0.030, 0.094, 0.078, 'chrome', x, IZ, seg=8)
    disc(0.024, 0.016, 0.082, 0.064, c, x, IZ, seg=8)
disc(0.038, 0.034, 0.094, 0.074, 'steel', 0, IZ, seg=8)    # key switch
disc(0.022, 0.022, 0.080, 0.068, 'gunmetal', 0, IZ, seg=8)
m.box((0.008, 0.016, 0.036), (0, 0.062, IZ), 'chrome', rot=(0, 35, 0))

# --- Wear: scratches, rust weep under bolts, cable gland into the wall ---
m.box((0.06, 0.003, 0.10), (-0.245, 0.0912, -0.13), 'rust_dark')
m.box((0.05, 0.003, 0.14), (0.25, 0.0912, 0.09), 'rust')
m.box((0.13, 0.003, 0.006), (0.215, 0.0912, -0.19), 'steel', rot=(0, 28, 0))
m.tube([(0.2, 0.2, 0.31), (0.2, 0.2, 0.335), (0.2, 0.3, 0.335)], 0.014, 'black', seg=4)
m.tube([(-0.2, 0.2, 0.31), (-0.2, 0.2, 0.335), (-0.2, 0.3, 0.335)], 0.014, 'rust', seg=4)

finish(m, bounds={'x': (-0.35, 0.35), 'y': (-0.1, 0.3), 'z': (-0.35, 0.35)})
