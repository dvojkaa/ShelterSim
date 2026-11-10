import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import stripes
m = Model('prop_room_bulkhead', seed=73)
F = -0.21   # front face plane
# posts |x| 0.9..1.2, full height; chamfered concrete
for s in (-1, 1):
    m.box((0.29, 0.42, 2.4), (s * 1.05, 0, 1.2), 'concrete', bevel=0.04)
    m.box((0.29, 0.44, 0.1), (s * 1.05, 0, 0.05), 'concrete_dark', bevel=0.015)       # base plinth
    # steel angle on the inner edge of the post (outside the opening: x >= 0.9)
    m.box((0.03, 0.42, 2.26), (s * 0.915, 0, 1.15), 'steel_dark')
    # hazard stripes low on the front and back of each post
    stripes(m, s * 1.05 - 0.11, s * 1.05 + 0.11, F - 0.006, 0.12, 0.5, 0.012, w=0.1)
    stripes(m, s * 1.05 - 0.11, s * 1.05 + 0.11, -F + 0.006, 0.12, 0.5, 0.012, w=0.1)
    # bolts up the front face
    for z in (0.75, 1.2, 1.65, 2.1):
        m.cyl(0.028, 0.025, (s * 1.05, F - 0.012, z), 'steel', rot=(90, 0, 0), seg=6)
        m.cyl(0.028, 0.025, (s * 1.05, -F + 0.012, z), 'steel', rot=(90, 0, 0), seg=6)
    # anchor plates / rust stain
m.box((0.14, 0.01, 0.22), (-1.1, F - 0.004, 1.4), 'rust_dark')
m.box((0.1, 0.01, 0.16), (1.0, -F + 0.004, 0.85), 'rust_dark')
# lintel z 2.3..2.8 across the whole width
m.box((2.39, 0.42, 0.5), (0, 0, 2.55), 'concrete', bevel=0.04)
# front steel flange along the lintel's lower edge, hazard band on top half
stripes(m, -1.0, 1.0, F - 0.006, 2.62, 2.76, 0.012, w=0.14)
stripes(m, -1.0, 1.0, -F + 0.006, 2.62, 2.76, 0.012, w=0.14)
m.box((2.3, 0.03, 0.04), (0, F - 0.012, 2.36), 'steel_dark')
for x in (-1.0, -0.5, 0.5, 1.0):
    m.cyl(0.028, 0.025, (x, F - 0.025, 2.36), 'steel', rot=(90, 0, 0), seg=6)
# small blank sign plate centred on the lintel front
m.box((0.5, 0.02, 0.2), (0, F - 0.01, 2.48), 'steel_dark')
m.box((0.44, 0.012, 0.14), (0, F - 0.02, 2.48), 'paint_white')
for x in (-0.22, 0.22):
    for z in (2.42, 2.54):
        m.cyl(0.012, 0.012, (x, F - 0.028, z), 'steel', rot=(90, 0, 0), seg=5)
# corner chamfer wedges where lintel meets posts (outside the opening)
finish(m, bounds={'x': (-1.2, 1.2), 'y': (-0.25, 0.25), 'z': (0, 2.8)})
