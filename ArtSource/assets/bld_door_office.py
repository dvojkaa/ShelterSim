import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('bld_door_office', seed=204)
# Office interior door leaf: painted steel/wood, frosted glass upper panel. Origin floor centre, hinge x=-0.9, front -Y.
m.box((1.8, 0.07, 2.2), (0, 0, 1.1), 'paint_white')
m.box((1.8, 0.03, 2.2), (0, 0.025, 1.1), 'plaster')  # slightly darker back face
# frame trim around (raised border)
m.box((0.08, 0.02, 2.2), (-0.86, -0.045, 1.1), 'plaster_dirty')
m.box((0.08, 0.02, 2.2), (0.86, -0.045, 1.1), 'plaster_dirty')
m.box((1.8, 0.018, 0.08), (0, -0.045, 2.16), 'plaster_dirty')
# frosted glass panel in upper half with frame
m.box((0.8, 0.025, 1.0), (0.0, -0.045, 1.5), 'steel_dark')
m.box((0.64, 0.03, 0.84), (0.0, -0.048, 1.5), 'glass_dirty')
m.box((0.64, 0.012, 0.12), (0.0, -0.065, 1.25), 'fabric_grey')   # grime band
m.box((0.2, 0.012, 0.5), (-0.2, -0.065, 1.65), 'concrete_light') # frosted highlight
m.box((0.05, 0.01, 0.4), (0.15, -0.065, 1.5), 'concrete_light', rot=(0, 20, 0))
# lower painted panel recess
m.box((1.3, 0.012, 0.75), (0.0, -0.043, 0.55), 'plaster', bevel=0.01)
m.box((1.3, 0.008, 0.04), (0.0, -0.05, 0.9), 'concrete_light')
# dent (angled slab and dark scrape)
m.box((0.3, 0.014, 0.22), (-0.35, -0.0435, 0.5), 'plaster_dirty', rot=(0, 25, 0))
m.box((0.34, 0.006, 0.02), (-0.35, -0.052, 0.58), 'concrete_dark', rot=(0, 25, 0))
# kick scuffs
m.box((0.8, 0.006, 0.07), (0.2, -0.0405, 0.06), 'concrete_dark')
m.box((0.12, 0.006, 0.3), (0.8, -0.0405, 0.4), 'plaster_dirty')
# lever handle with lock
m.box((0.12, 0.012, 0.28), (0.78, -0.0465, 1.0), 'steel_dark', bevel=0.005)
m.cyl(0.03, 0.05, (0.78, -0.06, 1.04), 'chrome', rot=(90, 0, 0), seg=8)
m.box((0.2, 0.02, 0.03), (0.69, -0.085, 1.04), 'chrome')
m.cyl(0.02, 0.02, (0.78, -0.056, 0.93), 'gunmetal', rot=(90, 0, 0), seg=6)
# blank nameplate slot
m.box((0.34, 0.012, 0.1), (0.0, -0.052, 2.01), 'steel')
m.box((0.28, 0.008, 0.06), (0.0, -0.059, 2.01), 'fabric_white')
# hinges
for z in (0.3, 1.1, 1.9):
    m.cyl(0.035, 0.22, (-0.9, -0.045, z), 'steel_dark', seg=6)
    m.box((0.16, 0.02, 0.1), (-0.82, -0.05, z), 'steel')
# back handle
m.box((0.12, 0.012, 0.28), (0.78, 0.055, 1.0), 'steel_dark')
m.box((0.2, 0.02, 0.03), (0.69, 0.07, 1.04), 'chrome')
finish(m, bounds={'x': (-0.95, 0.95), 'y': (-0.12, 0.12), 'z': (0, 2.2)})
