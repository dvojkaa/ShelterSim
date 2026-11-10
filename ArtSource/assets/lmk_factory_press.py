import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('lmk_factory_press', seed=1110)
m.box((3.0, 2.0, 0.3), (0, 0, 0.15), 'concrete_dark', bevel=0.02)           # base
m.box((2.6, 1.6, 0.5), (0, 0, 0.55), 'gunmetal', bevel=0.03)                # bolster
m.box((1.6, 1.0, 0.1), (0, 0, 0.85), 'steel_dark')                          # die plate
for sx in (-1, 1):
    m.box((0.45, 0.8, 2.2), (sx * 1.2, 0.35, 1.9), 'mustard_dark', bevel=0.03)   # columns
    m.box((0.5, 0.05, 1.8), (sx * 1.2, -0.07, 1.8), 'rust')                      # worn plate
m.box((2.95, 0.9, 0.7), (0, 0.35, 2.65), 'mustard', bevel=0.04)             # crown
m.box((2.6, 0.4, 0.6), (0, 0.4, 0.55), 'mustard_dark') if False else None
m.cyl(0.35, 0.8, (0, 0.2, 2.3), 'steel_dark', seg=10)                        # cylinder
m.cyl(0.42, 0.12, (0, 0.2, 2.0), 'gunmetal', seg=10)
m.cyl(0.12, 0.9, (0, 0.2, 1.5), 'chrome', seg=8)                             # piston rod
m.box((1.3, 0.8, 0.6), (0, 0.2, 1.1), 'steel', bevel=0.03)                  # ram head
m.box((1.4, 0.82, 0.08), (0, 0.2, 0.82), 'rust_dark')
# stained slab on the bolster
m.box((0.8, 0.5, 0.12), (0, 0.2, 0.96), 'rust', rot=(0, 0, 12))
# guard rails at the front
for sx in (-1.3, -0.4, 0.4, 1.3):
    m.box((0.06, 0.06, 1.0), (sx, -0.9, 0.8), 'steel_dark')
m.box((2.7, 0.06, 0.06), (0, -0.9, 1.25), 'hazard_yellow')
m.box((2.7, 0.06, 0.06), (0, -0.9, 0.85), 'hazard_yellow')
m.box((2.7, 0.04, 0.06), (0, -0.9, 0.5), 'steel')
# hazard stripes on base and crown
for i in range(10):
    c = 'hazard_yellow' if i % 2 == 0 else 'black'
    m.box((0.2, 0.025, 0.2), (-1.3 + i * 0.29, -1.0, 0.16), c, rot=(0, 20, 0))
for i in range(6):
    c = 'hazard_yellow' if i % 2 == 0 else 'black'
    m.box((0.2, 0.025, 0.2), (-0.8 + i * 0.32, -0.12, 2.52), c, rot=(0, 25, 0))
# control box with lamps + cable
m.box((0.45, 0.25, 0.7), (-1.15, -0.82, 1.55 - 0.0), 'gunmetal') if False else None
m.box((0.4, 0.22, 0.6), (-1.1, -0.55, 0.45 + 0.9), 'army_green_dark', bevel=0.02)
m.cyl(0.04, 0.03, (-1.18, -0.67, 1.65), 'red_bright', rot=(90, 0, 0), seg=8)
m.cyl(0.04, 0.03, (-1.02, -0.67, 1.65), 'glass_dirty', rot=(90, 0, 0), seg=8)
m.box((0.2, 0.02, 0.15), (-1.1, -0.67, 1.45), 'paint_white')
m.tube([(-1.1, -0.55, 1.2), (-1.4, -0.7, 0.9), (-1.35, -0.8, 0.32), (-0.9, -0.95, 0.32)], 0.025, 'rubber', seg=5)
# oil drum, oil puddle
m.cyl(0.24, 0.5, (1.1, -0.65, 0.55), 'rust_dark', seg=8) if False else None
finish(m, bounds={'x': (-1.5, 1.5), 'y': (-1.05, 1.0), 'z': (0, 3.0)})
