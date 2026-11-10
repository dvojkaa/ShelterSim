import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('bld_doorframe_interior', seed=91)
# Clean painted frame for a 1.8 x 2.2 opening in a 0.2 m wall. Opening |x|<0.9, z<2.2 stays empty.
for s in (-1, 1):
    # posts: stand in the wall thickness (|y|<0.1), jamb faces at |x|=0.9
    m.box((0.15, 0.2, 2.2), (s * 0.975, 0, 1.1), 'plaster', bevel=0.004)
    # architrave on both faces, proud of the wall by 0.02
    for f in (-1, 1):
        m.box((0.15, 0.03, 2.2), (s * 0.975, f * 0.105, 1.1), 'paint_white', bevel=0.004)
        m.box((0.15, 0.012, 0.12), (s * 0.975, f * 0.1195, 0.07), 'concrete_dark')   # scuffed kick
        m.box((0.04, 0.012, 0.5), (s * 0.96, f * 0.1195, 1.2 + s * 0.15), 'plaster_dirty')  # grime streak
# lintel
m.box((2.1, 0.2, 0.2), (0, 0, 2.3), 'plaster', bevel=0.004)
for f in (-1, 1):
    m.box((2.1, 0.03, 0.2), (0, f * 0.105, 2.3), 'paint_white', bevel=0.004)
    m.box((0.6, 0.012, 0.05), (0.2, f * 0.1195, 2.33), 'plaster_dirty')
    m.box((0.35, 0.012, 0.04), (-0.55, f * 0.1195, 2.26), 'concrete')
    m.box((0.05, 0.012, 0.3), (0.99, f * 0.1195, 0.9), 'concrete_light')  # chipped paint
    m.box((0.1, 0.012, 0.08), (-0.99, f * 0.1195, 1.5), 'wood_grey')
# latch strike plate on the right jamb face (inside the 0.2 wall thickness, flush with jamb)
m.box((0.01, 0.06, 0.12), (0.905, 0, 1.0), 'steel_dark')
finish(m, bounds={'x': (-1.05, 1.05), 'y': (-0.12, 0.12), 'z': (0, 2.4)})
