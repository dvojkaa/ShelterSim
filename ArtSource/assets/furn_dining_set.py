import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('furn_dining_set', seed=323)
# table 1.0 x 0.8, top z 0.75
m.box((0.84, 0.64, 0.05), (0, 0, 0.745), 'wood', bevel=0.01)
m.box((0.76, 0.56, 0.07), (0, 0, 0.69), 'wood_dark')
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((0.07, 0.07, 0.72), (sx * 0.37, sy * 0.27, 0.36), 'wood_dark', bevel=0.005)
m.box((0.3, 0.2, 0.005), (-0.2, 0.1, 0.773), 'wood_grey')            # worn patch
m.box((0.15, 0.15, 0.005), (0.25, -0.15, 0.773), 'wood_dark')           # stain
m.box((0.2, 0.2, 0.01), (0.35, 0.28, 0.755), 'wood_dark')               # chipped corner
m.cyl(0.08, 0.025, (0.15, -0.1, 0.78), 'plaster_dirty', seg=8)       # plate
m.cyl(0.035, 0.07, (-0.3, -0.15, 0.81), 'rust_light', seg=7)           # mug
m.box((0.1, 0.1, 0.02), (-0.1, 0.2, 0.78), 'rust_dark', rot=(0, 0, 25))

def chair(cx, cy, face):
    """face: direction unit vector the chair looks (toward table centre)."""
    # build axis-aligned: orient by swapping axes
    fx, fy = face
    def p(lx, ly, z):  # local x (right), local y (forward toward table) -> world
        return (cx + lx * (-fy if fx == 0 else 1) * (1 if fx == 0 else 1) if False else cx + (lx * (-fy) + ly * fx), cy + (lx * fx + ly * fy), z)
    def bx(sx, sy, sz, lx, ly, z, c, **k):
        if fx == 0:   # facing +-Y: local x -> world x * (-fy)... sizes unchanged
            sz_w = (sx, sy, sz)
        else:
            sz_w = (sy, sx, sz)
        m.box(sz_w, p(lx, ly, z), c, **k)
    for lx in (-0.19, 0.19):
        for ly in (-0.19, 0.19):
            bx(0.04, 0.04, 0.42, lx, ly, 0.21, 'wood_dark')
    bx(0.44, 0.44, 0.04, 0, 0, 0.44, 'wood', bevel=0.006)             # seat
    bx(0.04, 0.04, 0.4, -0.19, -0.19, 0.66, 'wood_dark')              # back posts
    bx(0.04, 0.04, 0.4, 0.19, -0.19, 0.66, 'wood_dark')
    bx(0.4, 0.03, 0.08, 0, -0.19, 0.8, 'wood')                        # top rail
    bx(0.4, 0.03, 0.07, 0, -0.19, 0.64, 'wood_dark')                  # lower rail
    bx(0.04, 0.04, 0.04, 0, 0, 0.22, 'wood_dark') if False else None

# four chairs: faces toward table
chair(-0.1, -0.53, (0, 1))
chair(-0.1, 0.53, (0, -1))
chair(-0.55, 0.0, (1, 0))
# knocked over chair lying on its side, long axis along Y, right of table
cx, cy = 0.58, 0.1
m.box((0.04, 0.44, 0.44), (cx, cy, 0.24), 'wood', bevel=0.006) if False else None
m.box((0.44, 0.04, 0.44), (cx, cy - 0.0, 0.24), 'wood', bevel=0.006)   # seat plane standing on edge
for lx in (-0.19, 0.19):
    m.box((0.04, 0.4, 0.04), (cx + lx, cy - 0.22, 0.05 if lx < 0 else 0.43), 'wood_dark')   # legs point -y
    m.box((0.04, 0.4, 0.04), (cx + lx, cy + 0.2, 0.04), 'wood_dark')   # back posts on floor
m.box((0.08, 0.04, 0.4), (cx, cy - 0.41, 0.24), 'wood_dark')           # leg crossbar
m.box((0.4, 0.03, 0.08), (cx, cy + 0.38, 0.04), 'wood')                # top rail
finish(m, bounds={'x': (-0.8, 0.8), 'y': (-0.8, 0.8), 'z': (0, 0.95)})
