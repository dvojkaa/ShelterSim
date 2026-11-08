import math, random
import bmesh
from mathutils import Matrix, Vector
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
m = Model('rubble_a', seed=107)
P = ('concrete_light', 'concrete', 'concrete_dark')
d = m.default
# Block of cast concrete: straight poured sides below, shattered top. Fallen wedge at its foot.
main = Chunk(d, (0.84, 0.9, 1.0), (-0.08, 0.05), 0.0, P, 151, cuts=7, keep=True, base_lift=0.25, d=(0.42, 0.52))
Chunk(d, (0.44, 0.46, 0.5), (0.28, -0.27), 0.0, P, 152, cuts=5, keep=True, base_lift=0.25)
Chunk(d, (0.3, 0.26, 0.24), (-0.28, -0.37), 0.0, P, 153, cuts=6)
vein(d, main, [(-105, -8), (-95, 10), (-85, 28), (-65, 38)], 0.012, 'concrete_dark')
vein(d, main, [(120, 15), (105, 34), (85, 46)], 0.012, 'concrete_dark')
finish(m, bounds={'x': (-0.5, 0.5), 'y': (-0.5, 0.5), 'z': (0, 1.0)})
