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
m = Model('warm_coat', seed=615)
t = T(m, 0)
m.box((.36,.18,.5),(0,0,0),'orange',bevel=.03)
m.box((.12,.14,.46),(-.24,0,-.04),'orange',rot=(0,10,0),bevel=.03)
m.box((.12,.14,.46),(.24,0,-.04),'orange',rot=(0,-10,0),bevel=.03)
m.box((.4,.2,.06),(0,0,.24),'fabric_white',bevel=.02)
m.box((.4,.19,.05),(0,0,-.24),'mustard_dark')
m.box((.03,.19,.5),(0,0,0),'black')
m.box((.3,.2,.05),(0,0,.08),'orange_dark' if False else 'rust_light')
finish_icon(m)
