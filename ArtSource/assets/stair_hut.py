import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('stair_hut', seed=61)
# side walls: profile in (y, z); low (1.1) in the open section, full height (2.5) under the roof
prof = [(-7.0, 0), (1.0, 0), (1.0, 2.5), (-2.0, 2.5), (-2.9, 1.1), (-7.0, 1.1)]
for s in (-1, 1):
    m.poly(prof, 0.8, (0, 0, 0), 'concrete', rot=(0, 0, 90)) if False else None
    m.poly(prof, 0.8, (s * 1.8, 0, 0), 'concrete', rot=(0, 0, 90))
    # outer weathering and stains
    # inner face hazard stripe band along the low wall top (flush into the wall)
    # railing on top of the open section
    for y in (-6.9, -5.4, -3.9, -3.0):
        m.box((0.06, 0.06, 0.55), (s * 1.8, y, 1.38), 'steel_dark')
    m.box((0.06, 3.96, 0.05), (s * 1.8, -4.95, 1.64), 'hazard_yellow')
    m.box((0.06, 3.96, 0.04), (s * 1.8, -4.95, 1.42), 'steel_dark')
    # curb cap
    m.box((0.8, 4.1, 0.05), (s * 1.8, -4.95, 1.12), 'concrete_light')
# roof slab with overhang at the front
m.box((4.4, 3.0, 0.6), (0, -0.5, 2.8), 'concrete_light', bevel=0.02)
m.box((4.2, 0.1, 0.35), (0, -2.0, 2.25), 'concrete_dark') if False else None
stripes(m, -2.0, 2.0, -2.0, 2.58, 3.02, 0.04)
m.box((4.4, 0.04, 0.05), (0, -1.98, 3.08), 'steel_dark') if False else None
# roof parapet and vent box
m.box((4.4, 0.2, 0.0001 + 0.0), (0, 0.9, 3.1), 'concrete') if False else None
# back wall
m.box((2.8, 0.3, 2.5), (0, 0.85, 1.25), 'concrete')
m.box((1.7, 0.02, 1.9), (0, 0.71, 1.15), 'gunmetal') if False else None
for s in (-1, 1): m.box((0.04, 0.5, 2.4), (s * 1.405, 0.85, 1.25), 'concrete_dark') if False else None
# hazard stripes on the entrance faces of the walls (front end at y=-7)
for s in (-1, 1):
    stripes(m, s * 1.8 - 0.28, s * 1.8 + 0.28, -6.97, 0.1, 1.0, 0.04, w=0.2)
# warning sign on slab front
m.box((0.8, 0.04, 0.4), (0, -2.02, 2.8), 'hazard_yellow') if False else None
# lamp over the corridor? no: corridor must stay empty. Put lamps on the walls inside (embedded)
for y in (-1.0, 0.2):
    m.box((0.06, 0.2, 0.3), (-1.4 + 0.0, y, 2.0), 'lamp_warm') if False else None
# base slab beyond wall
for s in (-1, 1):
    m.box((0.9, 8.1, 0.12), (s * 1.8, -3.0, 0.06), 'concrete_dark', ) if False else None
finish(m, bounds={'x': (-2.2, 2.2), 'y': (-7.0, 1.0), 'z': (0, 3.1)})
