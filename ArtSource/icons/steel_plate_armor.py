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
m = Model('steel_plate_armor', seed=614)
t = T(m, 0)
m.box((.38,.2,.44),(0,0,0),'steel',bevel=.04)
m.box((.18,.22,.2),(-.1,0,.07),'chrome',bevel=.03)
m.box((.18,.22,.2),(.1,0,.07),'chrome',bevel=.03)
m.box((.34,.22,.1),(0,0,-.12),'steel_dark',bevel=.02)
m.box((.17,.18,.12),(-.23,0,.22),'steel_dark',rot=(0,15,0),bevel=.04)
m.box((.17,.18,.12),(.23,0,.22),'steel_dark',rot=(0,-15,0),bevel=.04)
m.box((.04,.23,.44),(0,0,0),'steel_dark')
for p in (-.15,.15): m.cyl(.02,.23,(p,0,-.12),'chrome',rot=(90,0,0),seg=6)
finish_icon(m)
