import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('bld_door_interior_wood', seed=301)
P = 'paint_white'   # cream paint
# leaf: stiles and rails full thickness (y -0.03..0.03), recessed panels
m.box((0.14, 0.06, 2.2), (-0.83, 0, 1.1), P)
m.box((0.14, 0.06, 2.2), (0.83, 0, 1.1), P)
m.box((1.52, 0.06, 0.2), (0, 0, 2.1), P)
m.box((1.52, 0.06, 0.3), (0, 0, 0.15), P)
m.box((1.52, 0.06, 0.16), (0, 0, 1.08), P)      # lock rail
for z0, h, c in ((1.62, 0.9, 'plaster'), (0.58, 0.64, 'plaster')):
    pass
# two recessed panels (upper tall, lower short), stacked
m.box((1.46, 0.04, 0.82), (0, 0.01, 1.6), 'plaster')
m.box((1.46, 0.04, 0.58), (0, 0.01, 0.58) if False else (0, 0.01, 0.66), 'plaster')
# panel mouldings (raised lips inside the recess) painted lighter
# scuffs and a gouge
m.box((0.5, 0.008, 0.07), (0.2, -0.033, 0.12), 'plaster_dirty')
m.box((0.3, 0.008, 0.2), (-0.45, -0.033, 0.3), 'wood_grey')
m.box((0.08, 0.008, 0.45), (0.78, -0.033, 1.6), 'plaster_dirty')
m.box((0.02, 0.012, 0.22), (0.35, -0.034, 1.0), 'wood_dark', rot=(0, 25, 0))    # gouge
m.box((0.16, 0.008, 0.04), (-0.4, -0.033, 2.05), 'concrete_light')
# knob (front), rose and spindle; latch edge
m.cyl(0.04, 0.02, (0.76, -0.04, 1.0), 'mustard_dark', rot=(90, 0, 0), seg=8)
m.cyl(0.015, 0.06, (0.76, -0.07, 1.0), 'mustard_dark', rot=(90, 0, 0), seg=6)
m.cyl(0.035, 0.05, (0.76, -0.09, 1.0), 'mustard', rot=(90, 0, 0), seg=8, r2=0.03)
m.cyl(0.04, 0.02, (0.76, 0.04, 1.0), 'mustard_dark', rot=(90, 0, 0), seg=8)
m.cyl(0.03, 0.04, (0.76, 0.07, 1.0), 'mustard', rot=(90, 0, 0), seg=8)
m.box((0.03, 0.04, 0.16), (0.9, 0, 1.0), 'steel')       # latch faceplate on edge
# hinges
for z in (0.3, 1.1, 1.9):
    m.cyl(0.016, 0.13, (-0.905, -0.01, z), 'steel_dark', seg=6)
    m.box((0.07, 0.065, 0.12), (-0.86, 0, z), 'steel')
finish(m, bounds={'x': (-0.95, 0.95), 'y': (-0.12, 0.12), 'z': (0, 2.2)})
