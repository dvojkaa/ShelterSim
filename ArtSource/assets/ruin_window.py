from ss_art import Model, finish
m = Model('ruin_window', seed=25)

# Applied onto a wall at y=0, sticks out toward -Y only.
OX, OZ = 0.47, 0.52    # opening half-size

# Dark interior: flush behind everything, hidden side lies on the wall.
m.box((2 * OX + 0.04, 0.012, 2 * OZ + 0.04), (0, -0.008, 0), 'black')

# Broken plaster surround with jagged outer edge (one ring polygon per side keeps it chunky).
m.poly([(-0.65, -0.62), (-0.47, -0.62), (-0.47, 0.6), (-0.58, 0.66), (-0.63, 0.5), (-0.6, 0.3), (-0.65, 0.12),
        (-0.6, -0.1), (-0.65, -0.34)][::-1], 0.03, (0, -0.017, 0), 'plaster_dirty')
m.poly([(0.47, -0.6), (0.62, -0.62), (0.65, -0.3), (0.6, -0.08), (0.65, 0.18), (0.6, 0.42), (0.63, 0.66), (0.47, 0.6)],
       0.03, (0, -0.017, 0), 'plaster_dirty')
m.poly([(-0.5, 0.52), (0.5, 0.52), (0.56, 0.68), (0.3, 0.64), (0.1, 0.7), (-0.2, 0.63), (-0.44, 0.7)], 0.03, (0, -0.017, 0), 'plaster')

# Stone sill and lintel, these give it the architectural read.
m.box((1.24, 0.098, 0.09), (0, -0.051, -0.565), 'concrete_light', bevel=0.008)
m.box((1.1, 0.03, 0.06), (0, -0.017, -0.65), 'concrete_dark')
m.box((1.16, 0.056, 0.1), (0, -0.034, 0.56), 'concrete', bevel=0.006)

# Timber window frame: rotted, slightly askew.
for x in (-1, 1):
    m.box((0.08, 0.066, 0.9), (x * 0.47, -0.036, 0.0), 'wood_dark')
m.box((1.0, 0.066, 0.08), (0, -0.036, 0.49), 'wood_dark')
m.box((1.0, 0.066, 0.07), (0, -0.036, -0.49), 'wood_dark')
# Remains of the glazing bars
m.box((0.035, 0.03, 0.96), (0.02, -0.022, 0), 'wood_grey')
m.box((0.88, 0.03, 0.03), (0, -0.022, 0.06), 'wood_grey', rot=(0, 2, 0))

# Broken glass shards hanging on in the corners.
GY = -0.018
for pts, c in (([(-0.43, 0.45), (-0.05, 0.45), (-0.2, 0.3), (-0.3, 0.32), (-0.34, 0.14), (-0.43, 0.1)], 'glass_dirty'),
               ([(0.43, -0.45), (0.43, -0.1), (0.3, -0.24), (0.24, -0.2), (0.12, -0.34), (0.04, -0.45)], 'glass'),
               ([(0.43, 0.45), (0.43, 0.3), (0.34, 0.2), (0.28, 0.38), (0.12, 0.45)], 'glass')):
    m.poly(pts[::-1] if c == 'none' else pts, 0.006, (0, GY, 0), c)

# Boards nailed across, each one chosen to read separately from a distance.
BY = -0.073
m.box((1.16, 0.026, 0.15), (0.0, BY, 0.26), 'wood', rot=(0, -6, 0))
m.box((1.12, 0.026, 0.13), (0.03, BY - 0.002, -0.05), 'wood_grey', rot=(0, 3.5, 0))
m.box((1.14, 0.026, 0.16), (-0.02, BY, -0.31), 'wood_light', rot=(0, 8, 0))
# Snapped diagonal board: two segments with a jagged tip
m.poly([(-0.53, 0.4), (0.07, -0.02), (0.2, 0.02), (0.1, 0.08), (0.13, 0.13), (-0.5, 0.52)], 0.022, (0, -0.088, 0), 'wood_dark')

# Bent nails
for x, z in ((-0.53, 0.3), (0.53, 0.2), (-0.53, 0.0), (0.54, -0.06), (-0.53, -0.38), (0.53, -0.24), (-0.46, 0.46), (0.04, 0.06)):
    m.cyl(0.011, 0.008, (x, -0.095, z), 'gunmetal', rot=(90, 0, 0), seg=5)

# Rust/water stains running off the sill
m.box((0.1, 0.004, 0.06), (-0.3, -0.032, -0.648), 'rust_dark')
m.box((0.16, 0.004, 0.05), (0.24, -0.032, -0.65), 'concrete_light')

finish(m, bounds={'x': (-0.65, 0.65), 'y': (-0.1, 0.0), 'z': (-0.7, 0.7)})
