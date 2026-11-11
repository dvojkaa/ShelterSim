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
m = Model('plasma_rifle', seed=608)
t = T(m, 42)
t.box((.46,.1,.12),(0,0,.05),'steel_dark',bevel=.02)
t.cyl(.07,.2,(.3,0,.06),'chrome',rot=(0,90,0),seg=12)
t.cyl(.055,.2,(.3,0,.06),'leaf_green',rot=(0,90,0),seg=12)
t.cyl(.075,.03,(.42,0,.06),'chrome',rot=(0,90,0),seg=12)
t.box((.07,.07,.15),(-.04,0,-.1),'gunmetal',rot=(0,-12,0))
t.box((.22,.08,.1),(-.35,0,.0),'gunmetal',bevel=.01)
t.box((.16,.11,.04),(-.03,0,.13),'leaf_green')
finish_icon(m)
