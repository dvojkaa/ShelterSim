import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('furn_filing_cabinet', seed=322)
# carcass: y -0.325..0.325, x +-0.25
m.box((0.5, 0.6, 0.04), (0, 0.03, 0.02), 'black')                    # plinth
m.box((0.5, 0.65, 0.03), (0, 0, 1.285), 'steel_dark', bevel=0.008)   # top
m.box((0.03, 0.62, 1.2), (-0.235, 0.01, 0.65), 'gunmetal')
m.box((0.03, 0.62, 1.2), (0.235, 0.01, 0.65), 'gunmetal')
m.box((0.5, 0.03, 1.2), (0, 0.31, 0.65), 'gunmetal')                 # back
# dents / rust
m.box((0.01, 0.2, 0.15), (-0.25, 0.0, 0.5), 'steel_dark')
m.box((0.01, 0.15, 0.3), (0.25, 0.1, 0.9), 'rust')
m.box((0.01, 0.1, 0.1), (0.25, -0.1, 0.35), 'rust_dark')
m.box((0.2, 0.2, 0.01), (0.05, 0.1, 1.296), 'rust_dark')
# static lower drawers (3,4) fronts
fz = [1.125, 0.865, 0.605, 0.345]
for i in (2, 3):
    z = fz[i]
    m.box((0.46, 0.03, 0.24), (0, -0.31, z), 'steel', bevel=0.008)
    m.box((0.3, 0.02, 0.05), (0, -0.33, z + 0.03), 'steel_dark')       # handle
    m.box((0.1, 0.015, 0.05), (0, -0.33, z - 0.07), 'plaster')         # label holder
    m.box((0.08, 0.018, 0.035), (0, -0.332, z - 0.07), 'paint_white')  # blank card
m.box((0.2, 0.03, 0.02), (0.08, -0.33, 0.43), 'rust_dark')
m.box((0.46, 0.03, 0.17), (0, -0.31, 0.12), 'steel_dark', bevel=0.006)
# frame gaps behind drawers (dark body inside)
m.box((0.44, 0.02, 1.15), (0, 0.28, 0.65), 'black')
# Drawer_1 / Drawer_2
for i in (0, 1):
    z = fz[i]
    pv = (0, -0.325, z)
    d = m.part(f'Drawer_{i+1}', pivot=pv)
    d.box((0.46, 0.03, 0.24), pv, 'steel' if i else 'steel_light', bevel=0.008) if False else d.box((0.46, 0.03, 0.24), pv, 'steel', bevel=0.008)
    d.box((0.3, 0.02, 0.05), (0, -0.34, z + 0.03), 'steel_dark')
    d.box((0.1, 0.015, 0.05), (0, -0.34, z - 0.07), 'plaster')
    d.box((0.08, 0.018, 0.035), (0, -0.347, z - 0.07), 'paint_white')
    d.box((0.42, 0.55, 0.01), (0, -0.05, z - 0.11), 'steel_dark')
    d.box((0.01, 0.55, 0.2), (-0.205, -0.05, z - 0.02), 'steel_dark')
    d.box((0.01, 0.55, 0.2), (0.205, -0.05, z - 0.02), 'steel_dark')
    d.box((0.42, 0.01, 0.2), (0, 0.22, z - 0.02), 'steel_dark')
    for k in range(3):
        d.box((0.3, 0.012, 0.18), (0, -0.2 + k * 0.12, z - 0.02), ['paint_white', 'plaster', 'leather'][k])
d_dent = m  # dent on drawer fronts (static overlay)
m.box((0.14, 0.01, 0.1), (-0.1, -0.343, 0.9), 'steel_dark', rot=(0, 0, 0))
finish(m, bounds={'x': (-0.26, 0.26), 'y': (-0.36, 0.335), 'z': (0, 1.31)})
