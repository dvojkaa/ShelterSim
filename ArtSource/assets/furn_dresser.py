import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('furn_dresser', seed=101)
# carcass, plinth, top
m.box((1.2, 0.5, 0.08), (0, 0, 0.04), 'wood_dark', bevel=0.01)
m.box((1.16, 0.46, 0.74), (0, 0.01, 0.45), 'wood', bevel=0.01)
m.box((1.2, 0.5, 0.04), (0, 0, 0.84), 'wood_light', bevel=0.012)
m.box((0.3, 0.2, 0.012), (-0.38, -0.02, 0.862), 'wood_dark')   # water stain
for x in (-0.57, 0.57):
    m.box((0.05, 0.05, 0.1), (x, -0.2, 0.05), 'wood_dark')
# mirror stub at back
m.box((0.62, 0.04, 0.17), (0.1, 0.2, 0.911), 'wood_dark', bevel=0.008, rot=(-6, 0, 0))
m.box((0.54, 0.02, 0.11), (0.1, 0.18, 0.925), 'glass_dirty', rot=(-6, 0, 0))
m.box((0.1, 0.02, 0.06), (0.22, 0.175, 0.93), 'glass', rot=(-6, 0, 12))   # crack patch
# doily + clutter
m.box((0.3, 0.2, 0.01), (-0.35, 0.0, 0.865), 'fabric_white', rot=(0, 0, 8))
m.cyl(0.04, 0.1, (-0.4, 0.02, 0.92), 'glass_dirty', seg=6)
m.box((0.2, 0.01, 0.12), (0.45, -0.222, 0.4), 'wood_dark')   # grime
m.box((0.03, 0.01, 0.3), (-0.58, -0.222, 0.55), 'rust_dark')
# drawers
zs = [0.74, 0.52, 0.30]
for i, zc in enumerate(zs):
    d = m.part('Drawer_%d' % (i + 1), pivot=(0, -0.23, zc))
    off = -0.004 if i == 1 else 0.0       # middle drawer ajar
    col = ['wood_light', 'wood', 'wood_light'][i]
    d.box((1.08, 0.03, 0.19), (0, -0.215 + off, zc), col, bevel=0.008)
    d.box((0.9, 0.02, 0.01), (0, -0.232 + off, zc + 0.07), 'wood_dark')
    d.box((1.0, 0.34, 0.14), (0, -0.04 + off, zc - 0.02), 'wood_dark')
    for x in (-0.25, 0.25):
        d.cyl(0.02, 0.02, (x, -0.236 + off, zc), 'chrome', rot=(90, 0, 0), seg=6)
finish(m, bounds={'x': (-0.6, 0.6), 'y': (-0.25, 0.25), 'z': (0, 1.0)})
