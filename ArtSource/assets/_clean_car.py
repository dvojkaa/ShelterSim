import math
from ss_art import Model, finish

BOUNDS = {'x': (-1.0, 1.0), 'y': (-2.25, 2.25), 'z': (0.0, 2.25)}

def side(m, prof, w, color, z=0.0):
    """Side profile polygon [(y, z)...] CCW in (y,z) extruded across X (width w)."""
    m.poly(prof, w, (0, 0, z), color, rot=(0, 0, 90))

def build(name, paint, kind):
    m = Model(name, seed=51)
    wag = kind == 'wagon'
    dark = 'paint_white' if paint != 'paint_white' else 'fabric_grey'
    # lower body (nose at +Y)
    body = [(-2.2, 0.42), (-2.2, 0.95), (-2.0, 1.0), (-1.5, 1.05), (1.3, 1.05), (1.95, 0.95), (2.2, 0.8), (2.2, 0.42), (2.0, 0.3), (-2.0, 0.3)]
    side(m, [(-y, z) if False else (y, z) for y, z in body][::-1] if False else body, 1.74, paint)
    # bumpers
    m.box((1.8, 0.12, 0.2), (0, 2.19, 0.42), 'black', bevel=0.02)
    m.box((1.8, 0.12, 0.2), (0, -2.19, 0.42), 'black', bevel=0.02)
    m.box((1.72, 0.04, 0.06), (0, 2.23, 0.6), 'chrome') if False else None
    # lights
    for s in (-1, 1):
        m.box((0.3, 0.06, 0.14), (s * 0.62, 2.19, 0.82), 'lamp_warm')
        m.box((0.3, 0.06, 0.12), (s * 0.66, -2.19, 0.82), 'warning_red')
    # grille
    m.box((0.6, 0.05, 0.14), (0, 2.18, 0.66), 'gunmetal')
    # hood / trunk panel lines are implied by darker strips
    # cabin
    if wag:
        cab = [(-1.95, 1.02), (-1.98, 1.55), (-1.8, 1.72), (0.4, 1.72), (0.95, 1.05)]
    else:
        cab = [(-2.0, 1.02), (-2.02, 1.35), (-1.55, 1.68), (0.3, 1.68), (0.95, 1.05)]
    side(m, cab, 1.6, 'glass')
    # roof slab, pillars
    ry0, ry1 = (-1.82, 0.38) if wag else (-1.55, 0.3)
    m.box((1.66, ry1 - ry0, 0.08), (0, (ry0 + ry1) / 2, 1.7), paint, bevel=0.02)
    # pillars at the sides: A, B, C
    for s in (-1, 1):
        x = s * 0.84
        beam_pts = [((0.28, 1.7), (0.95, 1.05)), ((ry0 + 0.1, 1.7), (ry0 + 0.0 if wag else -1.2, 1.5 if not wag else 1.7))]
        # A pillar
        m.box((0.08, 0.1, 0.8), (x, 0.6, 1.4), paint, rot=(-40 if False else 0, 0, 0)) if False else None
    import math as _m
    def pillar(y0, z0, y1, z1, x):
        d = _m.hypot(y1 - y0, z1 - z0)
        a = _m.degrees(_m.atan2(z1 - z0, y1 - y0))
        m.box((0.08, d, 0.1), (x, (y0 + y1) / 2, (z0 + z1) / 2), paint, rot=(a, 0, 0))
    for s in (-1, 1):
        x = s * 0.82
        pillar(0.95, 1.05, 0.3, 1.7, x)
        if wag:
            m.box((0.08, 0.14, 0.7), (x, -0.55, 1.4), paint)
            pillar(-1.95, 1.05, -1.8, 1.7, x)
        else:
            m.box((0.08, 0.14, 0.7), (x, -0.55, 1.4), paint)
            pillar(-2.0, 1.05, -1.55, 1.66, x)
    # side window trim line (door cut) and door handles
    for s in (-1, 1):
        m.box((0.02, 0.03, 0.55), (s * 0.88, 0.15, 0.7), 'black') if False else None
        m.box((0.02, 0.03, 0.55), (s * 0.88, -0.65, 0.7), 'black') if False else None
        m.box((0.03, 0.2, 0.04), (s * 0.88, 0.3, 0.92), 'chrome')
        m.box((0.03, 0.2, 0.04), (s * 0.88, -0.9, 0.92), 'chrome')
        m.box((0.03, 0.02, 0.6), (s * 0.87, 0.9, 0.72), 'black')
        m.box((0.03, 0.02, 0.6), (s * 0.87, -0.1, 0.72), 'black')
        m.box((0.03, 0.02, 0.6), (s * 0.87, -1.3 if not wag else -1.5, 0.72), 'black')
        # mirrors
        m.box((0.14, 0.1, 0.1), (s * 0.92, 0.75, 1.15), paint)
    if wag:
        # roof rails
        for s in (-1, 1):
            m.box((0.05, 2.1, 0.05), (s * 0.6, -0.75, 1.8), 'steel_dark')
        m.box((0.6, 0.03, 0.3), (0, -2.14, 1.0), 'glass') if False else None
    # wheels
    for sx in (-1, 1):
        for y in (-1.4, 1.4):
            m.cyl(0.35, 0.2, (sx * 0.86, y, 0.35), 'rubber', rot=(0, 90, 0), seg=12)
            m.cyl(0.2, 0.22, (sx * 0.87, y, 0.35), 'chrome', rot=(0, 90, 0), seg=10)
            m.cyl(0.07, 0.24, (sx * 0.87, y, 0.35), 'steel_dark', rot=(0, 90, 0), seg=6)
    # wheel arch shadows (black rings suggested by dark blocks above the wheels)
    for sx in (-1, 1):
        for y in (-1.4, 1.4):
            pass
    # underbody
    m.box((1.5, 4.0, 0.12), (0, 0, 0.25), 'gunmetal')
    # plate
    m.box((0.5, 0.02, 0.14), (0, 2.27 - 0.01, 0.4), 'paint_white') if False else None
    finish(m, bounds=BOUNDS)
