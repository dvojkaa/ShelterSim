from ss_art import Model, finish
m = Model('piece_wall', seed=21)

# Tiling contract: plinth, cap beam, core and half-posts all run flush to x=+-1.0 with matching
# colours and heights, so two neighbours close up into one continuous plinth/beam and one 0.28 m post.
IN = 0.86       # inner face of the half-posts


def corrugated(x0, x1, z0, z1, s, color, back=0.045, base=0.058, peak=0.078, pitch=0.12):
    """Corrugated sheet as one zig-zag extrusion. s=-1 front, s=+1 back side of the wall."""
    n = max(2, round((x1 - x0) / (pitch / 2)))
    pts = [(x0, -s * back)]
    for i in range(n + 1):
        y = peak if i % 2 else base
        pts.append((x0 + (x1 - x0) * i / n, -s * y))
    pts.append((x1, -s * back))
    if s > 0:
        pts.reverse()
    # rot X 90: profile (x, p) -> world (x, -p), extrusion axis -> world Z
    m.poly(pts, z1 - z0, (0, 0, (z0 + z1) / 2), color, rot=(90, 0, 0))


def bolt(x, z, s, y=0.092, color='gunmetal'):
    m.cyl(0.02, 0.016, (x, s * y, z), color, rot=(90, 0, 0), seg=4)


def planks(z0, z1, s, widths, colors):
    z = z0
    for i, w in enumerate(widths):
        tilt = (-1.0, 0.6, -0.4, 1.1, -0.7, 0.5)[i % 6]
        c = colors[i % len(colors)]
        m.box((2 * IN, 0.026, w - 0.016), (0, s * 0.066, z + w / 2), c, rot=(0, tilt, 0))
        for x in (-0.72, 0.74):
            bolt(x, z + w / 2, s, y=0.081, color='steel_dark')
        z += w


# Solid core keeps the silhouette honest and hides everything behind the bays.
m.box((1.98, 0.09, 2.96), (0, 0, 1.5), 'concrete_dark')   # stops short of every outer face: no coplanar z-fight

# Concrete plinth and cap beam: run the full width and tile.
m.box((2.0, 0.188, 0.34), (0, 0, 0.17), 'concrete')
m.box((2.0, 0.188, 0.26), (0, 0, 2.87), 'concrete')
for s in (-1, 1):
    # Chamfer strip above the plinth, under the beam
    m.box((2.0, 0.008, 0.05), (0, s * 0.096, 0.3), 'concrete_light')
    m.box((2.0, 0.008, 0.04), (0, s * 0.096, 2.76), 'concrete_light')

# Half-posts: with the neighbouring panel these read as one 0.28 m concrete column.
for x in (-1, 1):
    m.box((0.14, 0.18, 2.4), (x * 0.93, 0, 1.54), 'concrete_light')

# --- Front (-Y): rusty corrugated lower bay, salvaged planks above.
corrugated(-IN, 0.12, 0.34, 1.42, -1, 'rust')
corrugated(0.06, IN, 0.34, 1.42, -1, 'steel_dark', base=0.064, peak=0.084, back=0.05)
corrugated(-0.66, -0.22, 0.52, 0.98, -1, 'rust_dark', base=0.08, peak=0.096, back=0.05)
planks(1.52, 2.74, -1, (0.22, 0.17, 0.24, 0.2, 0.18, 0.21), ('wood', 'wood_grey', 'wood_dark', 'wood', 'wood_light', 'wood_grey'))
# Steel patch screwed over the planks, with a diagonal brace
m.box((0.5, 0.014, 0.44), (0.42, -0.086, 2.2), 'steel', rot=(0, 5, 0))
for x, z in ((0.22, 2.03), (0.64, 2.01), (0.2, 2.39), (0.62, 2.37)):
    m.cyl(0.014, 0.008, (x, -0.096, z), 'gunmetal', rot=(90, 0, 0), seg=5)
m.box((1.0, 0.016, 0.06), (-0.34, -0.084, 2.12), 'rust_dark', rot=(0, -34, 0))

# --- Back (+Y): the bays swap so it doesn't look mirrored.
planks(0.34, 1.42, 1, (0.2, 0.18, 0.24, 0.16, 0.3), ('wood_grey', 'wood_dark', 'wood', 'wood_grey', 'wood_light'))
corrugated(-IN, -0.02, 1.52, 2.74, 1, 'steel_dark')
corrugated(-0.08, IN, 1.52, 2.74, 1, 'rust_dark', base=0.064, peak=0.084, back=0.05)
corrugated(0.26, 0.7, 1.78, 2.36, 1, 'rust', base=0.086, peak=0.098, back=0.05)
m.box((0.5, 0.014, 0.3), (-0.4, 0.086, 0.9), 'steel', rot=(0, -4, 0))

# Mid rail: rusty channel bolted through the posts on both sides.
for s in (-1, 1):
    m.box((2 * IN, 0.03, 0.1), (0, s * 0.075, 1.47), 'rust_dark')
    m.box((2 * IN, 0.008, 0.03), (0, s * 0.0935, 1.47), 'rust')
    for x in (-0.6, -0.2, 0.2, 0.6):
        bolt(x, 1.47, s, y=0.091)
    # Post bolts and concrete spalling
    for x in (-0.93, 0.93):
        for z in (0.62, 1.47, 2.34):
            bolt(x, z, s)
    # Plinth stains and chips: small overlays that stop short of the tile edge
    m.box((0.42, 0.006, 0.12), (s * 0.5, s * 0.096, 0.08), 'concrete_dark')
    m.box((0.08, 0.006, 0.34), (-s * 0.93, s * 0.092, 1.02), 'concrete')
    m.box((0.05, 0.006, 0.22), (s * 0.95, s * 0.092, 2.0), 'concrete_dark')
    m.box((0.12, 0.006, 0.06), (-s * 0.3, s * 0.096, 2.9), 'concrete_dark')
    # Rust weeping from the rail bolts
    m.box((0.05, 0.004, 0.2), (-s * 0.2, s * 0.083, 1.31), 'rust_dark')

finish(m, bounds={'x': (-1.0, 1.0), 'y': (-0.1, 0.1), 'z': (0, 3.0)})
