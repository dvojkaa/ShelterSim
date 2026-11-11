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
m = Model('assault_rifle', seed=605)
t = T(m, 42)
t.box((.5,.075,.09),(0,0,.05),'army_green_dark',bevel=.01)
t.cyl(.022,.35,(.4,0,.06),'gunmetal',rot=(0,90,0))
t.box((.3,.06,.05),(.28,0,.12),'army_green',bevel=.01)
t.box((.07,.06,.22),(.0,0,-.12),'gunmetal',rot=(0,-15,0))
t.box((.07,.06,.14),(-.16,0,-.07),'rubber',rot=(0,-12,0))
t.box((.3,.06,.1),(-.4,0,.02),'army_green',bevel=.01)
t.box((.12,.03,.04),(-.1,0,.13),'steel')
finish_icon(m)
