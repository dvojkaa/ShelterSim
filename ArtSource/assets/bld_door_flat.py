import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('bld_door_flat', seed=302)
# darker stained wood slab with raised bolection panel frame
m.box((1.8, 0.07, 2.2), (0, 0, 1.1), 'wood_dark')
for s in (-1, 1):
    m.box((0.06, 0.02, 1.9), (s * 0.55, -0.04, 1.2), 'wood')
m.box((1.16, 0.02, 0.06), (0, -0.04, 2.1), 'wood')
m.box((1.16, 0.02, 0.06), (0, -0.04, 0.3), 'wood')
m.box((1.16, 0.02, 0.05), (0, -0.04, 1.2), 'wood')
# grain streaks
for x, z, h in ((-0.3, 1.6, 0.5), (0.1, 0.8, 0.6), (0.38, 1.75, 0.3), (-0.42, 0.55, 0.3)):
    m.box((0.02, 0.006, h), (x, -0.036, z), 'wood_light' if h < 0.4 else 'wood_grey')
# peephole
m.cyl(0.022, 0.02, (0.0, -0.05, 1.62), 'chrome', rot=(90, 0, 0), seg=8)
m.cyl(0.008, 0.022, (0.0, -0.052, 1.62), 'black', rot=(90, 0, 0), seg=6)
# blank number plate
m.box((0.2, 0.012, 0.1), (0.0, -0.05, 1.8), 'mustard_dark')
m.box((0.16, 0.006, 0.06), (0.0, -0.057, 1.8), 'bone')
# deadbolt: escutcheon, round, thumb-turn
m.box((0.07, 0.02, 0.14), (0.7, -0.05, 1.27), 'steel', bevel=0.005)
m.cyl(0.028, 0.03, (0.7, -0.07, 1.29), 'steel_dark', rot=(90, 0, 0), seg=8)
m.box((0.03, 0.02, 0.02), (0.7, -0.09, 1.29), 'steel_dark')
# latch plate + lever-less lock handle (cylinder knob) below
m.box((0.1, 0.02, 0.2), (0.7, -0.05, 1.0), 'steel_dark', bevel=0.005)
m.cyl(0.032, 0.05, (0.7, -0.08, 1.0), 'steel', rot=(90, 0, 0), seg=8)
# chain slot (rail + slot on door)
m.box((0.14, 0.014, 0.03), (0.6, -0.045, 1.55), 'steel')
m.box((0.1, 0.016, 0.012), (0.6, -0.047, 1.55), 'black')
# worn bottom edge: doormat scuff and dirt
m.box((1.8, 0.075, 0.1), (0, 0, 0.05), 'concrete_dark')
m.box((1.4, 0.078, 0.04), (0.05, 0, 0.12), 'wood_grey')
m.box((0.5, 0.008, 0.3), (-0.3, -0.037, 0.3) if False else (-0.55, -0.037, 0.3), 'dirt')
# kick plate
m.box((1.3, 0.012, 0.2), (0, -0.04, 0.2), 'steel_dark')
# hinges
for z in (0.3, 1.1, 1.9):
    m.cyl(0.02, 0.15, (-0.905, -0.02, z), 'gunmetal', seg=6)
    m.box((0.08, 0.075, 0.13), (-0.86, 0, z), 'steel_dark')
# back side: latch edge, handle
m.cyl(0.03, 0.05, (0.7, 0.065, 1.0), 'steel', rot=(90, 0, 0), seg=8)
finish(m, bounds={'x': (-0.95, 0.95), 'y': (-0.12, 0.12), 'z': (0, 2.2)})
