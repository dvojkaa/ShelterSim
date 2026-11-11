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
m = Model('laser_rifle', seed=607)
t = T(m, 42)
t.box((.5,.08,.1),(0,0,.05),'plastic_blue',bevel=.02)
t.cyl(.03,.3,(.38,0,.05),'chrome',rot=(0,90,0))
t.cyl(.04,.05,(.52,0,.05),'red_bright',rot=(0,90,0))
t.box((.07,.065,.15),(-.02,0,-.1),'steel_dark',rot=(0,-12,0))
t.box((.25,.07,.11),(-.38,0,.0),'steel_dark',bevel=.01)
t.box((.2,.09,.03),(.0,0,.12),'lamp_warm')
t.box((.12,.05,.05),(.2,0,.12),'steel')
finish_icon(m)
