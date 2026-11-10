import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _lmk_sign import sign_base
m = Model('lmk_sign_factory', seed=1102)
sign_base(m, 'mustard')
# gear
m.cyl(0.62, 0.05, (-0.9, -0.11, 2.5), 'gunmetal', rot=(90, 0, 0), seg=10)
for i in range(10):
    a = i * 36
    m.box((0.28, 0.05, 0.3), (-0.9 + 0.7 * math.cos(math.radians(a)), -0.11, 2.5 + 0.7 * math.sin(math.radians(a))), 'gunmetal', rot=(0, -a, 0))
m.cyl(0.24, 0.06, (-0.9, -0.14, 2.5), 'mustard', rot=(90, 0, 0), seg=10)
# chimney + smoke
m.box((0.5, 0.05, 1.0), (0.7, -0.11, 2.2), 'gunmetal', taper=(0.8, 1))
m.box((0.62, 0.06, 0.12), (0.7, -0.115, 2.7), 'black')
m.box((0.9, 0.05, 0.4), (1.2, -0.11, 2.0), 'gunmetal')
for k, (x, z, s) in enumerate([(0.75, 2.95, 0.22), (0.95, 3.12, 0.3), (1.25, 3.2, 0.34)]):
    m.box((s, 0.05, s), (x, -0.11, z), 'steel_dark', rot=(0, 20 * k, 0))
finish(m, bounds={'x': (-2.0, 2.0), 'y': (-0.3, 0.4), 'z': (0, 3.6)})
