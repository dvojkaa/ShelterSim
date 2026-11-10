from ss_art import Model, finish
m = Model('bld_apt_entrance', seed=63)

# Clear opening |x|<1.4, z<2.4 stays EMPTY above the 0.05 step. Wall body y -0.2..0.
m.box((0.4, 0.2, 3.4), (-1.6, -0.1, 1.7), 'concrete', bevel=0.012)
m.box((0.4, 0.2, 3.4), (1.6, -0.1, 1.7), 'concrete', bevel=0.012)
m.box((3.2, 0.18, 0.38), (0, -0.1, 3.19), 'concrete', bevel=0.012)       # header above transom
m.box((2.8, 0.2, 0.2), (0, -0.1, 2.5), 'concrete_dark')              # lintel over opening (z 2.4..2.6)
# Raised surround trim on the street face
for s in (-1, 1):
    m.box((0.12, 0.06, 3.0), (s * 1.74, -0.23, 1.5), 'concrete_light', bevel=0.01)
m.box((3.6, 0.06, 0.14), (0, -0.23, 0.07), 'concrete_dark', bevel=0.01)   # plinth
# Door frame (outside the clear opening), steel, with hinge blocks
for s in (-1, 1):
    m.box((0.08, 0.14, 2.4), (s * 1.44, -0.08, 1.2), 'steel_dark')
m.box((2.96, 0.14, 0.08), (0, -0.08, 2.44), 'steel_dark')
# Transom: frame, bars, remaining glass shards, dark hole
m.box((2.8, 0.03, 0.46), (0, -0.04, 2.8), 'black')
for x in (-0.7, 0.0, 0.7):
    m.box((0.05, 0.08, 0.5), (x, -0.07, 2.8), 'steel_dark')
m.box((2.8, 0.08, 0.05), (0, -0.07, 3.04), 'steel_dark')
m.poly([(-1.38, 2.58), (-0.72, 2.58), (-0.9, 2.8), (-0.75, 3.02), (-1.38, 3.02)], 0.01, (0, -0.07, 0), 'glass_dirty')
m.poly([(0.02, 2.58), (0.68, 2.58), (0.68, 2.75), (0.4, 2.7), (0.3, 3.02), (0.02, 3.02)], 0.01, (0, -0.07, 0), 'glass')
m.poly([(0.72, 3.02), (1.38, 3.02), (1.38, 2.85), (1.15, 2.95)], 0.01, (0, -0.07, 0), 'glass_dirty')
# Canopy: slab with fascia, tie rods to the wall, tilted wedge look
m.box((3.4, 0.6, 0.16), (0, -0.3, 3.15), 'concrete_dark', bevel=0.012)
m.box((3.4, 0.06, 0.26), (0, -0.57, 3.2), 'concrete', bevel=0.01)
m.box((3.0, 0.5, 0.02), (0, -0.33, 3.08), 'black')
for s in (-1, 1):
    m.tube([(s * 1.55, -0.54, 3.07), (s * 1.55, -0.12, 2.75)], 0.018, 'rust', seg=5)
# Step
m.box((3.2, 0.5, 0.05), (0, -0.35, 0.025), 'concrete_light', bevel=0.005)
m.box((3.2, 0.05, 0.05), (0, -0.62 + 0.05, 0.025), 'concrete_dark')
# Buzzer / intercom panel (blank)
m.box((0.26, 0.04, 0.42), (1.62, -0.27, 1.3), 'steel_dark', bevel=0.008)
for i in range(3):
    for j in range(2):
        m.box((0.07, 0.02, 0.07), (1.56 + 0.1 * j, -0.3, 1.14 + 0.12 * i), 'steel')
m.box((0.18, 0.02, 0.06), (1.62, -0.3, 1.46), 'black')
# Dangling cable and weathering
m.tube([(1.5, -0.3, 1.5), (1.48, -0.3, 1.2), (1.2, -0.25, 0.6), (1.1, -0.2, 0.2)], 0.012, 'black', seg=4)
m.poly([(-1.75, 0.5), (-1.7, 0.5), (-1.62, 1.2), (-1.7, 1.9), (-1.76, 1.2)], 0.01, (0, -0.265, 0), 'concrete_dark')
m.poly([(1.72, 2.4), (1.78, 2.4), (1.76, 3.0), (1.7, 3.3), (1.66, 3.0)], 0.01, (0, -0.265, 0), 'concrete_dark')
m.box((0.2, 0.01, 0.7), (-1.2, -0.26, 0.4), 'plaster_dirty')
m.box((0.12, 0.01, 0.4), (0.9, -0.27, 3.15), 'rust_dark')
m.box((0.4, 0.01, 0.2), (-1.0, -0.21, 3.2), 'plaster_dirty')
m.cyl(0.012, 0.15, (-1.65, -0.2, 3.35), 'rust', rot=(90, 0, 0), seg=5)

finish(m, bounds={'x': (-1.8, 1.8), 'y': (-0.6, 0.0), 'z': (0, 3.4)})
