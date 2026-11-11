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
m = Model('laser_pistol', seed=606)
t = T(m, 28)
t.box((.3,.08,.1),(.02,0,.05),'plastic_blue',bevel=.02)
t.cyl(.035,.14,(.24,0,.05),'chrome',rot=(0,90,0))
t.cyl(.02,.03,(.32,0,.05),'red_bright',rot=(0,90,0))
t.box((.07,.065,.17),(-.1,0,-.05),'steel_dark',rot=(0,-12,0))
t.box((.12,.09,.03),(.0,0,.11),'lamp_warm')
t.box((.1,.02,.04),(.05,0,-.01),'steel')
finish_icon(m)
