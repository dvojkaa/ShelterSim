import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

from _lmk_sign import sign_base
m = Model('lmk_sign_bunker', seed=1202)
sign_base(m, 'hazard_yellow')
# trefoil: three wedges + hub
def wedge(a0, a1, r0, r1, n=6):
    pts = [(r1 * math.cos(math.radians(a0 + (a1 - a0) * k / n)), r1 * math.sin(math.radians(a0 + (a1 - a0) * k / n))) for k in range(n + 1)]
    pts += [(r0 * math.cos(math.radians(a1 - (a1 - a0) * k / n)), r0 * math.sin(math.radians(a1 - (a1 - a0) * k / n))) for k in range(n + 1)]
    return pts
for k in range(3):
    a = 90 + k * 120
    m.poly(wedge(a - 30, a + 30, 0.18, 0.62), 0.05, (-1.0, -0.12, 2.5), 'black')
m.cyl(0.14, 0.05, (-1.0, -0.12, 2.5), 'black', rot=(90, 0, 0), seg=10)
# blast door icon: black slab with dark door and wheel
m.box((1.3, 0.05, 1.3), (0.8, -0.115, 2.5), 'black')
m.box((0.9, 0.06, 1.0), (0.8, -0.12, 2.5), 'steel_dark')
m.box((0.12, 0.07, 1.0), (0.45, -0.125, 2.5), 'hazard_yellow')
m.cyl(0.2, 0.06, (0.9, -0.14, 2.5), 'hazard_yellow', rot=(90, 0, 0), seg=10)
m.cyl(0.08, 0.07, (0.9, -0.15, 2.5), 'black', rot=(90, 0, 0), seg=8)
finish(m, bounds={'x': (-2.0, 2.0), 'y': (-0.3, 0.4), 'z': (0, 3.6)})
