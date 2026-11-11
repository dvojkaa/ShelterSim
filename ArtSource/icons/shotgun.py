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
m = Model('shotgun', seed=603)
t = T(m, 42)
t.cyl(.035,.62,(.2,0,.07),'steel_dark',rot=(0,90,0))
t.cyl(.032,.5,(.2,0,.0),'gunmetal',rot=(0,90,0))
t.box((.24,.08,.07),(.0,0,.03),'steel',bevel=.01)
t.box((.3,.07,.11),(-.25,0,-.02),'wood_light',rot=(0,-10,0),bevel=.01)
t.box((.2,.08,.06),(.22,0,-.04),'wood',bevel=.01)
t.box((.07,.06,.1),(-.1,0,-.08),'wood',rot=(0,-15,0))
finish_icon(m)
