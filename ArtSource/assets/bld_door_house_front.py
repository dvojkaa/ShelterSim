import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('bld_door_house_front', seed=201)
# Wooden panelled family-house front door leaf. Origin floor centre, x -0.9..0.9 (hinge -0.9), z 0..2.2, front -Y.
m.box((1.8, 0.06, 2.2), (0, 0.01, 1.1), 'wood_dark')                      # core slab
# stiles and rails (front frame)
m.box((0.2, 0.03, 2.2), (-0.8, -0.04, 1.1), 'paint_green')
m.box((0.2, 0.03, 2.2), (0.8, -0.04, 1.1), 'paint_green')
m.box((0.2, 0.03, 2.2), (0.0, -0.04, 1.1), 'paint_green')
for z, h in ((0.1, 0.2), (0.95, 0.12), (1.5, 0.12), (2.12, 0.16)):
    m.box((1.8, 0.026, h), (0, -0.04, z), 'paint_green')
# 6 raised panels: 2 columns x 3 rows
cols = (-0.4, 0.4)
rows = ((0.52, 0.62), (1.22, 0.4), (1.82, 0.4))
for ci, cx in enumerate(cols):
    for ri, (cz, ph) in enumerate(rows):
        if ri == 2 and ci == 1:
            continue
        col = 'paint_green' if (ci + ri) % 2 == 0 else 'wood_light'
        m.box((0.5, 0.025, ph), (cx, -0.056, cz), 'paint_green')
        m.box((0.36, 0.012, ph - 0.14), (cx, -0.07, cz), 'paint_green')
# glazed top light (top-right) with cracked glass
m.box((0.5, 0.05, 0.4), (0.4, -0.04, 1.82), 'wood_dark')
m.box((0.4, 0.02, 0.3), (0.4, -0.062, 1.82), 'glass_dirty')
m.box((0.025, 0.015, 0.34), (0.4, -0.072, 1.82), 'wood_dark')            # muntin
m.box((0.4, 0.015, 0.025), (0.4, -0.072, 1.82), 'wood_dark')
for (x, z, l, a) in ((0.3, 1.86, 0.22, 35), (0.5, 1.78, 0.2, -50), (0.46, 1.9, 0.14, 70)):
    m.box((l, 0.008, 0.01), (x, -0.075, z), 'black', rot=(0, a, 0))
# flaking paint: patches of bare wood and a second paint colour
for (x, z, sx, sz) in ((-0.78, 0.5, 0.12, 0.3), (-0.45, 0.22, 0.25, 0.1), (0.78, 0.35, 0.1, 0.4), (0.0, 1.35, 0.1, 0.2),
                       (-0.35, 1.2, 0.22, 0.08), (0.75, 1.6, 0.1, 0.18), (-0.1, 0.1, 0.12, 0.1), (0.4, 0.58, 0.3, 0.1)):
    m.box((sx, 0.004, sz), (x, -0.0565 if abs(x) in (0.78, 0.75, 0.0) else -0.083, z), 'wood_light')
for (x, z, sx, sz) in ((-0.8, 1.3, 0.1, 0.22), (0.78, 1.1, 0.08, 0.14), (0.0, 0.7, 0.08, 0.3), (-0.8, 1.95, 0.1, 0.2)):
    m.box((sx, 0.004, sz), (x, -0.0565, z), 'paint_white')
# scuffs and kick marks
m.box((0.5, 0.006, 0.07), (0.3, -0.0575, 0.08), 'concrete_dark')
m.box((0.2, 0.006, 0.04), (-0.4, -0.0575, 0.15), 'black')
# mail slot (blank steel flap) on centre rail
m.box((0.34, 0.03, 0.07), (0.0, -0.06, 0.95), 'steel_dark')
m.box((0.3, 0.015, 0.03), (0.0, -0.078, 0.95), 'gunmetal')
# lock plate, handle, key hole, knocker
m.box((0.1, 0.012, 0.3), (0.77, -0.062, 1.0), 'mustard_dark', bevel=0.005)
m.cyl(0.035, 0.05, (0.77, -0.085, 1.07), 'mustard', rot=(90, 0, 0), seg=8)
m.box((0.2, 0.025, 0.03), (0.7, -0.1, 1.07), 'mustard')
m.box((0.02, 0.015, 0.05), (0.77, -0.07, 0.92), 'black')
m.cyl(0.035, 0.02, (0.0, -0.075, 1.65), 'mustard_dark', rot=(90, 0, 0), seg=8)
m.box((0.05, 0.02, 0.2), (0.0, -0.092, 1.58), 'mustard', bevel=0.004)
# rusted hinges (hinge side), barrels visible
for z in (0.3, 1.1, 1.9):
    m.cyl(0.04, 0.22, (-0.9, -0.045, z), 'rust_dark', seg=6)
    m.box((0.3, 0.02, 0.1), (-0.76, -0.066, z), 'rust')
    m.box((0.05, 0.022, 0.03), (-0.9, -0.068, z + 0.06), 'rust_light')
# back side: plain planks/flat so it reads from inside
m.box((1.7, 0.01, 2.1), (0, 0.045, 1.1), 'wood')
m.box((0.12, 0.02, 0.1), (0.77, 0.06, 1.0), 'mustard_dark')
finish(m, bounds={'x': (-0.95, 0.95), 'y': (-0.12, 0.12), 'z': (0, 2.2)})
