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
m = Model('scrap_armor', seed=613)
t = T(m, 0)
m.box((.36,.18,.42),(0,0,0),'leather',bevel=.03)
m.box((.14,.2,.12),(-.2,0,.2),'rust',rot=(0,15,0))
m.box((.14,.2,.12),(.2,0,.2),'rust_dark',rot=(0,-15,0))
m.box((.2,.2,.16),(-.08,0,.03),'steel',rot=(0,0,0))
m.box((.16,.2,.2),(.1,0,-.06),'rust_light')
m.box((.3,.2,.08),(0,0,-.17),'steel_dark')
for p in ((-.15,.1),(-.02,.1),(.1,.04),(.1,-.14),(-.1,-.17)):
    m.cyl(.015,.215,(p[0],0,p[1]),'chrome',rot=(90,0,0),seg=6)
finish_icon(m)
