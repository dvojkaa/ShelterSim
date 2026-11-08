from ss_art import Model, finish
m = Model('door_frame', seed=24)

OPEN_X = 0.78   # opening |x| < 0.78, z < 2.2 must stay empty
OPEN_Z = 2.2


def clip(pts, keep, cross):
    """Sutherland-Hodgman against one half-plane. keep(p)->bool, cross(a,b)->point."""
    out = []
    for i, p in enumerate(pts):
        q = pts[(i + 1) % len(pts)]
        if keep(p):
            out.append(p)
            if not keep(q):
                out.append(cross(p, q))
        elif keep(q):
            out.append(cross(p, q))
    return out


def clip_rect(pts, x0, x1, z0, z1):
    def on_x(v):
        return lambda a, b: (v, a[1] + (b[1] - a[1]) * (v - a[0]) / (b[0] - a[0]))
    def on_z(v):
        return lambda a, b: (a[0] + (b[0] - a[0]) * (v - a[1]) / (b[1] - a[1]), v)
    for keep, cross in ((lambda p: p[0] >= x0, on_x(x0)), (lambda p: p[0] <= x1, on_x(x1)),
                        (lambda p: p[1] >= z0, on_z(z0)), (lambda p: p[1] <= z1, on_z(z1))):
        if not pts:
            return pts
        pts = clip(pts, keep, cross)
    return pts


def hazard(x0, x1, z0, z1, s, slant=1, pitch=0.3, y=0.138):
    """Hazard band on a Y-facing surface: black backing, diagonal yellow stripes clipped to the rect."""
    m.box((x1 - x0, 0.004, z1 - z0), ((x0 + x1) / 2, s * y, (z0 + z1) / 2), 'black')
    lo, hi = z0 - 1, z1 + 1
    c = x0 - 3
    while c < x1 + 3:
        big = [(c - slant * lo, lo), (c + pitch / 2 - slant * lo, lo),
               (c + pitch / 2 - slant * hi, hi), (c - slant * hi, hi)]
        p = clip_rect(big, x0, x1, z0, z1)
        if len(p) >= 3:
            m.poly(p, 0.004, (0, s * (y + 0.003), 0), 'hazard_yellow')
        c += pitch


def bolt(x, z, s, c='gunmetal'):
    m.cyl(0.026, 0.012, (x, s * 0.144, z), c, rot=(90, 0, 0), seg=6)


# --- Structure. Everything inside the opening edge is 2 cm back from it except the steel liner.
LZ = OPEN_Z + 0.035    # lintel underside; the steel soffit fills the 3.5 cm below it
for sx in (-1, 1):
    # Reinforced concrete post, sits on the shoe, stops under the lintel: faces only touch, never overlap.
    m.box((0.2, 0.27, LZ - 0.14), (sx * 0.9, 0, (LZ + 0.14) / 2), 'concrete')
    # Steel jamb liner, 5 mm back from the opening edge; shoes and hinge blocks define the edge itself.
    m.box((0.03, 0.29, OPEN_Z - 0.14), (sx * 0.8, 0, (OPEN_Z + 0.14) / 2), 'steel_dark')
    # Base shoe
    m.box((0.22, 0.3, 0.14), (sx * 0.89, 0, 0.07), 'gunmetal')

# Lintel: concrete beam, steel soffit
m.box((2.0, 0.27, 2.94 - LZ), (0, 0, (LZ + 2.94) / 2), 'concrete')
m.box((1.6, 0.29, 0.03), (0, 0, OPEN_Z + 0.02), 'steel_dark')
m.box((2.0, 0.3, 0.06), (0, 0, 2.97), 'steel_dark')

for s in (-1, 1):
    # Hazard band across the lintel, stripes meet at the middle
    hazard(-0.96, 0.0, 2.32, 2.58, s, slant=1)
    hazard(0.0, 0.96, 2.32, 2.58, s, slant=-1)
    # Hazard on the lower posts
    hazard(-0.97, -0.81, 0.18, 1.1, s, slant=1, pitch=0.26)
    hazard(0.81, 0.97, 0.18, 1.1, s, slant=-1, pitch=0.26)
    # Bolts: lintel row and post rows
    for x in (-0.84, -0.42, 0.42, 0.84):
        bolt(x, 2.82, s)
    for sx in (-1, 1):
        for z in (1.3, 1.7, 2.1):
            bolt(sx * 0.9, z, s)
    # Concrete wear and rust weep (overlays only reach 1.0, never into the opening)
    m.box((0.14, 0.004, 0.3), (-0.9, s * 0.1365, 1.5), 'concrete_dark')
    m.box((0.1, 0.004, 0.2), (0.91, s * 0.1365, 1.9), 'concrete_light')
    m.box((0.05, 0.004, 0.18), (0.42, s * 0.1365, 2.7), 'rust_dark')
    m.box((0.05, 0.004, 0.14), (-0.84, s * 0.1365, 2.73), 'rust_dark')
    m.box((0.3, 0.004, 0.08), (-0.2, s * 0.1365, 2.9), 'concrete_light')

# Front only: warning lamp in a cage and conduit feeding it.
m.box((0.26, 0.012, 0.2), (0, -0.141, 2.8), 'gunmetal')
m.cyl(0.075, 0.03, (0, -0.135, 2.8), 'warning_red', rot=(90, 0, 0), seg=10)
m.cyl(0.04, 0.008, (0, -0.143, 2.8), 'red_bright', rot=(90, 0, 0), seg=8)
for dz in (-0.06, 0.0, 0.06):
    m.box((0.18, 0.008, 0.012), (0, -0.146, 2.8 + dz), 'gunmetal')
m.tube([(0.13, -0.137, 2.8), (0.32, -0.137, 2.8), (0.32, -0.137, 2.89), (0.92, -0.137, 2.89)], 0.012, 'steel', seg=5)

# Back only: door-open indicator box.
m.box((0.24, 0.016, 0.16), (0.55, 0.142, 2.78), 'army_green_dark')
m.cyl(0.03, 0.008, (0.5, 0.146, 2.78), 'lamp_warm', rot=(90, 0, 0), seg=6)
m.cyl(0.03, 0.008, (0.6, 0.146, 2.78), 'black', rot=(90, 0, 0), seg=6)

# Heavy hinge blocks on the -x post (door hinge side), wholly outside the opening.
for z in (0.42, 1.1, 1.78):
    m.box((0.1, 0.296, 0.18), (-0.83, 0, z), 'rust_dark')

# Opening clearance proof: fail the build if anything enters it. Checks verts, edge samples, face centres.
eps = 1e-4
def inside(v):
    return abs(v[0]) < OPEN_X - eps and 0 + eps < v[2] < OPEN_Z - eps
bad = 0
bm = m.default.bm
for v in bm.verts:
    bad += inside(v.co)
for e in bm.edges:
    a, b = e.verts[0].co, e.verts[1].co
    for k in range(1, 8):
        bad += inside(a.lerp(b, k / 8))
for f in bm.faces:
    bad += inside(f.calc_center_median())
print(f"ASSET_INFO door_frame opening check: {bad} samples inside |x|<{OPEN_X}, z<{OPEN_Z}")
assert bad == 0

finish(m, bounds={'x': (-1.0, 1.0), 'y': (-0.15, 0.15), 'z': (0, 3.0)})
