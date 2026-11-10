import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('bld_door_apt_entrance', seed=202)
# Heavy steel apartment-block entrance door leaf. Origin floor centre, hinge x=-0.9, front -Y.
m.box((1.8, 0.07, 2.2), (0, 0, 1.1), 'steel_dark')
# front skin plate with bevelled edge
m.box((1.68, 0.02, 2.08), (0, -0.045, 1.1), 'steel', bevel=0.012)
# riveted rail bands
for z in (0.12, 0.6, 2.1):
    m.box((1.72, 0.015, 0.08), (0, -0.058, z), 'gunmetal')
for x in (-0.82, 0.82):
    m.box((0.06, 0.015, 2.0), (x, -0.058, 1.1), 'gunmetal')
for i in range(9):
    for z in (0.12, 0.6, 2.1):
        m.box((0.03, 0.012, 0.03), (-0.72 + i * 0.18, -0.068, z), 'steel_dark')
# wire-glass window, square, head height
wx, wz = 0.0, 1.6
m.box((0.5, 0.02, 0.5), (wx, -0.05, wz), 'gunmetal')
m.box((0.38, 0.03, 0.38), (wx, -0.052, wz), 'glass_dirty')
for i in (-1, 1):
    for k in range(3):
        o = (k - 1) * 0.12
        m.box((0.46, 0.006, 0.008), (wx, -0.069, wz + o * 1.0), 'steel_dark', rot=(0, 45 * i, 0)) if False else None
# diamond wire mesh: two sets of diagonals
for i in range(-2, 3):
    for s in (-45, 45):
        m.box((0.3, 0.006, 0.008), (wx + i * 0.075 * 1.0, -0.069, wz + (0.0)), 'steel_dark', rot=(0, s, 0)) if False else None
for s in (-45, 45):
    for o in (-0.18, -0.09, 0.0, 0.09, 0.18):
        L = 0.38 * 1.4 - abs(o) * 1.2
        m.box((L * 0.55, 0.006, 0.008), (wx + o * 0.0 + (o if s == 45 else -o) * 0.5, -0.0695, wz + o * 0.5), 'steel_dark', rot=(0, s, 0))
# frame screws and cracks
for (x, z, l, a) in ((0.05, 1.62, 0.2, 20), (0.1, 1.55, 0.14, -60), (-0.1, 1.7, 0.16, 80)):
    m.box((l, 0.006, 0.008), (wx + x, -0.073, z), 'black', rot=(0, a, 0))
# horizontal push bar
m.box((1.3, 0.04, 0.05), (0.0, -0.1, 1.05), 'chrome', bevel=0.008) if False else None
m.cyl(0.025, 1.15, (0.1, -0.085, 1.05), 'steel', rot=(0, 90, 0), seg=6)
for x in (-0.4, 0.6):
    m.box((0.06, 0.04, 0.08), (x, -0.07, 1.05), 'gunmetal')
m.box((0.16, 0.02, 0.2), (0.82, -0.06, 1.05), 'gunmetal')
# blank stencil plate
m.box((0.5, 0.012, 0.18), (-0.1, -0.058, 0.85), 'concrete_light') if False else None
m.box((0.36, 0.012, 0.14), (0.0, -0.06, 0.42), 'plaster_dirty')
m.box((0.36, 0.006, 0.012), (0.0, -0.067, 0.46), 'concrete_dark')
# door closer: body on door top, arm toward hinge side
m.box((0.34, 0.06, 0.1), (-0.4, -0.085, 2.0), 'gunmetal', bevel=0.008)
m.box((0.34, 0.015, 0.04), (-0.5, -0.085, 2.14), 'steel_dark')   # arm
m.box((0.3, 0.012, 0.03), (-0.7, -0.085, 2.08), 'steel', rot=(0, 20, 0))
# rust streaks and patches
for (x, z, sx, sz, c) in ((-0.65, 1.2, 0.05, 0.8, 'rust'), (-0.5, 0.3, 0.08, 0.35, 'rust_dark'), (0.6, 0.15, 0.2, 0.12, 'rust'),
                          (0.3, 1.1, 0.04, 0.5, 'rust_dark'), (0.25, 1.35, 0.03, 0.25, 'rust'), (-0.2, 2.0, 0.1, 0.07, 'rust_light'),
                          (0.7, 1.9, 0.12, 0.16, 'rust')):
    m.box((sx, 0.006, sz), (x, -0.0575, z), c)
# hinges
for z in (0.3, 1.1, 1.9):
    m.cyl(0.05, 0.28, (-0.9, -0.03, z), 'rust_dark', seg=6)
    m.box((0.28, 0.02, 0.1), (-0.78, -0.068, z), 'gunmetal')
# kick plate
m.box((1.5, 0.014, 0.18), (0.0, -0.062, 0.09), 'steel_dark')
m.box((0.05, 0.01, 0.01), (0.3, 0.04, 1.0), 'steel_dark')  # back-face marker
m.box((1.7, 0.01, 2.0), (0, 0.04, 1.1), 'steel_dark')
m.box((1.2, 0.03, 0.05), (0.0, 0.055, 1.05), 'steel')
finish(m, bounds={'x': (-0.95, 0.95), 'y': (-0.12, 0.12), 'z': (0, 2.2)})
