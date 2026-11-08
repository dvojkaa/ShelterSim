import math
import bmesh
from mathutils import Matrix, Vector

def solid(m, verts, faces, color, centre=(0, 0, 0)):
    """Raw polygons; faces auto-oriented outward from centre. color: name or list per face."""
    part = m.default
    t = bmesh.new(); t.loops.layers.uv.new('UVMap')
    vs = [t.verts.new(v) for v in verts]
    c = Vector(centre)
    for i, idx in enumerate(faces):
        pts = [Vector(verts[j]) for j in idx]
        n = Vector((0, 0, 0))
        for a, b in zip(pts, pts[1:] + pts[:1]):
            n += a.cross(b)
        cen = sum(pts, Vector()) / len(pts)
        idx = list(idx)
        if n.dot(cen - c) < 0:
            idx.reverse()
        col = color[i] if isinstance(color, (list, tuple)) else color
        part._paint(t, [t.faces.new([vs[j] for j in idx])], col)
    part._commit(t, Matrix.Identity(4))

def beam(m, p0, p1, thick, depth, y, color):
    """Flat bar in the XZ plane from p0=(x,z) to p1=(x,z), at world y."""
    dx, dz = p1[0] - p0[0], p1[1] - p0[1]
    L = math.hypot(dx, dz)
    th = -math.degrees(math.atan2(dz, dx))
    m.box((L, depth, thick), ((p0[0] + p1[0]) / 2, y, (p0[1] + p1[1]) / 2), color, rot=(0, th, 0))

def beam_yz(m, p0, p1, thick, width, x, color):
    """Bar in the YZ plane from (y,z) to (y,z) at world x."""
    dy, dz = p1[0] - p0[0], p1[1] - p0[1]
    L = math.hypot(dy, dz)
    # box long axis local X; rot about Z 90 puts it on Y; then tilt about X
    a = math.degrees(math.atan2(dz, dy))
    m.box((width, L, thick), (x, (p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2), color, rot=(a, 0, 0))

def clip_diag(x0, x1, z0, z1, k, s):
    """Diagonal line z = s*x + k clipped to rect; returns two points or None."""
    pts = []
    for x in (x0, x1):
        z = s * x + k
        if z0 - 1e-9 <= z <= z1 + 1e-9: pts.append((x, z))
    for z in (z0, z1):
        x = (z - k) / s
        if x0 - 1e-9 <= x <= x1 + 1e-9: pts.append((x, z))
    pts = sorted(set((round(a, 5), round(b, 5)) for a, b in pts))
    return (pts[0], pts[-1]) if len(pts) >= 2 else None

def stripes(m, x0, x1, y, z0, z1, depth, c1='hazard_yellow', c2='black', n=None, w=0.16):
    """Hazard stripe band on an XZ face at y (diagonal bars clipped into the rect)."""
    m.box((x1 - x0, depth, z1 - z0), ((x0 + x1) / 2, y, (z0 + z1) / 2), c2)
    h = z1 - z0
    k = x0 - h
    i = 0
    while k < x1 + h:
        # bar centre line x = k + (z - z0)
        seg = clip_diag(x0, x1, z0, z1, -(k) + z0, 1.0)
        if seg:
            beam(m, seg[0], seg[1], w * 0.7, depth * 1.2, y, c1)
        k += w * 2.2
