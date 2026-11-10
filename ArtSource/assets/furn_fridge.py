import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('furn_fridge', seed=102)
FY = -0.28   # body front; door -0.31..-0.28; handle reaches -0.35
# feet + base grille
for x in (-0.3, 0.3):
    for y in (-0.22, 0.25):
        m.box((0.07, 0.07, 0.06), (x, y, 0.03), 'gunmetal')
m.box((0.7, 0.62, 0.06), (0, 0.02, 0.09), 'steel_dark')
# rounded body: big bevel, slightly tapered top
m.box((0.73, 0.62, 1.62), (0, 0.04, 0.93), 'paint_white', bevel=0.07)
m.box((0.72, 0.5, 0.05), (0, 0.06, 1.77), 'paint_white', bevel=0.02)
# back compressor hump / coil
m.box((0.5, 0.1, 0.4), (0, 0.3, 1.0), 'steel_dark')
m.box((0.4, 0.04, 0.3), (0, 0.33, 1.0), 'gunmetal')
# rust at the base and side
m.box((0.76, 0.64, 0.2), (0, 0.035, 0.2), 'rust', bevel=0.05) if False else None
for sx in (-1, 1):
    m.box((0.01, 0.4, 0.22), (sx * 0.3675, 0.07, 0.25), 'rust')
    m.box((0.01, 0.2, 0.12), (sx * 0.3675, -0.1, 0.38), 'rust_dark')
    m.box((0.01, 0.14, 0.4), (sx * 0.3675, 0.2, 1.3), 'fabric_stain')
# --- Door: pivot at hinge x=-0.37, spans whole front
p = m.part('Door', pivot=(-0.37, -0.295, 0.9))
p.box((0.74, 0.04, 1.66), (0.0, -0.29, 0.93), 'paint_white', bevel=0.02)
# rounded look: shaded bevel frame on top and sides
p.box((0.62, 0.012, 0.5), (0.0, -0.3075, 1.4), 'paint_white', bevel=0.01, taper=(0.98, 1.0))
p.box((0.74, 0.008, 0.02), (0, -0.311, 1.18), 'black')                        # freezer split
p.box((0.74, 0.008, 0.03), (0, -0.311, 0.12), 'steel_dark')                   # kick grille
p.box((0.5, 0.01, 0.1), (0.03, -0.31, 0.18), 'rust')                          # rust band
p.box((0.2, 0.01, 0.07), (0.12, -0.31, 0.3), 'rust_dark')
p.box((0.3, 0.01, 0.05), (-0.15, -0.31, 0.06), 'rust_light') if False else None
# handles (chrome vertical bars with mounts)
p.box((0.035, 0.03, 0.26), (0.27, -0.335, 1.38), 'chrome', bevel=0.006)
p.box((0.035, 0.03, 0.4), (0.27, -0.335, 0.92), 'chrome', bevel=0.006)
for z in (1.28, 1.48, 0.76, 1.08):
    p.box((0.02, 0.03, 0.025), (0.27, -0.322, z), 'steel_dark')
# emblem, grime
p.box((0.18, 0.006, 0.04), (-0.05, -0.311, 1.66), 'chrome')
p.box((0.12, 0.006, 0.28), (-0.28, -0.311, 0.62), 'fabric_stain')
finish(m, bounds={'x': (-0.375, 0.375), 'y': (-0.35, 0.35), 'z': (0, 1.8)})
