import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_boss_gate', seed=1401)
r = random.Random(1401)
# side posts + header housing
for sx in (-1, 1):
    m.box((0.3, 0.5, 3.0), (sx * 1.55, -0.1, 1.5), 'steel_dark', bevel=0.02)
    m.box((0.4, 0.6, 0.15), (sx * 1.55, -0.1, 0.075), 'concrete_dark')
    m.box((0.12, 0.1, 2.7), (sx * 1.35, -0.3, 1.4), 'hazard_yellow') if False else None
m.box((3.4, 0.55, 0.45), (0, -0.12, 2.75), 'steel_dark', bevel=0.02)   # roll housing
m.cyl(0.2, 3.0, (0, -0.12, 2.75), 'gunmetal', rot=(0, 90, 0), seg=8) if False else None
m.box((3.0, 0.5, 0.08), (0, -0.14, 2.97), 'rust_dark')
# slats of the closed rolling gate
for i in range(12):
    z = 0.12 + i * 0.2
    m.box((2.8, 0.14, 0.17), (0, 0.0, z), 'steel' if i % 3 else 'steel_dark', bevel=0.015)
    m.box((2.8, 0.05, 0.03), (0, -0.09, z + 0.07), 'steel_dark')
m.box((2.82, 0.2, 0.12), (0, -0.04, 2.5), 'steel_dark', bevel=0.01)      # bottom rail up at top slat
m.box((2.9, 0.22, 0.2), (0, -0.05, 0.11), 'hazard_yellow')               # bottom bar
for k in range(10):
    m.box((0.14, 0.23, 0.21), (-1.35 + k * 0.3, -0.05, 0.11), 'black', rot=(0, 0, 0)) if k % 2 else None
# central lock + handle
m.box((0.5, 0.1, 0.3), (0, -0.12, 1.0), 'gunmetal')
m.cyl(0.09, 0.1, (0, -0.2, 1.0), 'rust', rot=(90, 0, 0), seg=8)
m.box((0.12, 0.08, 0.5), (0.9, -0.12, 1.1), 'steel_dark')
# warning signs: hazard triangle + red plate on each post face
for sx in (-1, 1):
    m.box((0.36, 0.04, 0.36), (sx * 1.55, -0.38, 1.9), 'hazard_yellow', rot=(0, 45, 0))
    m.box((0.1, 0.05, 0.28), (sx * 1.55, -0.405, 1.93), 'black')
    m.box((0.1, 0.05, 0.08), (sx * 1.55, -0.405, 1.7), 'black')
    m.box((0.45, 0.04, 0.3), (sx * 1.55, -0.38, 1.2), 'warning_red')
    m.box((0.3, 0.05, 0.05), (sx * 1.55, -0.405, 1.2), 'fabric_white')
m.box((1.6, 0.05, 0.28), (0, -0.4, 2.75), 'warning_red')
for k in range(5):
    m.box((0.2, 0.06, 0.15), (-0.6 + k * 0.3, -0.43, 2.75), 'fabric_white')
# dents and rust
for i in range(10):
    m.box((r.uniform(0.2, 0.7), 0.03, r.uniform(0.15, 0.5)), (r.uniform(-1.2, 1.2), -0.09, r.uniform(0.4, 2.4)), r.choice(['rust', 'rust_dark']))
for sx in (-1, 1):
    m.box((0.06, 0.6, 2.4), (sx * 1.42, 0, 1.2), 'gunmetal')   # guide rails
finish(m, bounds={'x': (-1.85, 1.85), 'y': (-0.5, 0.3), 'z': (0, 3.1)})
