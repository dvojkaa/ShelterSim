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
m = Model('radio_part', seed=621)
t = T(m, 0)
m.box((.3,.22,.03),(0,0,0),'paint_green')
m.box((.1,.1,.06),(-.06,0,.045),'black')
m.cyl(.04,.1,(.08,.04,.07),'copper' if False else 'rust_light',seg=10)
m.cyl(.03,.08,(.08,-.06,.06),'plastic_blue',seg=10)
m.box((.08,.03,.04),(-.1,-.08,.035),'mustard')
m.cyl(.01,.3,(.0,.1,.12),'chrome',rot=(0,25,0),seg=6)
m.box((.2,.012,.012),(0,.09,.02),'hazard_yellow')
finish_icon(m)
