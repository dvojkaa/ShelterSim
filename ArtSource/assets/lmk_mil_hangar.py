import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_mil_hangar', seed=1206)
r = random.Random(1206)
for sx in (-1, 1):
    x0 = sx * (1.7 + 6.3 / 2)
    m.box((6.3, 0.2, 3.2), (x0, -0.11, 1.6), 'steel', bevel=0.01)
    for i in range(21):   # corrugation ribs
        x = x0 - 3.0 + i * 0.3
        m.box((0.14, 0.08, 3.1), (x, -0.24, 1.6), r.choice(['steel', 'steel_dark', 'steel']), bevel=0.01)
    m.box((6.3, 0.3, 0.3), (x0, -0.15, 3.35), 'army_green_dark')          # top fascia
    m.box((6.3, 0.3, 0.34), (x0, -0.15, 0.17), 'concrete_dark')            # plinth
    for k in range(3):
        m.box((r.uniform(0.5, 1.2), 0.04, r.uniform(0.8, 1.8)), (x0 + r.uniform(-2.5, 2.5), -0.3, r.uniform(0.8, 2.6)), r.choice(['rust', 'rust_dark']))
    # stencil blocks
    m.box((2.2, 0.05, 0.5), (x0 + sx * 0.5, -0.32, 2.0), 'fabric_white')
    for j in range(5):
        m.box((0.28, 0.06, 0.34), (x0 + sx * 0.5 - 0.8 + j * 0.4, -0.35, 2.0), 'army_green_dark')
    m.box((6.0, 0.05, 0.12), (x0, -0.32, 2.85), 'hazard_yellow')
    # buttress posts
    for px in (-2.8, 0.2, 2.8):
        m.box((0.3, 0.45, 3.3), (x0 + px, -0.4, 1.7), 'steel_dark', bevel=0.02)
# header above the gap
m.box((3.4, 0.25, 0.7), (0, -0.13, 3.15), 'steel_dark', bevel=0.02)
for i in range(11):
    m.box((0.14, 0.08, 0.6), (-1.5 + i * 0.3, -0.27, 3.15), 'steel')
m.box((3.6, 0.5, 0.18), (0, -0.4, 2.9), 'hazard_yellow')
for i in range(9):
    m.box((0.22, 0.52, 0.19), (-1.6 + i * 0.4, -0.4, 2.9), 'black', rot=(0, 0, 0)) if i % 2 else None
for sx in (-1, 1):
    m.box((0.3, 0.5, 2.8), (sx * 1.75, -0.26, 1.4), 'concrete_dark')                # jambs
    m.box((0.12, 0.4, 2.8), (sx * 1.68, -0.5, 1.4), 'hazard_yellow') if False else None
# roof lip with a slight awning
m.box((16.0, 1.2, 0.12), (0, -0.6, 3.5 - 0.06), 'army_green_dark', rot=(-4, 0, 0)) if False else None
m.box((15.9, 0.8, 0.08), (0, -0.7, 3.38), 'army_green_dark', rot=(-6, 0, 0))
# a few crates + barrels at the base
for x in (-6.5, 5.2):
    m.box((0.8, 0.7, 0.8), (x, -0.8, 0.4), 'army_green', bevel=0.02)
m.cyl(0.3, 0.9, (-5.5, -0.8, 0.45), 'rust', seg=8)
finish(m, bounds={'x': (-8.0, 8.0), 'y': (-1.2, 0.0), 'z': (0, 3.5)})
