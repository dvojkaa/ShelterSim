import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('lmk_factory_stack', seed=1109)
r = random.Random(1109)
m.cyl(1.9, 0.5, (0, 0, 0.25), 'concrete_dark', seg=12)
m.cyl(1.7, 0.3, (0, 0, 0.65), 'concrete', seg=12)
# tapering shaft in five sections
H = 20.0
secs = [(0.8, 5.0, 1.5, 1.42, 'brick'), (5.8, 5.0, 1.42, 1.34, 'brick_dark'), (10.8, 4.5, 1.34, 1.27, 'brick'), (15.3, 4.0, 1.27, 1.2, 'brick_dark')]
for z0, h, r0, r1, c in secs:
    m.cyl(r0, h, (0, 0, z0 + h / 2), c, seg=12, r2=r1)
# rust bands and steel hoops
for z in (3.0, 7.5, 12.0, 16.0):
    m.cyl(1.52 - z * 0.015, 0.35, (0, 0, z), 'rust', seg=12)
for z in (1.5, 5.8, 10.8, 15.3, 18.5):
    m.cyl(1.46 - z * 0.014, 0.12, (0, 0, z), 'steel_dark', seg=12)
# streaks of rust/soot running down
for i in range(12):
    a = r.uniform(0, 6.283); z = r.uniform(4, 18)
    rr = 1.5 - z * 0.015 + 0.02
    m.box((0.18, 0.04, r.uniform(1.2, 3)), (rr * math.cos(a), rr * math.sin(a), z), r.choice(['rust_dark', 'black', 'rust']), rot=(0, 0, math.degrees(a) + 90))
# rim, soot top
m.cyl(1.38, 0.35, (0, 0, 19.5), 'steel_dark', seg=12, r2=1.5)
m.cyl(1.5, 0.15, (0, 0, 19.775), 'rust_dark', seg=12)
m.cyl(1.3, 0.1, (0, 0, 19.95), 'black', seg=12)
for i in range(6):
    a = i * 60 + 10
    m.box((0.5, 0.12, 0.3), (1.45 * math.cos(math.radians(a)), 1.45 * math.sin(math.radians(a)), 19.95), 'black', rot=(0, 0, a + 90))
# ladder on -Y with cage hoops
yl = -1.5
for sx in (-1, 1):
    m.box((0.06, 0.08, 19.2), (sx * 0.25, yl - 0.12, 10.0), 'steel_dark')
for i in range(60):
    z = 0.8 + i * 0.3
    yy = -(math.sqrt(max(1.5 - z * 0.015, 1.1) ** 2 - 0.0)) - 0.12
    m.box((0.5, 0.05, 0.05), (0, yy + 0.0, z), 'rust')
for z in (1.5, 4.5, 7.5, 10.5, 13.5, 16.5):
    m.box((0.06, 0.55, 0.06), (0.0, yl - 0.4, z), 'steel') if False else None
for z in (6.0, 9.0, 12.0, 15.0, 18.0):
    for sx in (-1, 1):
        m.box((0.05, 0.5, 0.05), (sx * 0.3, yl - 0.35, z), 'steel_dark')
    m.box((0.65, 0.05, 0.05), (0, yl - 0.6, z), 'steel_dark')
for z in (2.5, 7.0, 13.0, 18.0):
    for sx in (-1, 1):
        m.box((0.06, 0.2, 0.06), (sx * 0.25, yl + 0.0, z), 'steel')
# lightning rod / platform bracket at the top
m.box((0.08, 0.08, 0.6), (-1.0, 0.0, 19.9), 'steel') if False else None
# access hatch at base
m.box((0.6, 0.12, 0.9), (1.0, -1.15, 1.1), 'gunmetal', rot=(0, 0, 40))
finish(m, bounds={'x': (-2.0, 2.0), 'y': (-2.2, 2.0), 'z': (0, 20.1)})
