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
m = Model('hunting_rifle', seed=604)
t = T(m, 42)
t.cyl(.022,.7,(.25,0,.06),'gunmetal',rot=(0,90,0))
t.box((.5,.06,.07),(-.15,0,.0),'wood_light',bevel=.01)
t.box((.12,.065,.12),(-.45,0,-.03),'wood',rot=(0,-10,0))
t.box((.15,.05,.05),(.1,0,.0),'wood',bevel=.01)
t.cyl(.03,.3,(-.02,0,.14),'gunmetal',rot=(0,90,0))
t.cyl(.045,.04,(.12,0,.14),'steel',rot=(0,90,0))
t.cyl(.045,.04,(-.16,0,.14),'steel',rot=(0,90,0))
finish_icon(m)
