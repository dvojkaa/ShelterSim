"""Shared wrecked-car builder for car_sedan_* and car_pickup_*.

Invented, generic 1950s-ish rounded car. Nose at +Y, rear at -Y, envelope x +-1, y +-2.25, z 0..2.25.
The body is a loft along Y (ring of points per station) rather than stacked boxes, so it gets a rounded
silhouette, real wheel arches, and rust/seams painted per face: no decal planes, so nothing can z-fight.
"""
import math
import bmesh
import numpy as np
from mathutils import Matrix, Vector
from ss_art import Model, finish

BOUNDS = {'x': (-1.0, 1.0), 'y': (-2.25, 2.25), 'z': (0.0, 2.25)}
WHEEL_R, WHEEL_Y, ARCH_R = 0.35, 1.4, 0.47
WELL_X = 0.70       # inner wall of the wheel wells
CROWN = 0.10        # cabin roof/windshield crown slope, keeps the split-screen halves planar
ROOF_Z = 2.10       # cabin side plane top edge


def tab(table, y):
    return float(np.interp(y, [p[0] for p in table], [p[1] for p in table]))


def mesh(part, verts, faces):
    """Raw faces: faces = [(vertex indices, colour), ...]. Winding is the caller's job."""
    part = getattr(part, 'default', part)   # accept a Model or a Part
    t = bmesh.new()
    t.loops.layers.uv.new('UVMap')
    vs = [t.verts.new(v) for v in verts]
    for idx, color in faces:
        part._paint(t, [t.faces.new([vs[i] for i in idx])], color)
    part._commit(t, Matrix.Identity(4))


def loft(part, rings, color_fn):
    """rings: [(y, [(x, z), ...]), ...] with ascending y and rings CCW in (x, z). Capped at both ends.
    color_fn(segment index, y mid, z mid) -> palette name, a (colour, colour) pair to split the quad into two
    differently painted triangles, or None to leave a hole."""
    n = len(rings[0][1])
    verts = [(x, y, z) for y, ring in rings for x, z in ring]
    faces = []
    for i in range(len(rings) - 1):
        for j in range(n):
            k = (j + 1) % n
            a, b, c, d = i * n + j, (i + 1) * n + j, (i + 1) * n + k, i * n + k
            ymid = (rings[i][0] + rings[i + 1][0]) / 2
            zmid = (rings[i][1][j][1] + rings[i][1][k][1] + rings[i + 1][1][j][1] + rings[i + 1][1][k][1]) / 4
            col = color_fn(j, ymid, zmid)
            if isinstance(col, tuple) and rings[i + 1][0] - rings[i][0] < 0.14:
                col = col[1]                # too narrow to tear: slivers would read as a sawtooth
            if isinstance(col, tuple):      # two triangles, two colours: a torn edge rather than a rectangle
                tri = ((a, b, c), (a, c, d)) if (i + j) % 2 else ((b, c, d), (b, d, a))
                faces += [(tri[0], col[0]), (tri[1], col[1])]
            elif col:
                faces.append(((a, b, c, d), col))
    last = (len(rings) - 1) * n
    faces.append((list(range(n)), color_fn(-1, rings[0][0], 0) or 'black'))
    faces.append(([last + j for j in reversed(range(n))], color_fn(-2, rings[-1][0], 0) or 'black'))
    mesh(part, verts, faces)


def patch_color(patches, paint, y, z, side, dy=0.12, dz=0.12):
    """patches: (y0, y1, z0, z1, side, colour). Solid inside, half-painted faces round the edge so that rust
    tears off in triangles instead of showing the rectangle it was declared as."""
    for y0, y1, z0, z1, ps, c in patches:
        if ps == side and y0 < y < y1 and z0 < z < z1:
            return c
    for y0, y1, z0, z1, ps, c in patches:
        if ps == side and y0 - dy < y < y1 + dy and z0 - dz < z < z1 + dz:
            return (c, paint)
    return paint


def facet(part, pts, color, outward, lift=0.012):
    """One single-sided polygon lifted off a surface, oriented to face `outward`."""
    pts = [Vector(p) for p in pts]
    nrm = Vector((0, 0, 0))
    for a, b in zip(pts, pts[1:] + pts[:1]):
        nrm += a.cross(b)
    if nrm.dot(Vector(outward)) < 0:
        pts.reverse()
        nrm = -nrm
    nrm.normalize()
    mesh(part, [tuple(p + nrm * lift) for p in pts], [(list(range(len(pts))), color)])


# ---------------------------------------------------------------------------------------- shapes
SHAPES = {
    'sedan': {
        'top': [(-2.2, 1.04), (-2.08, 1.17), (-1.6, 1.30), (-1.0, 1.37), (1.0, 1.37), (1.6, 1.25), (2.0, 1.13), (2.2, 0.98)],
        'cowl': 1.02, 'roof_f': 0.42, 'roof_r': -0.52, 'deck': -1.18,
        'seams': [0.78, 0.0, -0.78],
        'bed': None,
        'extra_y': [-2.2, -2.08, -1.95, -1.6, -1.25, -0.5, 0.5, 1.0, 1.95, 2.08, 2.2],
    },
    'pickup': {
        'top': [(-2.2, 1.18), (-2.11, 1.27), (-2.0, 1.29), (-0.3, 1.31), (-0.2, 1.37), (1.0, 1.37), (1.6, 1.25), (2.0, 1.13), (2.2, 0.98)],
        'cowl': 0.98, 'roof_f': 0.34, 'roof_r': -0.03, 'deck': -0.21,
        'seams': [0.82, -0.14],
        'bed': (-2.06, -0.28, 0.86),
        'extra_y': [-2.2, -2.11, -2.08, -2.06, -0.75, -0.3, -0.28, -0.25, 0.3, 1.0, 1.95, 2.2],
    },
}
BOTTOM = [(-2.2, 0.46), (-2.05, 0.36), (-1.8, 0.30), (1.8, 0.30), (2.05, 0.36), (2.2, 0.46)]
WIDTH = [(-2.2, 0.72), (-2.1, 0.83), (-1.85, 0.92), (-1.4, 0.955), (-0.85, 0.895), (0.85, 0.895), (1.4, 0.955), (1.85, 0.93), (2.1, 0.83), (2.2, 0.72)]


def arch_z(y):
    for yc in (-WHEEL_Y, WHEEL_Y):
        d = abs(y - yc)
        if d < ARCH_R:
            return WHEEL_R + (ARCH_R * ARCH_R - d * d) ** 0.5
    return 0.0


def body_ring(shape, y, dents):
    """16-point ring, CCW, +X half going up then -X half coming back down. See segment() for the bands."""
    zt, zb, w = tab(shape['top'], y), tab(BOTTOM, y), tab(WIDTH, y)
    za = max(zb + 0.06, arch_z(y))
    zs = 0.72 + (zt - 0.72) * 0.68
    inner = min(WELL_X, w * 0.97 - 0.03)
    bed = shape['bed']
    if bed and bed[0] < y < bed[1]:
        i1, z1, i2, z2 = 0.74, zt, 0.74, bed[2]
    else:
        i1, z1, i2, z2 = w * 0.62, zt + 0.025, w * 0.32, zt + 0.04

    def half(side):
        pts = [(inner, zb), (inner, za), (w * 0.985, za), (w, 0.72), (w * 0.985, zs), (w - 0.07, zt), (i1, z1), (i2, z2)]
        out = []
        for x, z in pts:
            if x > 0.75:
                for ds, yc, ry, zc2, rz, depth in dents:
                    if ds == side:
                        x -= depth * max(0.0, 1 - ((y - yc) / ry) ** 2) * max(0.0, 1 - ((z - zc2) / rz) ** 2)
            out.append((x * side, z))
        return out
    right, left = half(1), half(-1)
    return right + left[::-1]


def under_col(band, y):
    """Floor and wheel wells. Outside the arches they are out of sight behind the chassis, so they are left out."""
    if band == 0:
        return None if abs(y) < 1.8 else 'black'
    return 'rust_dark' if abs(abs(y) - WHEEL_Y) < ARCH_R else None


def segment(j):
    """Loft segment index -> (band, side). Bands: 0 floor, 1 well wall, 2 well roof, 3 lower side, 4 mid side,
    5 shoulder, 6 inner shoulder / bed rail, 7 hood crown / bed wall, 8 hood centre / bed floor."""
    if j == 15:
        return (0, 1)
    return (j + 1, 1) if j < 8 else (15 - j, -1)


def station_list(shape, extra):
    ys = list(shape['extra_y']) + list(extra)
    for yc in (-WHEEL_Y, WHEEL_Y):
        for f in (-1.0, -0.75, -0.3, 0.3, 0.75, 1.0):
            ys.append(yc + f * ARCH_R)
    for s in shape['seams']:
        ys += [s - 0.012, s + 0.012]
    ys = sorted(ys)
    out = [ys[0]]
    for y in ys[1:]:
        if y - out[-1] > 0.004:
            out.append(y)
    return out


# ---------------------------------------------------------------------------------------- cabin
def side_x(z):
    """Cabin side plane: leaning inward so the greenhouse tumbles home."""
    return 0.84 - (z - 1.32) * (0.22 / 0.78)


def cabin(m, paint, shape, zbelt, panes):
    cy, ry, rr, dy = shape['cowl'], shape['roof_f'], shape['roof_r'], shape['deck']
    z_low, z_lip = zbelt - 0.08, zbelt + 0.03

    def ring(z_edge):
        w = side_x(z_edge)
        return [(side_x(z_low), z_low), (w, z_edge), (0.0, z_edge + CROWN * w), (-w, z_edge), (-side_x(z_low), z_low)]
    # Dark cabin shell sits under the panels. Leave the front screen segments open: the shared
    # atlas is opaque, so a full 'glass' facet reads as paint and hides the cockpit.
    def core_color(j, y, z):
        if j in (1, 2) and ry < y < cy:
            return None
        return 'black'
    loft(m, [(dy, ring(z_lip)), (rr, ring(ROOF_Z)), (ry, ring(ROOF_Z)), (cy, ring(z_lip))], core_color)

    # Pillars stand proud of the glass, so the windows read as recessed.
    for s in (-1, 1):
        for y0, y1, r in [(cy, ry, 0.05), (dy, rr, 0.075 if shape['bed'] is None else 0.06)]:
            m.tube([(s * side_x(zbelt - 0.02), y0, zbelt - 0.02), (s * side_x(ROOF_Z + 0.03), y1, ROOF_Z + 0.03)], r, paint, seg=6)
        if shape['bed'] is None:
            m.tube([(s * side_x(zbelt - 0.02), -0.02, zbelt - 0.02), (s * side_x(ROOF_Z), -0.02, ROOF_Z)], 0.05, paint, seg=6)
    # Split-screen bar in the windshield and the matching one at the back.
    m.tube([(0, cy - 0.01, zbelt + 0.09), (0, ry + 0.01, ROOF_Z + 0.07)], 0.028, paint, seg=4)

    # Roof shell: rounded, with a small visor overhang at the front.
    def shell(y0, grow, dz):
        w = side_x(ROOF_Z) + grow
        return (y0, [(w - 0.03, ROOF_Z - 0.03 + dz), (w + 0.03, ROOF_Z + 0.02 + dz), (w - 0.08, ROOF_Z + 0.075 + dz),
                     (0.3 * w, ROOF_Z + 0.115 + dz), (-0.3 * w, ROOF_Z + 0.115 + dz), (-w + 0.08, ROOF_Z + 0.075 + dz),
                     (-w - 0.03, ROOF_Z + 0.02 + dz), (-w + 0.03, ROOF_Z - 0.03 + dz)])
    rust_rows = panes.get('roof_rust', [])

    def roof_col(j, y, z):
        for y0, y1, jj, c in rust_rows:
            if y0 < y < y1 and j in jj:
                return (c, paint)
        return 'rust_dark' if j == 0 else paint
    ry_mid = (rr + ry) / 2
    loft(m, [shell(rr - 0.10, -0.10, -0.05), shell(rr - 0.03, -0.01, -0.01), shell(ry_mid - 0.05, 0.0, 0.0), shell(ry_mid + 0.1, 0.0, 0.0),
             shell(ry + 0.03, 0.0, -0.005), shell(ry + 0.14, -0.08, -0.05)], roof_col)

    # Glass: flat facets on the core, jagged where smashed.
    def screen(s0, s1, side, uvs, color):
        """(t, u): t 0 at s0 -> 1 at s1 (each (y, z_edge)), u 0 centre -> 1 side edge."""
        pts = []
        for t, u in uvs:
            y = s0[0] + (s1[0] - s0[0]) * t
            ze = s0[1] + (s1[1] - s0[1]) * t
            w = side_x(ze)
            pts.append((side * u * w, y, ze + CROWN * w * (1 - u)))
        out = (0, 1.0 if s0[0] > s1[0] else -1.0, 0.8)
        facet(m, pts, color, out)

    for side, uvs, color in panes.get('wind', []):
        screen((cy, z_lip), (ry, ROOF_Z), side, uvs, color)
    for side, uvs, color in panes.get('rear', []):
        screen((dy, z_lip), (rr, ROOF_Z), side, uvs, color)
    for side, yzs, color in panes.get('side', []):
        pts = [(side * side_x(z), y, z) for y, z in yzs]
        facet(m, pts, color, (side, 0, 0.25))

    # Simple, dark readable interior now visible through the clear/open screen.
    dash_y = cy - 0.20
    m.box((1.42, 0.34, 0.13), (0, dash_y, z_lip - 0.17), 'gunmetal', bevel=0.025)
    m.box((0.56, 0.46, 0.16), (-0.38, cy - 0.03, z_lip - 0.13), 'black', bevel=0.025)  # instrument binnacle
    for x in (-0.39, 0.39):
        m.box((0.52, 0.44, 0.15), (x, -0.05, 0.73), 'leather_dark', bevel=0.035)
        m.box((0.50, 0.15, 0.46), (x, -0.28, 0.96), 'leather', bevel=0.035)
    # Steering wheel ring, oriented toward +Y.
    wheel = [(-0.65 + 0.16 * math.cos(i * math.tau / 8), cy - 0.03,
              z_lip - 0.13 + 0.16 * math.sin(i * math.tau / 8)) for i in range(9)]
    m.tube(wheel, 0.018, 'steel_dark', seg=5)
    m.tube([(-0.65, cy - 0.03, z_lip - 0.13), (-0.65, cy - 0.20, z_lip - 0.23)], 0.025, 'gunmetal', seg=6)


FULL = [(0.06, 0.06), (0.93, 0.06), (0.93, 0.93), (0.06, 0.93)]


# ---------------------------------------------------------------------------------------- running gear
def wheel(m, x, y, flat=False, rim='steel'):
    side = 1 if x > 0 else -1
    tyre_x = side * 0.855       # tyre 0.735..0.975: spec's 0.95 centre would break the 1.0 m envelope
    if flat:
        # Collapsed tyre: flattened contact patch and bulging sidewalls, rim dropped close to the ground.
        prof = [(-0.33, 0.0), (0.33, 0.0), (0.42, 0.07), (0.41, 0.19), (0.32, 0.33), (0.17, 0.45), (0.0, 0.5), (-0.17, 0.45),
                (-0.32, 0.33), (-0.41, 0.19), (-0.42, 0.07)]
        m.poly(prof, 0.24, (tyre_x, y, 0), 'rubber', rot=(0, 0, 90))
        cz = 0.25
        r_hub = 0.15
    else:
        m.cyl(WHEEL_R, 0.24, (tyre_x, y, WHEEL_R), 'rubber', rot=(0, 90, 0), seg=12)
        cz = WHEEL_R
        r_hub = 0.15
    if rim == 'none':
        m.cyl(0.17, 0.03, (side * 0.975, y, cz), 'rust_dark', rot=(0, 90, 0), seg=8)
    else:
        m.cyl(r_hub + 0.07, 0.03, (side * 0.97, y, cz), 'fabric_stain', rot=(0, 90, 0), seg=10)
        m.cyl(r_hub, 0.03, (side * 0.98, y, cz), rim, rot=(0, 90, 0), seg=8)


def running_gear(m, wheels):
    # Chassis and axles: only ever seen as darkness under the car, but it stops you seeing daylight through it.
    m.box((1.34, 3.6, 0.2), (0, 0, 0.26), 'black')
    for y in (-WHEEL_Y, WHEEL_Y):
        m.cyl(0.045, 1.5, (0, y, 0.3), 'rust_dark', rot=(0, 90, 0), seg=6)
    for x, y, flat, rim in wheels:
        wheel(m, x, y, flat, rim)
    # Exhaust, rusted through and sagging out behind the rear axle.
    m.tube([(0.42, -0.8, 0.22), (0.42, -1.75, 0.2), (0.46, -2.12, 0.14)], 0.035, 'rust_dark', seg=6)


def nose(m, paint, left_lamp='glass_dirty', right_lamp='smashed'):
    # Fender pods with round lamps, the most retro thing about it.
    for s, lamp in ((-1, left_lamp), (1, right_lamp)):
        m.cyl(0.19, 0.62, (s * 0.60, 1.86, 0.95), paint, rot=(-90, 0, 0), seg=8, r2=0.165)
        m.cyl(0.155, 0.03, (s * 0.60, 2.175, 0.95), 'chrome', rot=(-90, 0, 0), seg=10)
        if lamp == 'smashed':
            m.cyl(0.115, 0.03, (s * 0.60, 2.19, 0.95), 'black', rot=(-90, 0, 0), seg=10)
            m.poly([(-0.1, -0.07), (0.1, -0.08), (0.07, 0.0), (0.03, -0.04), (-0.04, 0.03), (-0.08, -0.02)], 0.012, (s * 0.60, 2.207, 0.95), 'glass_dirty')
        else:
            m.cyl(0.12, 0.035, (s * 0.60, 2.192, 0.95), lamp, rot=(-90, 0, 0), seg=10)
    # Grille mouth + chrome bars. Generic horizontal bars: no badge, no recognisable shape.
    m.box((0.78, 0.07, 0.3), (0, 2.195, 0.71), 'gunmetal')
    for i, z in enumerate((0.61, 0.71, 0.81)):
        if i == 1:      # middle bar snapped short and bent off line
            m.tube([(-0.38, 2.215, z), (0.02, 2.215, z)], 0.017, 'chrome', seg=4)
            m.tube([(0.1, 2.215, z - 0.03), (0.38, 2.215, z)], 0.017, 'rust', seg=4)
        else:
            m.tube([(-0.38, 2.215, z), (0.38, 2.215, z)], 0.017, 'chrome', seg=4)
    # Dented front bumper with brackets.
    for x in (-0.5, 0.45):
        m.box((0.09, 0.2, 0.08), (x, 2.08, 0.42), 'steel_dark')
    m.tube([(-0.92, 1.92, 0.44), (-0.82, 2.08, 0.43), (-0.3, 2.17, 0.42), (0.18, 2.17, 0.42), (0.42, 2.09, 0.47), (0.72, 2.15, 0.39), (0.9, 1.97, 0.36)], 0.075, 'chrome', seg=6)


def side_trim(m, shape, side, spans):
    """Chrome spear along the flank, following the fender swell, with missing sections."""
    for y0, y1 in spans:
        ys = [y0, (y0 + y1) / 2, y1] if y1 - y0 > 1.0 else [y0, y1]
        m.tube([(side * (tab(WIDTH, y) - 0.002), y, 0.93) for y in ys], 0.02, 'chrome', seg=4)


def door_handles(m, ys):
    for s, y in ys:
        m.box((0.05, 0.13, 0.035), (s * 0.905, y, 1.1), 'chrome')


# ---------------------------------------------------------------------------------------- sedan
def build_sedan(m, paint):
    shape = SHAPES['sedan']
    zbelt = tab(shape['top'], 0.0)
    dents = [(1, 1.9, 0.45, 0.8, 0.5, 0.11), (-1, -0.3, 0.35, 0.82, 0.3, 0.05)]
    door_y0, door_y1 = -0.78, 0.0        # +X rear door has been torn off
    patches = [
        (1.3, 1.95, 1.0, 1.5, -1, 'rust_dark'), (-2.2, -1.6, 0.36, 0.85, 1, 'rust_dark'), (0.0, 0.5, 0.4, 0.72, -1, 'rust'),
        (-1.25, -0.78, 0.36, 0.75, -1, 'rust_dark'), (-2.2, -1.95, 0.72, 1.25, -1, 'rust'), (0.5, 0.78, 0.8, 1.2, 1, 'rust'),
        (1.6, 2.1, 0.8, 1.15, 1, 'rust_dark'),
    ]

    def col(j, y, z):
        if j < 0:
            return paint
        band, side = segment(j)
        if band in (0, 1, 2):
            return under_col(band, y)
        for s in shape['seams']:
            if abs(y - s) < 0.013 and band in (3, 4, 5):
                return 'black'
        if side == 1 and door_y0 < y < door_y1 and band in (3, 4, 5):
            return None
        if band in (6, 7, 8) and -1.95 < y < -1.25:     # trunk lid: one big rust bloom on the crown
            return 'rust_dark' if side == -1 and band == 7 and y > -1.6 else paint
        return patch_color(patches, paint, y, z, side)

    loft(m, [(y, body_ring(shape, y, dents)) for y in station_list(shape, [door_y0, door_y1])], col)
    panes = {
        'roof_rust': [(-0.42, 0.0, (2,), 'rust'), (0.1, 0.42, (5,), 'rust_dark')],
        # The screen is clear/open across most of its area; only a few edge shards remain.
        'wind': [(-1, [(0.05, 0.08), (0.29, 0.07), (0.17, 0.24), (0.06, 0.30)], 'glass'),
                 (1, [(0.82, 0.93), (0.94, 0.92), (0.91, 0.66), (0.85, 0.72)], 'glass_dirty')],
        'rear': [(-1, [(0.06, 0.06), (0.93, 0.06), (0.93, 0.5), (0.55, 0.3), (0.4, 0.6), (0.2, 0.42), (0.06, 0.7)], 'glass_dirty'),
                 (1, FULL, 'glass')],
        'side': [
            (-1, [(0.05, 1.42), (0.86, 1.42), (0.42, 2.05), (0.05, 2.05)], 'glass'),
            (-1, [(-0.09, 1.42), (-0.9, 1.42), (-0.5, 2.05), (-0.09, 2.05)], 'glass_dirty'),
            (1, [(0.05, 1.42), (0.86, 1.42), (0.62, 1.68), (0.4, 1.55), (0.28, 1.84), (0.05, 1.7)], 'glass_dirty'),
            (1, [(-0.09, 1.42), (-0.9, 1.42), (-0.62, 1.7), (-0.4, 1.52), (-0.25, 1.62), (-0.09, 1.5)], 'glass_dirty'),
        ],
    }
    cabin(m, paint, shape, zbelt, panes)
    # What's behind the missing door: dark cabin, ripped bench seat, sill and jambs that close the opening.
    m.box((0.1, 0.78, 1.04), (0.62, -0.39, 0.86), 'gunmetal')
    m.box((0.32, 0.5, 0.2), (0.74, -0.42, 0.72), 'leather')
    m.box((0.12, 0.5, 0.42), (0.64, -0.42, 1.0), 'leather_dark', rot=(0, -8, 0))
    m.box((0.3, 0.8, 0.09), (0.76, -0.39, 0.42), 'rust_dark')
    m.box((0.28, 0.8, 0.06), (0.74, -0.39, 1.30), paint)
    for y in (door_y0, door_y1):
        m.box((0.3, 0.03, 1.0), (0.76, y, 0.86), 'gunmetal')

    # Tail fins, modest: a tail lamp at the end of each.
    for s in (-1, 1):
        m.poly([(-2.17, 1.0), (-1.3, 1.28), (-1.38, 1.35), (-1.95, 1.39), (-2.17, 1.37)], 0.09, (s * 0.80, 0, 0), paint, rot=(0, 0, 90))
        m.cyl(0.05, 0.1, (s * 0.80, -2.16, 1.27), 'warning_red' if s < 0 else 'black', rot=(-90, 0, 0), seg=8)
    # Rear bumper, hanging off its right-hand bracket.
    m.box((0.09, 0.2, 0.08), (-0.45, -2.1, 0.46), 'steel_dark')
    m.tube([(-0.9, -1.98, 0.46), (-0.78, -2.13, 0.46), (0.2, -2.175, 0.42), (0.68, -2.16, 0.26), (0.84, -2.02, 0.15)], 0.07, 'chrome', seg=6)
    # Scabs of rust on the tail panel, standing proud of the cap.
    m.box((0.46, 0.02, 0.16), (0.32, -2.205, 0.78), 'rust')
    m.box((0.24, 0.02, 0.2), (-0.5, -2.205, 0.68), 'rust_dark')
    # Trunk seam and lid handle.
    m.tube([(-0.55, -2.14, 1.13), (0.55, -2.14, 1.13)], 0.012, 'black', seg=5)
    m.box((0.16, 0.03, 0.06), (0, -2.19, 0.98), 'chrome')

    nose(m, paint)
    side_trim(m, shape, -1, [(-1.9, -0.95), (-0.6, 0.8), (0.95, 1.85)])
    side_trim(m, shape, 1, [(-1.9, -1.1), (0.05, 0.5), (1.1, 1.75)])
    door_handles(m, [(-1, 0.1), (-1, -0.7), (1, 0.1)])
    running_gear(m, [(-0.875, 1.4, False, 'steel'), (0.875, 1.4, True, 'steel'),
                     (-0.875, -1.4, True, 'none'), (0.875, -1.4, False, 'steel')])


# ---------------------------------------------------------------------------------------- pickup
def build_pickup(m, paint):
    shape = SHAPES['pickup']
    zbelt = tab(shape['top'], 0.3)
    dents = [(-1, 1.85, 0.5, 0.8, 0.5, 0.1), (1, -1.0, 0.7, 1.1, 0.35, 0.07), (-1, -2.0, 0.3, 1.0, 0.4, 0.06)]
    bed0, bed1, bedz = shape['bed']
    patches = [
        (1.0, 1.95, 0.72, 1.1, 1, 'rust_dark'), (-0.75, -0.3, 0.72, 1.0, -1, 'rust'), (-1.75, -1.3, 0.72, 1.0, 1, 'rust_dark'),
        (0.3, 0.82, 0.4, 0.72, 1, 'rust'), (1.6, 2.1, 0.4, 0.75, -1, 'rust_dark'),
    ]

    def col(j, y, z):
        if j < 0:
            return paint
        band, side = segment(j)
        if band in (0, 1, 2):
            return under_col(band, y)
        for s in shape['seams']:
            if abs(y - s) < 0.013 and band in (3, 4, 5):
                return 'black'
        if band in (6, 7, 8):
            if bed0 < y < bed1:
                if band == 6:       # rail top, scuffed to bare metal in places
                    return 'rust_dark' if -1.6 < y < -0.9 else paint
                if band == 7:
                    return 'rust_dark' if y < -1.3 else paint
                return 'rust_dark'
            if 1.0 < y < 2.0 and band != 6:
                return 'gunmetal'   # open engine bay under the raised hood
        return patch_color(patches, paint, y, z, side)

    loft(m, [(y, body_ring(shape, y, dents)) for y in station_list(shape, [])], col)
    panes = {
        'roof_rust': [(-0.03, 0.15, (5,), 'rust')],
        'wind': [(1, FULL, 'glass'),
                 (-1, [(0.06, 0.06), (0.93, 0.06), (0.93, 0.34), (0.55, 0.2), (0.45, 0.55), (0.25, 0.32), (0.06, 0.45)], 'glass_dirty')],
        'rear': [(-1, FULL, 'glass_dirty'), (1, [(0.06, 0.06), (0.93, 0.06), (0.5, 0.35), (0.32, 0.7), (0.06, 0.5)], 'glass_dirty')],
        'side': [
            (-1, [(-0.12, 1.42), (0.82, 1.42), (0.34, 2.05), (0.02, 2.05), (-0.1, 2.05)], 'glass'),
            (1, [(-0.12, 1.42), (0.82, 1.42), (0.56, 1.72), (0.36, 1.55), (0.2, 1.82), (-0.11, 1.62)], 'glass_dirty'),
        ],
    }
    cabin(m, paint, shape, zbelt, panes)

    # Engine sitting in the open bay, and the alligator hood propped open above it.
    m.box((0.62, 0.72, 0.24), (0, 1.5, 1.38), 'gunmetal')
    m.box((0.46, 0.5, 0.14), (0, 1.48, 1.54), 'rust_dark')
    m.cyl(0.13, 0.1, (0.05, 1.58, 1.64), 'steel_dark', seg=10)
    m.box((0.6, 0.08, 0.32), (0, 1.97, 1.26), 'steel_dark')
    m.tube([(-0.33, 1.22, 1.38), (-0.45, 1.5, 1.42), (-0.42, 1.9, 1.34)], 0.022, 'black', seg=5)
    m.tube([(0.33, 1.3, 1.42), (0.45, 1.6, 1.36)], 0.022, 'rust', seg=5)
    th = 54.0
    hood_l = 1.02
    cy, cz = 1.03, 1.36
    hc = (0, cy + hood_l / 2 * math.cos(math.radians(th)), cz + hood_l / 2 * math.sin(math.radians(th)))
    m.box((1.2, hood_l, 0.05), hc, paint, rot=(th, 0, 0))
    # Hood skin patches + crown ridge, in the hood's own tilted frame so they sit proud, not coplanar.
    for s, t, c in [(-0.36, 0.2, 'rust_dark'), (0.32, -0.25, 'rust_dark')]:
        ly = t
        dn = 0.032
        loc = (s, hc[1] + ly * math.cos(math.radians(th)) + dn * -math.sin(math.radians(th)),
               hc[2] + ly * math.sin(math.radians(th)) + dn * math.cos(math.radians(th)))
        m.box((0.36, 0.34, 0.012), loc, c, rot=(th, 0, 0))
    m.tube([(0, cy + 0.03 * math.cos(math.radians(th)) - 0.02, cz + 0.03 * math.sin(math.radians(th)) + 0.015),
            (0, cy + 0.98 * math.cos(math.radians(th)) - 0.02, cz + 0.98 * math.sin(math.radians(th)) + 0.015)], 0.018, 'rust_dark', seg=5)
    # Prop rod: the only thing holding it up.
    m.tube([(0.52, 1.72, 1.2), (0.52, 1.32, 1.66)], 0.012, 'steel_dark', seg=4)

    # Bed: floor ribs, a spare tyre and a loose plank. All resting on the floor.
    for x in (-0.48, -0.24, 0.0, 0.24, 0.48):
        m.box((0.05, 1.7, 0.02), (x, -1.17, bedz + 0.052), 'steel_dark')
    m.cyl(0.33, 0.2, (-0.28, -1.55, bedz + 0.17), 'rubber', seg=12)
    m.cyl(0.14, 0.02, (-0.28, -1.55, bedz + 0.28), 'rust', seg=8)
    m.box((0.22, 1.2, 0.05), (0.42, -1.05, bedz + 0.095), 'wood_grey', rot=(0, 0, 8))
    m.box((0.2, 0.9, 0.05), (0.2, -0.85, bedz + 0.135), 'wood_dark', rot=(0, 0, -11))
    # Tailgate rust, rail caps and trim.
    m.box((0.34, 0.02, 0.14), (-0.34, -2.205, 0.84), 'rust_dark')
    m.box((0.3, 0.02, 0.14), (0.42, -2.205, 0.72), 'rust_dark')
    m.box((1.44, 0.03, 0.07), (0, -2.135, 1.26), 'black')
    for x in (-0.78, 0.78):
        m.cyl(0.05, 0.05, (x, -2.2, 1.16), 'warning_red' if x < 0 else 'black', rot=(-90, 0, 0), seg=8)
    # Rear step bumper, one bracket gone so it dips at one end.
    m.box((0.09, 0.2, 0.08), (0.46, -2.1, 0.48), 'steel_dark')
    m.tube([(-0.88, -2.0, 0.28), (-0.72, -2.16, 0.34), (0.3, -2.175, 0.46), (0.86, -2.12, 0.46), (0.92, -1.98, 0.46)], 0.07, 'rust', seg=6)

    nose(m, paint, left_lamp='smashed', right_lamp='glass_dirty')
    side_trim(m, shape, -1, [(-1.9, -1.0), (0.0, 0.8), (1.1, 1.85)])
    side_trim(m, shape, 1, [(-0.6, 0.15), (0.95, 1.8)])
    door_handles(m, [(-1, -0.02), (1, -0.02)])
    running_gear(m, [(-0.875, 1.4, False, 'steel'), (0.875, 1.4, False, 'none'),
                     (-0.875, -1.4, False, 'steel'), (0.875, -1.4, True, 'steel')])


def build_car(asset_id, paint, kind):
    m = Model(asset_id, seed=41 if kind == 'sedan' else 42)
    (build_sedan if kind == 'sedan' else build_pickup)(m, paint)
    finish(m, bounds=BOUNDS)
