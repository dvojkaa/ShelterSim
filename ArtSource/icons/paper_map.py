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
m = Model('paper_map', seed=623)
t = T(m, 0)
m.box((.3,.2,.015),(0,0,0),'sand',rot=(0,0,0))
m.box((.1,.2,.02),(-.1,0,.012),'fabric_white',rot=(0,0,0))
m.box((.1,.2,.02),(.0,0,.0),'sand')
m.box((.1,.2,.02),(.1,0,.012),'fabric_white')
m.box((.3,.006,.025),(0,-.04,.02),'rust_light',rot=(0,0,20))
m.box((.1,.006,.025),(.05,.05,.025),'leaf_green')
m.box((.03,.03,.03),(-.08,.04,.03),'warning_red',rot=(0,0,45))
m.box((.3,.2,.01),(.02,.02,-.012),'plaster')
finish_icon(m)
