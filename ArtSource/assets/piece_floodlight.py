"""Tripod work floodlight. Lens faces +Y, head tilted down. Bounds x .25 y .2 z 0..0.5."""
import math
from mathutils import Vector
from ss_art import Model, finish
m = Model('piece_floodlight', seed=202)

def seg(a, b, ra, rb, color, n=6):
    a, b = Vector(a), Vector(b); d = b - a
    e = d.to_track_quat('Z', 'Y').to_euler()
    m.cyl(ra, d.length, (a + b) / 2, color, rot=tuple(math.degrees(x) for x in e), seg=n, r2=rb)

top = (0, 0, 0.3)
for a in (90, 210, 330):
    r = math.radians(a)
    foot = (0.17 * math.cos(r), 0.17 * math.sin(r), 0.015)
    seg(top, foot, 0.014, 0.012, 'steel_dark')
    m.box((0.05, 0.05, 0.016), foot, 'rubber', rot=(0, 0, a))
m.cyl(0.04, 0.06, (0, 0, 0.3), 'gunmetal', seg=8)
m.cyl(0.015, 0.1, (0, 0, 0.36), 'steel', seg=6)
# head, tilted down 22 degrees about its pivot
pc = Vector((0, 0, 0.38)); ang = -22
ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
def hb(size, off, color, **k):
    y, z = off[1], off[2]
    loc = (pc.x + off[0], pc.y + y * ca - z * sa, pc.z + y * sa + z * ca)
    m.box(size, loc, color, rot=(ang, 0, 0), **k)
hb((0.4, 0.1, 0.2), (0, 0, 0), 'steel_dark', bevel=0.012)          # housing
hb((0.36, 0.01, 0.16), (0, 0.055, 0), 'lamp_warm')                  # lens
hb((0.4, 0.04, 0.02), (0, 0.075, 0.105), 'gunmetal')                # hood top
hb((0.4, 0.04, 0.02), (0, 0.075, -0.105), 'gunmetal')
hb((0.02, 0.04, 0.2), (-0.2, 0.075, 0), 'gunmetal')
hb((0.02, 0.04, 0.2), (0.2, 0.075, 0), 'gunmetal')
hb((0.3, 0.03, 0.14), (0, -0.065, 0), 'gunmetal')                   # rear cooling cap
for i in range(-2, 3):
    hb((0.012, 0.02, 0.15), (i * 0.05, -0.09, 0), 'steel')
hb((0.12, 0.05, 0.01), (0.1, 0.0, 0.103), 'rust')
# yoke
m.box((0.012, 0.03, 0.12), (-0.215, 0, 0.38), 'steel', bevel=0.003)
m.box((0.012, 0.03, 0.12), (0.215, 0, 0.38), 'steel', bevel=0.003)
m.box((0.44, 0.03, 0.012), (0, 0, 0.33), 'steel')
m.tube([(0.0, -0.05, 0.31), (0.05, -0.15, 0.1), (0.12, -0.17, 0.015)], 0.007, 'black', seg=4)
finish(m, bounds={'x': (-0.25, 0.25), 'y': (-0.2, 0.2), 'z': (0, 0.5)})
