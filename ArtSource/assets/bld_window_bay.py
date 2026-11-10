import random
from ss_art import Model, finish
m = Model('bld_window_bay', seed=61)
r = random.Random(6101)

# Wall body y -0.05..0.2, opening x +-0.8, z 0.9..2.2. Four slabs around the opening so it is a real hole.
Y0, T = 0.075, 0.25
m.box((3.0, T, 0.9), (0, Y0, 0.45), 'concrete')
m.box((3.0, T, 0.8), (0, Y0, 2.6), 'concrete')
m.box((0.7, T, 1.3), (-1.15, Y0, 1.55), 'concrete')
m.box((0.7, T, 1.3), (1.15, Y0, 1.55), 'concrete')
# Dark room behind the opening
m.box((1.6, 0.02, 1.3), (0, 0.185, 1.55), 'black')

# Half pilasters on both edges (tile into full pilasters), floor band on top (tiles into a slab edge).
for s in (-1, 1):
    m.box((0.15, 0.06, 2.8), (s * 1.425, -0.08, 1.4), 'concrete_dark', bevel=0.01)
m.box((3.0, 0.1, 0.18), (0, -0.1, 2.91), 'concrete_dark', bevel=0.01)

# Frame (steel-grey painted), set in the opening
for s in (-1, 1):
    m.box((0.08, 0.1, 1.3), (s * 0.76, 0.0, 1.55), 'steel_dark')
m.box((1.6, 0.1, 0.08), (0, 0.0, 2.16), 'steel_dark')
m.box((1.6, 0.1, 0.08), (0, 0.0, 0.94), 'steel_dark')
m.box((0.05, 0.07, 1.2), (0.0, 0.0, 1.55), 'steel_dark')       # centre mullion
# Sill ledge and lintel
m.box((1.9, 0.2, 0.08), (0, -0.05, 0.86), 'concrete_light', bevel=0.01)
m.box((1.8, 0.06, 0.1), (0, -0.08, 2.27), 'concrete_dark', bevel=0.01)

# Left half: broken glass shards; right half: boarded
m.poly([(-0.72, 1.0), (-0.03, 1.0), (-0.2, 1.3), (-0.3, 1.25), (-0.4, 1.55), (-0.72, 1.6)], 0.01, (0, 0.0, 0), 'glass_dirty')
m.poly([(-0.72, 2.12), (-0.03, 2.12), (-0.03, 1.9), (-0.3, 2.0), (-0.45, 1.85), (-0.72, 1.9)], 0.01, (0, 0.0, 0), 'glass')
m.poly([(-0.72, 2.12), (-0.55, 2.12), (-0.72, 1.98)], 0.01, (0, -0.012, 0), 'glass_dirty')
for z, c, a in ((1.2, 'wood', -4), (1.62, 'wood_grey', 3), (2.0, 'wood_light', -2)):
    m.box((0.82, 0.03, 0.2), (0.4, -0.065, z), c, rot=(0, a, 0))
m.box((0.1, 0.03, 1.1), (0.55, -0.095, 1.55), 'wood_dark', rot=(0, 0, 18))
for x, z in ((0.05, 1.2), (0.05, 1.62), (0.76, 1.2), (0.76, 2.0), (0.05, 2.0)):
    m.cyl(0.012, 0.01, (x, -0.085, z), 'gunmetal', rot=(90, 0, 0), seg=5)

# Weathering: cracks (thin dark wedges), stains, patched repair
m.poly([(-1.2, 0.9), (-1.17, 0.9), (-1.1, 1.5), (-1.14, 1.8), (-1.19, 1.5)], 0.01, (0, -0.056, 0), 'concrete_dark')
m.poly([(1.0, 2.2), (1.04, 2.2), (1.1, 2.6), (1.25, 2.8), (1.2, 2.82), (1.05, 2.65)], 0.01, (0, -0.056, 0), 'concrete_dark')
m.poly([(-0.9, 0.0), (-0.85, 0.0), (-0.6, 0.5), (-0.5, 0.45), (-0.8, 0.0)], 0.01, (0, -0.056, 0), 'concrete_dark')
m.box((0.5, 0.01, 0.5), (1.15, -0.055, 0.4), 'concrete_light')
m.box((0.14, 0.01, 0.7), (-0.35, -0.1, 0.45), 'rust_dark')   # run-off under the sill ledge
m.box((0.1, 0.012, 0.6), (0.5, -0.057, 0.5), 'rust_dark')
m.box((0.9, 0.01, 0.5), (0.0, -0.056, 2.6), 'concrete_dark')
for i in range(3):
    m.box((0.25 + 0.1 * i, 0.012, 0.35), (-1.1 + 0.9 * i, -0.056, 2.55 + 0.1 * i), 'plaster_dirty')
# Rebar stub from a spalled corner
m.cyl(0.012, 0.18, (-1.1, -0.05, 2.15), 'rust', rot=(90, 0, 0), seg=5)

finish(m, bounds={'x': (-1.5, 1.5), 'y': (-0.15, 0.2), 'z': (0, 3.0)})
