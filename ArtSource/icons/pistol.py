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
m = Model('pistol', seed=601)
t = T(m, 28)
t.box((.34,.07,.08),(.04,0,.05),'gunmetal',bevel=.01)
t.box((.34,.075,.02),(.04,0,.1),'steel')
t.box((.07,.065,.17),(-.1,0,-.05),'wood',rot=(0,-12,0))
t.box((.14,.02,.02),(.0,0,-.03),'gunmetal')
t.box((.03,.03,.03),(.2,0,.1),'chrome')
finish_icon(m)
