import math
from mathutils import Matrix, Euler, Vector
from ss_art import Model, finish_icon
class T:
    """Wrapper applying a global rotation (about the view axis by `tilt` deg) to every part."""
    def __init__(s, m, tilt=28):
        s.m = m
        f = Vector((1, 1.25, -0.8)).normalized()
        s.R = Matrix.Rotation(math.radians(tilt), 3, f)
    def _r(s, loc, rot):
        L = s.R @ Vector(loc)
        E = (s.R @ Euler([math.radians(a) for a in rot]).to_matrix()).to_euler()
        return tuple(L), tuple(math.degrees(a) for a in E)
    def box(s, size, loc, color, rot=(0, 0, 0), **k):
        l, r = s._r(loc, rot); s.m.box(size, l, color, r, **k)
    def cyl(s, r, h, loc, color, rot=(0, 0, 0), **k):
        l, rr = s._r(loc, rot); s.m.cyl(r, h, l, color, rr, **k)
m = Model('padded_jacket', seed=611)
t = T(m, 0)
m.box((.36,.18,.42),(0,0,0),'fabric_blue',bevel=.03)
for y in (-.09,0,.09,.18):pass
for z in (-.12,0,.12):
    m.box((.38,.19,.03),(0,0,z),'fabric_grey')
m.box((.12,.14,.4),(-.24,0,-.02),'fabric_blue',rot=(0,10,0),bevel=.03)
m.box((.12,.14,.4),(.24,0,-.02),'fabric_blue',rot=(0,-10,0),bevel=.03)
m.box((.04,.19,.4),(0,-.0,0),'black')
m.box((.2,.16,.06),(0,0,.22),'fabric_grey',bevel=.02)
finish_icon(m)
