from pathlib import Path
import bpy
import math
from mathutils import Vector, Matrix, Euler
from ss_art import Model, finish

m = Model('piece_armored_van', seed=3301)


def quad(part, pts, color, normal):
    """single-sided face (visible only from the +normal side): clear opening from the other side"""
    vs = [part.bm.verts.new(Vector(p)) for p in pts]
    f = part.bm.faces.new(vs)
    f.normal_update()  # new faces have a zero normal until updated; without this the flip test never ran
    if f.normal.dot(Vector(normal)) < 0:
        f.normal_flip()
    part._paint(part.bm, [f], color)

def xrot(c, pts, deg):
    r = Matrix.Rotation(math.radians(deg), 3, 'X')
    return [tuple(Vector(c) + r @ (Vector(p) - Vector(c))) for p in pts]

# chassis + body (front = +Y)
m.box((3.9, 6.2, .35), (0, 0, .62), 'gunmetal')
m.box((4.1, 3.3, 1.25), (0, -1.2, 1.40), 'army_green', bevel=.10)       # cargo box
m.box((4.0, 2.2, .95), (0, 1.95, 1.18), 'army_green', bevel=.10)        # hood
m.box((4.1, 1.8, 1.405), (0, .25, 1.5775), 'army_green_dark', bevel=.10, taper=(.9, 1))  # cab
m.box((3.5, 1.1, .10), (0, .75, 2.26), 'steel_dark', bevel=.03)          # cab roof plate
m.box((3.7, 3.0, .12), (0, -1.3, 2.10), 'steel_dark', bevel=.04)         # cargo roof plate
# slab side armour plates + welded seams
for s in (-1, 1):
    m.box((.14, 3.0, 1.0), (s*2.12, -1.25, 1.35), 'steel_dark', bevel=.03)
    if s > 0:
        m.box((.14, 1.6, .575), (s*2.12, .35, 1.1625), 'steel_dark', bevel=.03)   # below the big window
        m.box((.14, .15, .80), (s*2.12, 1.075, 1.85), 'steel_dark', bevel=.03)  # A pillar plate
        m.box((.14, .15, .80), (s*2.12, -.375, 1.85), 'steel_dark', bevel=.03)  # B pillar plate
        m.box((.14, 1.6, .15), (s*2.12, .35, 2.175), 'steel_dark', bevel=.03)   # brow
    else:  # fixed A/B pillar plates around the left door
        m.box((.14, .10, 1.375), (-2.12, 1.10, 1.5625), 'steel_dark', bevel=.03)
        m.box((.14, .05, 1.375), (-2.12, -.425, 1.5625), 'steel_dark', bevel=.03)
    m.box((.14, 2.0, .45), (s*2.08, 1.95, 1.10), 'rust_dark', bevel=.03)
    for y in (-2.4, -1.25, -.1):
        m.box((.18, .07, 1.05), (s*2.14, y, 1.38), 'rust')
    if s > 0:  # side slit window: single-sided so it is a clear opening from inside
        quad(m.default, [(2.195, -.3, 1.45), (2.195, 1.05, 1.45), (2.195, 1.05, 2.10), (2.195, -.3, 2.10)], 'black', (1, 0, 0))
        quad(m.default, [(2.196, -.2, 1.52), (2.196, .95, 1.52), (2.196, .95, 2.03), (2.196, -.2, 2.03)], 'glass', (1, 0, 0))
    # fenders
    m.box((.5, 1.5, .08), (s*2.2, 1.95, 1.72 - .55), 'rust_dark', bevel=.02)
# wide windshield with armour brow and side pillars
_wc = (0, 1.32, 1.62)
quad(m.default, xrot(_wc, [(-1.6, 1.32, 1.62), (1.6, 1.32, 1.62), (1.6, 1.32, 2.20), (-1.6, 1.32, 2.20)], 11), 'glass_dirty', (0, 1, 0))
m.box((3.4, .26, .10), (0, 1.27, 2.25), 'steel_dark', bevel=.03)
for x in (-1.78, 1.78):
    m.box((.26, .24, .62), (x, 1.23, 1.91), 'steel_dark', bevel=.03)
m.box((3.4, .22, .08), (0, 1.17, 1.60), 'steel_dark', bevel=.03)
# grille and bull-bar ram
m.box((3.0, .2, .6), (0, 2.98, 1.12), 'steel_dark', bevel=.03)
for x in (-1.1, -.55, 0, .55, 1.1):
    m.box((.08, .08, .5), (x, 3.2, 1.12), 'steel')
m.box((4.0, .3, .35), (0, 3.02, .70), 'rust_dark', bevel=.05)
m.box((3.9, .3, .16), (0, 3.05, 1.55), 'rust', bevel=.03)
for x in (-1.9, -.65, .65, 1.9):
    m.box((.22, .34, 1.0), (x, 3.06, 1.12), 'steel_dark', bevel=.04)
m.box((3.0, .30, .14), (0, 3.10, .92), 'steel_dark', bevel=.03)
# headlights
for x in (-1.5, 1.5):
    m.box((.45, .16, .3), (x, 3.10, 1.42), 'lamp_warm', bevel=.03)
    m.box((.6, .06, .42), (x, 3.07, 1.42), 'steel_dark', bevel=.02)
# rear doors at -Y
m.box((3.6, .1, 1.1), (0, -2.9, 1.40), 'steel_dark', bevel=.02)
m.box((.1, .16, 1.1), (0, -2.93, 1.40), 'black')
for x in (-1.4, 1.4):
    for z in (1.0, 1.8):
        m.box((.18, .12, .1), (x, -2.98, z), 'chrome')
m.box((3.9, .3, .22), (0, -3.02, .62), 'steel_dark', bevel=.04)  # rear bumper
for x in (-1.5, 1.5):
    m.box((.3, .06, .2), (x, -2.98, 1.0), 'red_bright')
# big wheels: one part each, pivot at the hub, axle along X
for nm, x, y in (('Wheel_FL', -2.18, 2.05), ('Wheel_FR', 2.18, 2.05), ('Wheel_RL', -2.18, -2.0), ('Wheel_RR', 2.18, -2.0)):
    w = m.part(nm, pivot=(x, y, .72))
    w.cyl(.72, .62, (x, y, .72), 'rubber', rot=(0, 90, 0), seg=12)
    w.cyl(.42, .60, (x, y, .72), 'steel_dark', rot=(0, 90, 0), seg=10)
    w.cyl(.16, .60, (x, y, .72), 'rust', rot=(0, 90, 0), seg=8)
    sx = 1 if x > 0 else -1
    for k in range(4):
        a = math.radians(k * 90)
        w.box((.04, .08, .08), (x + sx*.285, y + .28*math.cos(a), .72 + .28*math.sin(a)), 'chrome')

# ---------------- cab interior (thin boxes: the nearest face always points at the camera) ----------------
T = .05
m.box((4.0, 1.75, T), (0, .25, .825), 'rubber')                     # floor
m.box((4.0, 1.75, T), (0, .25, 2.17), 'fabric_grey')                # headliner
m.box((4.0, T, 1.32), (0, -.62, 1.51), 'steel_dark')                # rear bulkhead (hides cargo)
# dashboard + cowl
m.box((4.0, .30, .73), (0, .99, 1.215), 'gunmetal')
m.box((1.6, .10, .22), (-.55, .81, 1.40), 'black')       # instrument binnacle
for i, gx in enumerate((-1.0, -.75, -.5, -.25)):
    m.cyl(.065, .03, (gx - .1, .755, 1.40), 'chrome', rot=(90, 0, 0), seg=8)
    m.cyl(.05, .03, (gx - .1, .74, 1.40), 'lamp_warm' if i == 3 else 'glass_dirty', rot=(90, 0, 0), seg=8)
m.box((.5, .08, .2), (.7, .82, 1.35), 'steel_dark')                  # radio
m.box((.3, .02, .06), (.7, .775, 1.38), 'hazard_yellow')
for sx in (.5, .9):
    m.cyl(.07, .04, (sx + .3, .80, 1.20), 'rust_light', rot=(90, 0, 0), seg=6)
# steering wheel: ring + 3 spokes + column, angled toward the driver
wc = Vector((-.55, .66, 1.36)); tilt = -55
R = Matrix.Rotation(math.radians(tilt), 3, 'X')
for k in range(10):
    a = math.radians(k * 36)
    pos = wc + R @ Vector((.21*math.cos(a), 0, .21*math.sin(a)))
    mm = R @ Matrix.Rotation(-a, 3, 'Y')
    e = mm.to_euler('XYZ')
    m.box((.14, .05, .05), tuple(pos), 'leather_dark', rot=tuple(math.degrees(v) for v in e))
for k in range(3):
    a = math.radians(90 + k * 120)
    d = R @ Vector((.105*math.cos(a), 0, .105*math.sin(a)))
    mm = R @ Matrix.Rotation(-a, 3, 'Y')
    e = mm.to_euler('XYZ')
    m.box((.21, .035, .035), tuple(wc + d), 'steel_dark', rot=tuple(math.degrees(v) for v in e))
m.cyl(.045, .08, tuple(wc), 'black', rot=(tilt + 90, 0, 0), seg=8)
m.cyl(.035, .35, (-.55, .78, 1.22), 'gunmetal', rot=(35, 0, 0), seg=6)
# seats (driver left, passenger right)
for sx in (-.55, .55):
    m.box((.8, .70, .20), (sx, .20, 1.05), 'leather')       # cushion
    m.box((.8, .14, .75), (sx, -.17, 1.38), 'leather', rot=(-8, 0, 0))   # back
    m.box((.42, .12, .24), (sx, -.14, 1.88), 'leather_dark')  # headrest
    m.box((.1, .06, .12), (sx, -.15, 1.76), 'steel_dark')
    m.box((.7, .6, .22), (sx, .15, .94), 'steel_dark')                  # seat base
# centre console + gear lever
m.box((.3, .9, .25), (0, .35, .98), 'steel_dark')
m.cyl(.025, .35, (0, .45, 1.28), 'chrome', seg=6, rot=(-15, 0, 0))
m.cyl(.05, .05, (0, .42, 1.46), 'black', seg=6)
# rear-view mirror at the windscreen
m.box((.03, .03, .12), (0, 1.10, 2.02), 'steel_dark')
m.box((.38, .04, .12), (0, 1.07, 1.94), 'black')
# right side wall liner with slit window opening, left wall pillars (the left door closes the rest)
m.box((T, 1.72, .60), (2.0, .26, 1.15), 'army_green_dark')          # below window (z .85..1.45)
m.box((T, 1.72, .14), (2.0, .26, 2.17), 'army_green_dark')         # above window (z 2.10..2.24)
m.box((T, .30, .65), (2.0, -.45, 1.775), 'army_green_dark')          # B pillar at window
m.box((T, .10, .65), (2.0, 1.085, 1.775), 'army_green_dark')         # A pillar at window
m.box((.45, .8, .12), (1.78, .35, 1.0), 'army_green_dark')          # door pocket
m.box((.08, .16, .05), (1.95, .1, 1.35), 'chrome')                  # handle
m.box((T, .22, 1.32), (-2.0, -.52, 1.51), 'army_green_dark')        # left B pillar liner
m.box((T, .10, 1.32), (-2.0, 1.07, 1.51), 'army_green_dark')        # left A pillar liner
m.box((T, 1.72, .14), (-2.0, .26, 2.17), 'army_green_dark')        # left header liner
m.box((T, 1.72, .05), (-2.0, .26, .875), 'army_green_dark')         # left sill liner

# left driver door: hinged at its FRONT vertical edge (y 1.05), swings about Z
hx, hy = -2.15, 1.05
d = m.part('Door_L', pivot=(hx, hy, 1.3))
d.box((.14, 1.45, .575), (-2.12, .325, 1.1625), 'steel_dark')        # lower skin z .875..1.62
d.box((.14, 1.45, .15), (-2.12, .325, 2.175), 'steel_dark')          # top z 1.76..1.80
d.box((.14, .10, .65), (-2.12, -.35, 1.775), 'steel_dark')                       # rear strip beside slit
d.box((.14, .10, .65), (-2.12, 1.0, 1.775), 'steel_dark')                        # front strip
d.box((.07, 1.40, .05), (-2.05, .325, 1.0), 'army_green_dark')                  # inner panel trim
for z in (1.0, 1.4):
    d.box((.04, .5, .04), (-2.07, .3, z), 'rust_dark')
quad(d, [(-2.195, -.3, 1.45), (-2.195, .95, 1.45), (-2.195, .95, 2.10), (-2.195, -.3, 2.10)][::-1], 'black', (-1, 0, 0))
quad(d, [(-2.196, -.22, 1.52), (-2.196, .87, 1.52), (-2.196, .87, 2.03), (-2.196, -.22, 2.03)], 'glass', (-1, 0, 0))
d.box((.07, .22, .05), (-2.22, -.15, 1.35), 'chrome')                 # outer handle
d.box((.05, .05, .20), (-2.22, -.05, 1.35), 'chrome')
d.box((.18, .035, 1.0), (-2.19, -.35, 1.25), 'rust')                            # weld seam
d.box((.08, .16, .05), (-2.0, .1, 1.35), 'chrome')                              # inner handle

# anchors (pivot position is what the game reads)
for nm, p in (('Seat', (-.55, .12, 1.93)), ('Exit_L', (-2.45, 1.2, 0)), ('Exit_R', (2.45, 1.2, 0))):
    a = m.part(nm, pivot=p)
    a.box((.01, .01, .01), (p[0], p[1], max(p[2], .005)), 'black')

# turret ring on roof + rotating head
m.cyl(.62, .12, (0, -.9, 2.18), 'steel_dark', seg=12)
h = m.part('Head', pivot=(0, -.9, 2.26))
h.box((.8, .8, .22), (0, -.9, 2.30), 'army_green_dark', bevel=.04)
h.box((.6, .5, .20), (0, -.95, 2.24), 'steel_dark', bevel=.03)
h.cyl(.07, 1.2, (0, -.35, 2.34), 'gunmetal', rot=(-90, 0, 0), seg=8)
h.cyl(.11, .22, (0, .30, 2.34), 'black', rot=(-90, 0, 0), seg=8)
h.box((.3, .3, .2), (0, -1.3, 2.36), 'rust_dark', bevel=.03)
h.box((.55, .08, .26), (0, -.52, 2.36), 'steel', bevel=.02)

finish(m, bounds={'x':(-2.5,2.5),'y':(-3.25,3.25),'z':(0,2.5)})
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).resolve().parents[1] / 'blend' / 'piece_armored_van.blend'))
