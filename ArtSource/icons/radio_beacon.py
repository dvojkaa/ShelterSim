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
m = Model('radio_beacon', seed=622)
t = T(m, 0)
m.box((.3,.22,.2),(0,0,0),'army_green',bevel=.02)
m.box((.24,.02,.12),(0,-.115,.0),'steel_dark')
m.cyl(.04,.03,(-.07,-.13,0),'chrome',rot=(90,0,0))
m.cyl(.04,.03,(.07,-.13,0),'chrome',rot=(90,0,0))
m.cyl(.015,.35,(.1,.05,.25),'chrome',seg=6)
m.cyl(.035,.04,(.1,.05,.43),'red_bright',seg=8)
m.box((.1,.02,.05),(-.04,-.115,.07),'lamp_warm')
m.box((.31,.23,.03),(0,0,.1),'army_green_dark')
finish_icon(m)
