import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('bld_door_shop_glass', seed=203)
# Shopfront glass door leaf: steel frame, big pane. Origin floor centre, hinge x=-0.9, front -Y.
fw = 0.14
m.box((fw, 0.07, 2.2), (-0.9 + fw / 2, 0, 1.1), 'steel', bevel=0.006)
m.box((fw, 0.07, 2.2), (0.9 - fw / 2, 0, 1.1), 'steel', bevel=0.006)
m.box((1.8, 0.066, 0.14), (0, 0, 2.13), 'steel')
m.box((1.8, 0.066, 0.3), (0, 0, 0.15), 'steel_dark')           # bottom rail / kick plate
m.box((1.4, 0.02, 0.2), (0, -0.04, 0.15), 'steel', bevel=0.008)
m.box((1.4, 0.012, 0.02), (0, -0.054, 0.1), 'rust')
# glass pane: split into a missing-corner shape (polygon in XZ)
x0, x1, z0, z1 = -0.76, 0.76, 0.3, 2.06
m.poly([(x0, z0), (x1, z0), (x1, z1 - 0.45), (x1 - 0.3, z1 - 0.2), (x1 - 0.55, z1), (x0, z1)], 0.02, (0, 0, 0), 'glass')
# cracks (thin dark boxes) radiating from the broken corner
cx, cz = x1 - 0.3, z1 - 0.2
for (L, a, dx, dz) in ((0.7, 40, -0.2, -0.25), (0.5, -20, -0.35, -0.05), (0.6, 75, 0.0, -0.4), (0.35, 10, -0.5, -0.35), (0.4, 60, -0.15, -0.8)):
    m.box((L, 0.006, 0.008), (cx + dx, -0.0125, cz + dz), 'black', rot=(0, a, 0))
m.box((0.5, 0.006, 0.008), (-0.3, -0.0125, 0.9), 'gunmetal', rot=(0, -35, 0))
# grime on glass
m.box((0.5, 0.004, 0.3), (-0.4, -0.0115, 0.6), 'glass_dirty')
m.box((0.3, 0.004, 0.5), (0.35, -0.0115, 0.55), 'glass_dirty')
# glazing bead around
m.box((1.52, 0.03, 0.03), (0, -0.04, 0.3), 'gunmetal')
m.box((0.03, 0.03, 1.76), (-0.76, -0.04, 1.18), 'gunmetal')
m.box((0.03, 0.03, 1.76), (0.76, -0.04, 1.18), 'gunmetal')
m.box((0.8, 0.03, 0.03), (-0.36, -0.04, 2.06), 'gunmetal')
# long vertical pull handle bar
for z in (0.8, 1.55):
    m.box((0.05, 0.07, 0.05), (0.72, -0.065, z), 'chrome')
m.cyl(0.025, 0.95, (0.72, -0.09, 1.18), 'chrome', seg=6)
# blank sticker area
m.box((0.34, 0.006, 0.22), (-0.35, -0.0125, 1.3), 'fabric_white')
m.box((0.34, 0.004, 0.02), (-0.35, -0.0155, 1.2), 'plaster_dirty')
# rust streaks and scuffs on frame
for (x, z, sx, sz) in ((-0.84, 0.6, 0.04, 0.5), (0.85, 1.7, 0.03, 0.35), (0.0, 2.13, 0.4, 0.05)):
    m.box((sx, 0.01, sz), (x, -0.038, z), 'rust')
# hinges on hinge side
for z in (0.35, 1.1, 1.85):
    m.cyl(0.04, 0.2, (-0.9, -0.05, z), 'gunmetal', seg=6)
    m.box((0.18, 0.02, 0.08), (-0.82, -0.045, z), 'steel_dark')
# back side lock rod
m.box((0.12, 0.03, 0.12), (0.72, 0.05, 1.0), 'steel_dark')
finish(m, bounds={'x': (-0.95, 0.95), 'y': (-0.12, 0.12), 'z': (0, 2.2)})
