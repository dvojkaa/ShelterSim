import math
from ss_art import Model, finish
m = Model('door_leaf', seed=23)

# Bunker door, hinge side x=-0.8, handle side +x. Layers from the middle outwards (per side):
# core 0.028, panels to 0.034, frame rails to 0.04, straps to 0.044, rivets/wheel to 0.05.
m.box((1.6, 0.056, 2.2), (0, 0, 1.1), 'steel_dark')
# Hinge knuckles at the -x edge: centred in the thickness
for z in (0.32, 1.1, 1.88):
    m.cyl(0.05, 0.2, (-0.75, 0, z), 'gunmetal', seg=8)


def rivet(x, z, s, y=0.046, c='steel'):
    m.cyl(0.018, 0.008, (x, s * y, z), c, rot=(90, 0, 0), seg=4)


def wheel(cx, cz, s, R=0.2):
    y = s * 0.036
    n = 8
    pts = [(cx + R * math.cos(i * 2 * math.pi / n), y, cz + R * math.sin(i * 2 * math.pi / n)) for i in range(n + 1)]
    m.tube(pts, 0.014, 'warning_red', seg=4)
    for a in (20, 140, 260):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        m.tube([(cx, y, cz), (cx + R * ca, y, cz + R * sa)], 0.012, 'cloth_red', seg=3)
    m.cyl(0.06, 0.018, (cx, s * 0.04, cz), 'gunmetal', rot=(90, 0, 0), seg=8)
    m.cyl(0.03, 0.02, (cx, s * 0.04, cz), 'steel', rot=(90, 0, 0), seg=6)


for s in (-1, 1):
    y_panel = s * 0.031
    y_frame = s * 0.034
    # Riveted frame
    m.box((0.14, 0.012, 2.2), (-0.73, y_frame, 1.1), 'gunmetal')
    m.box((0.14, 0.012, 2.2), (0.73, y_frame, 1.1), 'gunmetal')
    m.box((1.32, 0.012, 0.14), (0, y_frame, 2.13), 'gunmetal')
    m.box((1.32, 0.012, 0.1), (0, y_frame, 1.32), 'gunmetal')
    m.box((1.32, 0.012, 0.1), (0, y_frame, 0.72), 'gunmetal')

    # Upper plate
    m.box((1.32, 0.008, 0.76), (0, s * 0.03, 1.73), 'army_green_dark')
    # Salvaged wood panel between the rails
    for i, (x, w, c) in enumerate(((-0.53, 0.26, 'wood'), (-0.27, 0.26, 'wood_dark'), (0.0, 0.28, 'wood_grey'),
                                   (0.28, 0.28, 'wood'), (0.53, 0.22, 'wood_dark'))):
        m.box((w - 0.012, 0.01, 0.5), (x, y_panel, 1.02), c)
    # Hazard kick plate
    m.box((1.32, 0.008, 0.44), (0, s * 0.03, 0.44), 'black')
    stripes = [-0.56, -0.28, 0.0, 0.28, 0.56]
    for x in stripes:
        m.poly([(x - 0.13, 0.26), (x + 0.0, 0.26), (x + 0.13, 0.62), (x + 0.0, 0.62)], 0.004,
               (0, s * 0.0345, 0), 'hazard_yellow')
    m.box((1.32, 0.012, 0.21), (0, y_frame, 0.105), 'rust_dark')
    m.box((0.36, 0.006, 0.12), (0.42, s * 0.0415, 0.08), 'rust')

    # Hinge straps (long and heavy, so the hinge side is obvious from a distance)
    for z in (0.32, 1.1, 1.88):
        m.box((0.46, 0.012, 0.11), (-0.54, s * 0.04, z), 'rust_dark')
        for x in (-0.66, -0.36):
            rivet(x, z, s, y=0.046, c='gunmetal')

    # Frame rivets, sparse enough to read as rows rather than noise
    for z in (0.3, 0.9, 1.6, 2.04):
        rivet(0.73, z, s)
    for x in (-0.3, 0.3):
        rivet(x, 2.13, s)
        rivet(x, 1.32, s)

    # Vision slit: steel surround, dirty glass, two bars
    m.cyl(0.15, 0.012, (0.04, s * 0.04, 1.76), 'steel', rot=(90, 0, 0), seg=8)
    m.cyl(0.115, 0.008, (0.04, s * 0.044, 1.76), 'glass_dirty', rot=(90, 0, 0), seg=8)
    for dz in (-0.04, 0.04):
        m.box((0.2, 0.006, 0.016), (0.04, s * 0.047, 1.76 + dz), 'gunmetal')

    # Locking wheel + its striker bar across to the handle side
    wheel(0.04, 1.26, s)
    m.box((0.44, 0.012, 0.05), (0.5, s * 0.04, 1.26), 'steel')
    # Pull handle on the free edge
    m.box((0.06, 0.008, 0.34), (0.68, s * 0.041, 1.26), 'gunmetal')
    m.tube([(0.68, s * 0.037, 1.12), (0.68, s * 0.037, 1.4)], 0.012, 'chrome', seg=5)

    # Rust bleed and scratches
    m.box((0.12, 0.004, 0.28), (0.6, s * 0.0362, 2.0), 'rust_dark')
    m.box((0.3, 0.004, 0.08), (-0.25, s * 0.0362, 1.6), 'rust')

finish(m, bounds={'x': (-0.8, 0.8), 'y': (-0.05, 0.05), 'z': (0, 2.2)})
