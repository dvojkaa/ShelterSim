import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_farm_windmill', seed=1210)
r = random.Random(1210)
TZ = 7.4
# lattice tower legs
for sx in (-1, 1):
    for sy in (-1, 1):
        m.tube([(sx * 1.4, sy * 1.4, 0.05), (sx * 0.55, sy * 0.55, TZ)], 0.08, 'steel_dark', seg=5)
        m.box((0.5, 0.5, 0.2), (sx * 1.4, sy * 1.4, 0.1), 'concrete_dark')
# cross braces at levels
levels = [0.12, 1.85, 3.7, 5.55, 7.4]
def half(z): return 1.4 - (1.4 - 0.55) * z / TZ
for a, b in zip(levels[:-1], levels[1:]):
    ha, hb = half(a), half(b)
    for sx in (-1, 1):
        m.tube([(sx * ha, -ha, a), (sx * hb, hb if sx > 0 else -hb, b)], 0.035, 'rust', seg=4) if False else None
    # ring braces
    for s in (-1, 1):
        m.box((2 * hb, 0.06, 0.06), (0, s * hb, b), 'steel')
        m.box((0.06, 2 * hb, 0.06), (s * hb, 0, b), 'steel')
    # X braces on four faces
    m.tube([(-ha, -ha, a), (hb, -hb, b)], 0.035, 'rust', seg=4)
    m.tube([(ha, -ha, a), (-hb, -hb, b)], 0.035, 'rust', seg=4)
    m.tube([(-ha, ha, a), (hb, hb, b)], 0.035, 'rust', seg=4)
    m.tube([(ha, ha, a), (-hb, hb, b)], 0.035, 'rust', seg=4)
    m.tube([(-ha, -ha, a), (-hb, hb, b)], 0.035, 'rust', seg=4)
    m.tube([(-ha, ha, a), (-hb, -hb, b)], 0.035, 'rust', seg=4)
    m.tube([(ha, -ha, a), (hb, hb, b)], 0.035, 'rust', seg=4)
    m.tube([(ha, ha, a), (hb, -hb, b)], 0.035, 'rust', seg=4)
# platform + gearbox head
m.box((1.4, 1.4, 0.12), (0, 0, TZ + 0.06), 'steel_dark')
m.box((0.5, 1.2, 0.6), (0, 0.1, TZ + 0.5), 'gunmetal', bevel=0.03)
m.cyl(0.18, 0.5, (0, 0.1, TZ + 0.5), 'steel', seg=8)
# ladder on -Y
for sx in (-1, 1):
    m.box((0.05, 0.06, 7.4), (sx * 0.25, -1.2 + 0.0, 3.7), 'steel_dark', rot=(-6, 0, 0)) if False else None
# rotor on -Y: hub at (0, -0.7, 8.4)
HZ = TZ + 1.0
m.tube([(0, 0.1, TZ + 0.5), (0, 0.1, HZ - 0.1)], 0.1, 'steel_dark', seg=6) if False else None
m.box((0.3, 0.3, HZ - TZ - 0.4), (0, 0.1, (HZ + TZ + 0.4) / 2 - 0.0), 'gunmetal') if False else None
m.cyl(0.2, 0.8, (0, -0.35, HZ), 'steel', rot=(90, 0, 0), seg=8)
m.cyl(0.28, 0.3, (0, -0.8, HZ), 'rust', rot=(90, 0, 0), seg=8, r2=0.1)
m.box((0.3, 0.3, 1.0), (0, 0.1, TZ + 0.6 + 0.0), 'gunmetal') if False else None
NB = 14
for i in range(NB):
    a = i * 360.0 / NB
    ar = math.radians(a)
    rad = 1.0
    m.box((1.0, 0.04, 0.4), (rad * math.cos(ar) * 1.0 + 0.0 * 0, -0.9, HZ + rad * math.sin(ar)), 'rust_light' if i % 2 else 'steel', rot=(18, -a, 0))
# ring
for i in range(24):
    a = math.radians(i * 15)
    m.box((0.1, 0.05, 0.12), (1.45 * math.cos(a), -0.9, HZ + 1.45 * math.sin(a)), 'steel_dark', rot=(0, -i * 15 + 90, 0)) if False else None
for i in range(8):
    a = math.radians(i * 45)
    m.tube([(0, -0.9, HZ), (1.45 * math.cos(a), -0.9, HZ + 1.45 * math.sin(a))], 0.025, 'steel_dark', seg=4)
# tail boom and vane
m.box((0.14, 2.8, 0.14), (0, 1.7, TZ + 0.7), 'steel_dark')
m.box((0.05, 1.4, 1.2), (0, 3.0, TZ + 1.0), 'rust', taper=(1, 1))
m.box((0.05, 1.0, 0.5), (0, 3.2, TZ + 1.7), 'rust_dark')
# base pump housing, trough, rod
m.box((0.5, 0.5, 0.9), (0, 0, 0.5), 'steel_dark', bevel=0.03)
m.cyl(0.07, 6.8, (0, 0, 3.9), 'steel', seg=6)
m.box((1.8, 0.7, 0.5), (0, -2.6, 0.25), 'concrete_dark', bevel=0.03)
m.box((1.5, 0.5, 0.2), (0, -2.6, 0.5), 'water')
m.tube([(0, -0.2, 0.7), (0, -1.0, 0.7), (0, -2.2, 0.7)], 0.07, 'steel_dark', seg=5)
finish(m, bounds={'x': (-1.8, 1.8), 'y': (-3.1, 3.8), 'z': (0, 10.4)})
