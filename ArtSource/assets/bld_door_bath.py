import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('bld_door_bath', seed=303)
m.box((1.8, 0.06, 2.2), (0, 0, 1.1), 'paint_white')
# louvre slats near the bottom: slanted boxes in a recessed vent
m.box((0.9, 0.012, 0.5), (0, -0.034, 0.5), 'plaster_dirty')
m.box((0.98, 0.02, 0.04), (0, -0.04, 0.78), 'paint_white')
m.box((0.98, 0.02, 0.04), (0, -0.04, 0.22), 'paint_white')
for s in (-1, 1):
    m.box((0.04, 0.02, 0.56), (s * 0.47, -0.04, 0.5), 'paint_white')
for i in range(6):
    m.box((0.88, 0.05, 0.022), (0, -0.045, 0.28 + i * 0.09), 'concrete_light', rot=(-30, 0, 0))
# upper panel line
m.box((1.3, 0.01, 0.03), (0, -0.034, 1.95), 'plaster')
m.box((0.03, 0.01, 0.9), (-0.65, -0.034, 1.5), 'plaster')
m.box((0.03, 0.01, 0.9), (0.65, -0.034, 1.5), 'plaster')
m.box((1.3, 0.01, 0.03), (0, -0.034, 1.05), 'plaster')
# water-stained lower third
m.box((1.8, 0.008, 0.65), (0, -0.032, 0.33) if False else (0, -0.031, 0.45), 'plaster_dirty') if False else None
m.box((1.78, 0.007, 0.1), (0, -0.0335, 0.72), 'fabric_stain')
m.box((0.5, 0.007, 0.25), (-0.55, -0.0335, 0.6), 'fabric_stain')
m.box((0.4, 0.007, 0.2), (0.55, -0.0335, 0.55), 'fabric_stain')
m.box((1.8, 0.065, 0.06), (0, 0, 0.03), 'wood_dark')   # swollen wet bottom edge
# mould patches
for x, z, w, h in ((-0.7, 0.2, 0.2, 0.14), (0.2, 0.1, 0.25, 0.1), (0.7, 0.28, 0.16, 0.2), (-0.25, 0.9, 0.12, 0.1)):
    m.box((w, 0.008, h), (x, -0.0345, z), 'leaf_dark', rot=(0, 15, 0))
m.box((0.08, 0.008, 0.07), (0.15, -0.035, 0.3), 'army_green_dark')
# small lock knob (thumb-turn) + indicator slot
m.box((0.08, 0.02, 0.12), (0.74, -0.04, 1.05), 'steel', bevel=0.005)
m.cyl(0.028, 0.03, (0.74, -0.06, 1.05), 'chrome', rot=(90, 0, 0), seg=8)
m.box((0.03, 0.01, 0.008), (0.74, -0.077, 1.05), 'black')
m.cyl(0.03, 0.04, (0.74, -0.06, 1.2), 'steel', rot=(90, 0, 0), seg=8)   # round knob above
m.cyl(0.03, 0.04, (0.74, 0.06, 1.2), 'steel', rot=(90, 0, 0), seg=8)
# blank occupancy plate
m.box((0.12, 0.01, 0.08), (0.0, -0.034, 1.7), 'concrete')
for z in (0.3, 1.1, 1.9):
    m.cyl(0.016, 0.13, (-0.905, -0.01, z), 'steel', seg=6)
    m.box((0.07, 0.065, 0.12), (-0.86, 0, z), 'steel_dark')
finish(m, bounds={'x': (-0.95, 0.95), 'y': (-0.12, 0.12), 'z': (0, 2.2)})
