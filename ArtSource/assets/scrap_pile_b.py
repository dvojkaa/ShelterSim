import math, random
import bmesh
from mathutils import Euler, Matrix, Vector
from ss_art import Model, finish


class Chunk:
    """Hewn lump: a box sliced by seeded random planes, so it has big flat facets and a flat base at z=base.
    keep=True keeps all six box-face centres, so the chunk's bounding box is exactly `size`."""
    def __init__(self, part, size, xy, base, palette, seed, cuts=16, d=(0.33, 0.45), rz=0, keep=False, base_lift=-0.1):
        rng = random.Random(seed)
        self.size, self.rz = Vector(size), rz
        self.centre = Vector((xy[0], xy[1], base + size[2] / 2))
        self.planes = []
        t = bmesh.new(); t.loops.layers.uv.new('UVMap')
        bmesh.ops.create_cube(t, size=1.0)
        for _ in range(cuts):
            while True:
                n = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1)))
                if n.length > 0.01:
                    n.normalize()
                    if n.z > base_lift:
                        break
            dist = rng.uniform(*d)
            if keep:
                dist = max(dist, 0.5 * max(abs(n.x), abs(n.y), abs(n.z)) + 0.015)
            self.planes.append((n.copy(), dist))
            res = bmesh.ops.bisect_plane(t, geom=list(t.verts) + list(t.edges) + list(t.faces), dist=1e-5,
                                         plane_co=n * dist, plane_no=n, clear_outer=True)
            cut = [e for e in res['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]
            if cut:
                bmesh.ops.contextual_create(t, geom=cut)
        bmesh.ops.recalc_face_normals(t, faces=t.faces)
        bmesh.ops.scale(t, vec=size, verts=t.verts)
        bmesh.ops.recalc_face_normals(t, faces=t.faces)
        for f in t.faces:
            f.normal_update()
            part._paint(t, [f], shade(f.normal.z, palette, rng))
        part._commit(t, Matrix.Translation(self.centre) @ Matrix.Rotation(math.radians(rz), 4, 'Z'))

    def surf(self, az, el):
        """World (point, outward normal) where a ray from the chunk centre at azimuth/elevation degrees leaves it."""
        a, e = math.radians(az), math.radians(el)
        rot = Matrix.Rotation(math.radians(self.rz), 3, 'Z')
        w = Vector((math.cos(e) * math.cos(a), math.cos(e) * math.sin(a), math.sin(e)))
        lc = rot.transposed() @ w
        u = Vector((lc.x / self.size.x, lc.y / self.size.y, lc.z / self.size.z))
        best, nb = 1e9, Vector((0, 0, 1))
        planes = list(self.planes)
        for ax in range(3):
            for s in (-1, 1):
                nv = Vector((0, 0, 0)); nv[ax] = s
                planes.append((nv, 0.5))
        for n, dist in planes:
            k = n.dot(u)
            if k > 1e-6 and dist / k < best:
                best, nb = dist / k, n
        pu = u * best
        pt = rot @ Vector((pu.x * self.size.x, pu.y * self.size.y, pu.z * self.size.z)) + self.centre
        nw = rot @ Vector((nb.x / self.size.x, nb.y / self.size.y, nb.z / self.size.z))
        return pt, nw.normalized()


def shade(up, palette, rng):
    """palette = (light, mid, dark): top facets catch the light, undersides sink into shadow, with some noise."""
    light, mid, dark = palette
    r = rng.random()
    if up > 0.55:
        return light if r < 0.72 else mid
    if up > 0.05:
        return mid if r < 0.65 else (light if r < 0.8 else dark)
    return dark if r < 0.75 else mid


def pebbles(part, spots, palette, seed):
    """Small seated stones. spots = [(x, y, size), ...]"""
    for i, (x, y, s) in enumerate(spots):
        Chunk(part, (s, s * 0.85, s * 0.62), (x, y), 0.0, palette, seed + i, cuts=8, d=(0.3, 0.42), rz=(seed * 37 + i * 61) % 360)


def vein(part, chunk, path, r, color, sub=3):
    """Raised seam following the chunk's surface. path = [(az, el), ...]; each stretch is subdivided so it hugs the facets."""
    pts = []
    for (a0, e0), (a1, e1) in zip(path[:-1], path[1:]):
        for i in range(sub):
            k = i / sub
            pts.append(chunk.surf(a0 + (a1 - a0) * k, e0 + (e1 - e0) * k)[0])
    pts.append(chunk.surf(*path[-1])[0])
    part.tube(pts, r, color, seg=4)


def nugget(part, chunk, az, el, s, colors, seed):
    """Lump of metal half-sunk in the rock surface."""
    pt, n = chunk.surf(az, el)
    part.blob((s, s * 0.9, s * 0.8), pt + n * s * 0.12, colors[0], rough=0.26, subdiv=1, seed=seed, colors=colors)


def tyre(p, R, r, loc, rot, seg=14, sides=6, hub='steel_dark'):
    """Tyre on local Z axis with a steel hub, so it reads as a wheel off a wreck."""
    t = bmesh.new(); t.loops.layers.uv.new('UVMap')
    rings = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        ring = []
        for j in range(sides):
            b = 2 * math.pi * (j + 0.5) / sides
            rad = R + r * math.cos(b) * 1.08
            ring.append(t.verts.new((rad * math.cos(a), rad * math.sin(a), r * math.sin(b))))
        rings.append(ring)
    for i in range(seg):
        for j in range(sides):
            t.faces.new((rings[i][j], rings[(i + 1) % seg][j], rings[(i + 1) % seg][(j + 1) % sides], rings[i][(j + 1) % sides]))
    bmesh.ops.recalc_face_normals(t, faces=t.faces)
    p._paint(t, t.faces, 'rubber')
    p._commit(t, p._xf(loc, rot))
    p.cyl(R * 0.72, r * 1.1, loc, hub, rot=rot, seg=10)
    p.cyl(R * 0.22, r * 1.5, loc, 'gunmetal', rot=rot, seg=6)


def _deg(e):
    return tuple(math.degrees(a) for a in e)


def gear(p, R, th, loc, rot, color='steel', teeth=10):
    """Toothed gear on local Z axis: big readable teeth, dark bore."""
    M = Euler([math.radians(a) for a in rot]).to_matrix()
    L = Vector(loc)
    p.cyl(R, th, loc, color, rot=rot, seg=teeth * 2)
    p.cyl(R * 0.34, th * 1.5, loc, 'gunmetal', rot=rot, seg=8)
    p.cyl(R * 0.16, th * 1.7, loc, 'black', rot=rot, seg=6)
    for i in range(teeth):
        a = 2 * math.pi * i / teeth
        c = L + M @ Vector((math.cos(a) * R * 1.08, math.sin(a) * R * 1.08, 0))
        e = (M @ Matrix.Rotation(a, 3, 'Z')).to_euler('XYZ')
        p.box((R * 0.3, R * 0.3, th), tuple(c), color, rot=_deg(e), taper=None)


def bent(p, pts, thick, width, loc, rot, colors):
    """Folded sheet: polyline pts in local XZ, one coloured panel per fold, so rust and bare metal alternate."""
    for i, (a, b) in enumerate(zip(pts[:-1], pts[1:])):
        dx, dz = b[0] - a[0], b[1] - a[1]
        l = math.hypot(dx, dz)
        nx, nz = -dz / l * thick, dx / l * thick
        ov = 0.15 * thick / l
        a2 = (a[0] - dx * ov, a[1] - dz * ov); b2 = (b[0] + dx * ov, b[1] + dz * ov)
        p.poly([a2, b2, (b2[0] + nx, b2[1] + nz), (a2[0] + nx, a2[1] + nz)], width, loc, colors[i % len(colors)], rot=rot)


def corrugated(p, w, l, n, amp, thick, loc, rot, color):
    """Corrugated roofing sheet: w across the ribs (local X), l along them (local Y)."""
    top, bot = [], []
    for i in range(2 * n + 1):
        x = -w / 2 + w * i / (2 * n)
        z = amp if i % 2 else -amp
        top.append((x, z + thick))
        bot.append((x, z))
    p.poly(bot + top[::-1], l, loc, color, rot=rot)


def drum(p, r, h, loc, rot, color, band='rust_dark'):
    p.cyl(r, h, loc, color, rot=rot, seg=10)
    M = Euler([math.radians(a) for a in rot]).to_matrix()
    for k in (-0.33, 0.33):
        c = Vector(loc) + M @ Vector((0, 0, k * h))
        p.cyl(r * 1.05, h * 0.05, tuple(c), band, rot=rot, seg=10)


def litter(p, mound, n, seed, colors, lim=0.63):
    """Seeded plates and beams laid over the mound's surface so it reads as a heap of metal, not a rock.
    Each candidate is rejected if any corner would leave the footprint or sink below the ground."""
    rng = random.Random(seed)
    placed = 0
    while placed < n:
        az, el = rng.uniform(-180, 180), rng.uniform(12, 80)
        pt, nrm = mound.surf(az, el)
        beam = rng.random() < 0.3
        sx = rng.uniform(0.3, 0.46) if beam else rng.uniform(0.2, 0.38)
        sy = rng.uniform(0.05, 0.08) if beam else rng.uniform(0.16, 0.3)
        sz = 0.05 if beam else 0.018
        q = nrm.to_track_quat('Z', 'Y')
        tilt = Euler((math.radians(rng.uniform(-22, 22)), math.radians(rng.uniform(-22, 22)), math.radians(rng.uniform(0, 360))))
        M = q.to_matrix() @ tilt.to_matrix()
        c = pt + nrm * rng.uniform(0.01, 0.05)
        ok = True
        for ax in (-1, 1):
            for ay in (-1, 1):
                for az2 in (-1, 1):
                    w = c + M @ Vector((ax * sx / 2, ay * sy / 2, az2 * sz / 2))
                    if abs(w.x) > lim or abs(w.y) > lim or w.z < 0.004:
                        ok = False
        if not ok:
            continue
        p.box((sx, sy, sz), tuple(c), rng.choice(colors), rot=_deg(M.to_euler('XYZ')))
        placed += 1


def skirt(p, core, azimuths, seed, colors, lim=0.63):
    """Big sheets stood against the heap's flanks, feet on the ground, so the core never shows as a bare lump.
    Every third one is corrugated roofing. A sheet that won't fit the footprint is narrowed until it does."""
    rng = random.Random(seed)
    for i, az in enumerate(azimuths):
        a = math.radians(az)
        out = Vector((math.cos(a), math.sin(a), 0))
        tan = Vector((-out.y, out.x, 0))
        lean = math.radians(rng.uniform(16, 30))
        up = (out * -math.sin(lean) + Vector((0, 0, 1)) * math.cos(lean)).normalized()
        w, h = rng.uniform(0.38, 0.5), rng.uniform(0.38, 0.54)
        if i % 3 == 0:  # short and stood clear: a rib pattern cutting into the heap's slope leaves a sawtooth edge
            h, lean = min(h, 0.3), math.radians(10)
            up = (out * -math.sin(lean) + Vector((0, 0, 1)) * math.cos(lean)).normalized()
        wall = core.surf(az, 22)[0]
        radial = Vector((wall.x, wall.y, 0)).dot(out)
        if i % 3 == 0:  # clear the heap's widest point too, which is usually at its foot
            for e in (2, 10):
                wl = core.surf(az, e)[0]
                radial = max(radial, Vector((wl.x, wl.y, 0)).dot(out))
        foot = out * (radial + h * 0.62 * math.sin(lean) + (0.1 if i % 3 == 0 else 0.06))
        tw = (tan * math.cos(math.radians(rng.uniform(-10, 10))) + up * math.sin(math.radians(rng.uniform(-10, 10)))).normalized()
        nrm = tw.cross(up).normalized()
        up2 = nrm.cross(tw).normalized()
        while True:
            c = foot + up2 * (h / 2)
            cs = [c + tw * sx * w / 2 + up2 * sz * h / 2 for sx in (-1, 1) for sz in (-1, 1)]
            if all(abs(v.x) <= lim and abs(v.y) <= lim for v in cs) or w < 0.2:
                break
            w *= 0.9
        c.z += 0.008 - min(v.z for v in cs)
        color = colors[i % len(colors)]
        if i % 3 == 0:
            M = Matrix((tw, up2, nrm)).transposed()
            corrugated(p, w, h, max(3, int(w / 0.08)), 0.016, 0.012, tuple(c), _deg(M.to_euler('XYZ')), color)
        else:
            M = Matrix((tw, nrm, up2)).transposed()
            r = _deg(M.to_euler('XYZ'))
            p.box((w, 0.02, h), tuple(c), color, rot=r)
            # Rust bloom and a rib, so large plates don't read as blank cards.
            off = Vector(nrm) * (0.014 if nrm.dot(out) > 0 else -0.014)
            p.box((w * 0.44, 0.01, h * 0.34), tuple(c + off + tw * w * 0.2 - up2 * h * 0.22), 'rust_light' if color != 'rust' else 'rust_dark', rot=r)
            p.box((w, 0.012, 0.03), tuple(c + off * 1.2 + up2 * h * 0.25), 'steel_dark', rot=r)
m = Model('scrap_pile_b', seed=106)
d = m.default
core = Chunk(d, (0.92, 0.94, 0.56), (0.02, 0.02), 0.0, ('rust_dark', 'steel_dark', 'gunmetal'), 141, cuts=16, d=(0.36, 0.47))
litter(d, core, 10, 13, ['steel', 'steel', 'steel', 'steel_dark', 'rust', 'rust', 'rust_light', 'paint_green', 'army_green'])
# Two tyres stacked flat at the left foot
tyre(d, 0.17, 0.07, (-0.38, -0.38, 0.075), (0, 0, 0))
tyre(d, 0.17, 0.07, (-0.38, -0.36, 0.22), (6, 2, 30))
# Rusty drum on its side across the crown
drum(d, 0.17, 0.5, (0.1, 0.12, 0.66), (82, 0, 58), 'rust')
# Big gear standing up at the back: top of the silhouette
gear(d, 0.25, 0.05, (-0.2, 0.34, 0.76), (78, 0, -18), 'steel', teeth=11)
# Salvaged car door leaning on the front right: skin, window, handle, a rust patch.
door_loc, door_rot = Vector((0.26, -0.41, 0.34)), (-26, 0, 8)
M = Euler([math.radians(a) for a in door_rot]).to_matrix()


def door(size, off, color):
    d.box(size, tuple(door_loc + M @ Vector(off)), color, rot=door_rot)


door((0.66, 0.035, 0.36), (0, 0, -0.05), 'paint_green')
door((0.2, 0.012, 0.13), (0.15, -0.022, -0.1), 'rust')
door((0.08, 0.02, 0.022), (-0.2, -0.026, 0.06), 'chrome')
door((0.5, 0.012, 0.2), (0.0, 0, 0.22), 'glass_dirty')
door((0.05, 0.03, 0.25), (-0.28, 0, 0.23), 'paint_green')
door((0.05, 0.03, 0.25), (0.28, 0, 0.23), 'paint_green')
door((0.61, 0.03, 0.045), (0, 0, 0.34), 'paint_green')
# Pipes
d.tube([(0.3, 0.05, 0.46), (0.42, 0.18, 0.86), (0.42, 0.34, 1.02)], 0.036, 'steel_dark', seg=7)
d.tube([(0.02, -0.1, 0.5), (-0.04, -0.14, 1.12)], 0.03, 'steel', seg=7)
# Sheets stood round the flanks not covered by door and tyres
skirt(d, core, [0, 50, 95, 140, 185], 33, ['rust_light', 'steel', 'rust', 'steel', 'steel_dark'])
# Fallen bits
d.box((0.18, 0.12, 0.018), (0.54, -0.55, 0.009), 'rust', rot=(0, 0, 18))
d.box((0.14, 0.14, 0.018), (-0.54, 0.48, 0.009), 'steel', rot=(0, 0, 33))
finish(m, bounds={'x': (-0.65, 0.65), 'y': (-0.65, 0.65), 'z': (0, 1.15)})
