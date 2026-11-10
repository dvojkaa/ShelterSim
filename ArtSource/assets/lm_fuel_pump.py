import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('lm_fuel_pump', seed=302)
# island base with kerb
m.box((0.9, 0.7, 0.14), (0, 0, 0.07), 'concrete', bevel=0.02)
m.box((0.5, 0.5, 0.03), (0, 0, 0.155), 'concrete_dark')
# body, two-sided
m.box((0.5, 0.36, 1.0), (0, 0, 0.67), 'rust', bevel=0.03)
m.box((0.52, 0.38, 0.08), (0, 0, 0.2), 'steel_dark')
m.box((0.56, 0.4, 0.1), (0, 0, 1.2), 'steel_dark', bevel=0.02)
m.box((0.5, 0.36, 0.28), (0, 0, 1.39), 'rust_dark', bevel=0.03, taper=(0.9, 0.9))   # head
m.box((0.54, 0.38, 0.05), (0, 0, 1.56), 'gunmetal', bevel=0.01)
m.cyl(0.07, 0.1, (0, 0, 1.62), 'rust_dark', seg=8, r2=0.04)
for s in (-1, 1):
    y = s * 0.19
    # display window (blank) and dial
    m.box((0.34, 0.02, 0.2), (0, y, 1.38), 'gunmetal')
    m.box((0.3, 0.015, 0.15), (0, y + s * 0.012, 1.38), 'glass_dirty')
    for i in range(4):
        m.box((0.06, 0.012, 0.09), (-0.11 + i * 0.075, y + s * 0.022, 1.38), 'black')
    # faded hazard stripes around the waist
    for i in range(5):
        m.box((0.08, 0.012, 0.12), (-0.18 + i * 0.09, y + s * 0.004, 0.55), 'hazard_yellow' if i % 2 == 0 else 'black', rot=(0, 25, 0))
    # blank label plate
    m.box((0.26, 0.012, 0.14), (0, y + s * 0.004, 0.9), 'paint_white')
    # selector buttons
    for i in range(3):
        m.cyl(0.025, 0.02, (-0.1 + i * 0.1, y + s * 0.02, 1.1), 'chrome' if i != 1 else 'warning_red', rot=(90, 0, 0), seg=6)
    # nozzle holster and hose side
sx = 0.29
m.box((0.08, 0.2, 0.16), (sx, -0.0, 1.05), 'steel_dark')                  # holster
m.box((0.07, 0.04, 0.22), (sx + 0.02, -0.07, 1.14), 'gunmetal')
# nozzle hanging
m.box((0.05, 0.05, 0.18), (sx + 0.05, 0.05, 1.05), 'black', bevel=0.01)
m.cyl(0.015, 0.14, (sx + 0.05, 0.05, 0.93), 'chrome', seg=6)
m.box((0.04, 0.09, 0.03), (sx + 0.05, 0.02, 1.0), 'rust_light')
# drooping hose from head to nozzle
m.tube([(0.26, 0.0, 1.3), (0.4, 0.0, 1.3), (0.42, 0.05, 1.0), (0.4, 0.1, 0.55), (0.3, 0.1, 0.4), (0.38, 0.05, 0.9)], 0.025, 'rubber', seg=6) if False else None
m.tube([(0.26, 0.1, 1.28), (0.4, 0.14, 1.2), (0.45, 0.2, 0.8), (0.4, 0.22, 0.45), (0.34, 0.18, 0.6), (0.36, 0.09, 1.0)], 0.022, 'rubber', seg=6)
# bolts base
for x in (-0.4, 0.4):
    for y in (-0.3, 0.3):
        m.cyl(0.025, 0.03, (x, y, 0.15), 'steel_dark', seg=6)
# rust drips / patches
m.box((0.18, 0.012, 0.25), (-0.12, -0.186, 0.8), 'rust_dark')
m.box((0.14, 0.012, 0.2), (0.1, 0.186, 0.35), 'rust_dark')
# frame the other flank: panel seam
m.box((0.012, 0.37, 0.7), (-0.26, 0, 0.67), 'steel_dark')
finish(m, bounds={'x': (-0.45, 0.5), 'y': (-0.35, 0.35), 'z': (0, 1.7)})
