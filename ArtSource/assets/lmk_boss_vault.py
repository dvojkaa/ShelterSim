import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_boss_vault', seed=1403)
r = random.Random(1403)
m.box((1.8, 0.3, 2.4), (0, -0.15, 1.2), 'gunmetal', bevel=0.03)          # frame slab
m.box((1.7, 0.08, 0.15), (0, -0.34, 0.075), 'concrete_dark')
# round vault door
m.cyl(0.78, 0.2, (0, -0.4, 1.2), 'steel_dark', rot=(90, 0, 0), seg=16)
m.cyl(0.68, 0.1, (0, -0.52, 1.2), 'steel', rot=(90, 0, 0), seg=16)
m.cyl(0.62, 0.05, (0, -0.58, 1.2), 'army_green', rot=(90, 0, 0), seg=16)
# locking bolts around the ring
for k in range(12):
    a = math.radians(k * 30)
    m.cyl(0.05, 0.12, (0.73 * math.cos(a), -0.52, 1.2 + 0.73 * math.sin(a)), 'chrome', rot=(90, 0, 0), seg=6)
# wheel
m.cyl(0.14, 0.12, (0, -0.65, 1.2), 'rust_dark', rot=(90, 0, 0), seg=8)
for k in range(3):
    m.box((0.62, 0.07, 0.07), (0, -0.7, 1.2), 'steel_dark', rot=(0, 60 * k, 0))
m.cyl(0.33, 0.06, (0, -0.68, 1.2), 'steel_dark', rot=(90, 0, 0), seg=12, r2=0.33) if False else None
# stencilled number 07 on the upper frame
m.box((0.5, 0.03, 0.34), (0, -0.315, 2.12), 'fabric_stain') if False else None
m.box((0.2, 0.03, 0.3), (-0.17, -0.31, 2.1), 'fabric_white')
m.box((0.08, 0.04, 0.18), (-0.17, -0.315, 2.1), 'gunmetal')
m.box((0.2, 0.03, 0.3), (0.17, -0.31, 2.1), 'fabric_white')
m.box((0.12, 0.04, 0.2), (0.13, -0.315, 2.04), 'gunmetal')
# hazard strips and signs
m.box((1.7, 0.03, 0.1), (0, -0.31, 0.3), 'hazard_yellow')
for k in range(7):
    m.box((0.12, 0.04, 0.11), (-0.7 + k * 0.23, -0.315, 0.3), 'black', rot=(0, 20, 0))
m.box((0.28, 0.03, 0.28), (-0.7, -0.31, 1.95), 'red_bright') if False else None
m.box((0.26, 0.03, 0.2), (0.68, -0.31, 2.1), 'warning_red')
m.box((0.2, 0.04, 0.05), (0.68, -0.32, 2.1), 'fabric_white')
# hinges and keypad
for z in (0.6, 1.8):
    m.box((0.18, 0.2, 0.25), (-0.86, -0.44, z), 'gunmetal')
m.box((0.2, 0.08, 0.28), (0.82, -0.36, 1.2), 'black')
m.box((0.12, 0.03, 0.06), (0.82, -0.41, 1.28), 'lamp_warm')
# rust stains
for i in range(8):
    m.box((r.uniform(0.08, 0.3), 0.02, r.uniform(0.3, 0.9)), (r.uniform(-0.8, 0.8), -0.305, r.uniform(0.5, 2.0)), r.choice(['rust', 'rust_dark']))
finish(m, bounds={'x': (-0.95, 0.95), 'y': (-0.8, 0.0), 'z': (0, 2.5)})
