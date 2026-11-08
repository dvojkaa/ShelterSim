"""Masked wasteland raider. Faces +Y, origin at feet. Six separately pivoted parts: runtime swings them by name."""
import math
from mathutils import Vector
from ss_art import Model, finish

m = Model('raider', seed=23)


def seg(p, a, b, ra, rb, color, n=6, caps=False):
    """Tapered limb segment from world point a (radius ra) to b (radius rb). Ends are buried in the next mass,
    so caps stay off by default: the budget goes on the silhouette instead."""
    a, b = Vector(a), Vector(b)
    d = b - a
    e = d.to_track_quat('Z', 'Y').to_euler()
    p.cyl(ra, d.length, (a + b) / 2, color, rot=tuple(math.degrees(x) for x in e), seg=n, r2=rb, caps=caps)


# ---------------------------------------------------------------- Torso
t = m.part('Torso', pivot=(0, 0, 1.0))
# Body masses: wide hips, narrow waist, V chest.
t.box((0.38, 0.25, 0.20), (0, 0, 0.98), 'leather_dark', bevel=0.02)
t.box((0.31, 0.21, 0.24), (0, 0, 1.15), 'leather', taper=(1.22, 1.14))
t.box((0.38, 0.24, 0.30), (0, 0, 1.38), 'leather', taper=(1.26, 1.14), bevel=0.02)
t.box((0.46, 0.26, 0.10), (0, -0.005, 1.56), 'leather_dark', taper=(0.5, 0.66))
# Red neck rag bunched under the mask, tail hanging on the left chest.
t.cyl(0.135, 0.09, (0, 0.0, 1.58), 'warning_red', seg=7, r2=0.095)
t.poly([(-0.07, 0.0), (-0.015, -0.05), (0.0, -0.17), (0.03, -0.11), (0.065, -0.14), (0.085, 0.0)], 0.022,
       (-0.06, 0.15, 1.56), 'warning_red', rot=(4, 0, 0))
# Scrap chest plate: jagged cut sheet, bolted on.
t.poly([(-0.165, -0.13), (-0.04, -0.15), (0.11, -0.12), (0.175, -0.05), (0.19, 0.12), (0.06, 0.135), (-0.09, 0.11), (-0.185, 0.13)], 0.03,
       (0.0, 0.142, 1.34), 'rust', rot=(-3, 0, 0))
t.poly([(0.02, -0.03), (0.17, -0.05), (0.175, 0.03), (0.03, 0.05)], 0.036, (0.0, 0.142, 1.25), 'mustard_dark', rot=(-3, 0, 0))
for x, z in ((-0.16, 1.45), (0.15, 1.31)):
    t.box((0.022, 0.022, 0.022), (x, 0.162, z), 'steel', rot=(0, 45, 0))
# Bandolier over the plate, right shoulder to left hip, with shells.
t.box((0.058, 0.08, 0.62), (0.015, 0.143, 1.28), 'black', rot=(0, -34, 0))
t.box((0.058, 0.06, 0.62), (0.015, -0.127, 1.28), 'black', rot=(0, 34, 0))
for i in range(3):
    k = -0.10 + i * 0.09
    t.box((0.03, 0.02, 0.05), (0.015 + k * math.sin(math.radians(34)), 0.186, 1.28 + k * math.cos(math.radians(34))), 'bone', rot=(0, -34, 0))
# Back: stitched patch, scrap spine plate, cross strap already above.
t.box((0.14, 0.018, 0.14), (0.10, -0.128, 1.22), 'wood_grey', rot=(0, 8, 0))
t.poly([(-0.10, -0.10), (0.09, -0.12), (0.12, 0.09), (-0.03, 0.12), (-0.13, 0.07)], 0.03, (-0.02, -0.14, 1.40), 'rust_dark', rot=(3, 0, 0))
# Belt, buckle, pouches, knife sheath.
t.box((0.39, 0.265, 0.058), (0, 0, 1.055), 'black')
t.box((0.08, 0.02, 0.07), (0, 0.14, 1.055), 'steel')
t.box((0.09, 0.075, 0.10), (-0.19, 0.04, 1.0), 'leather_dark')
t.box((0.075, 0.07, 0.085), (0.19, -0.05, 1.0), 'army_green_dark')
# Tattered red loin rag (front) and leather tail (rear), short enough for the legs to swing past.
t.poly([(-0.10, 0.0), (-0.11, -0.13), (-0.07, -0.22), (-0.035, -0.15), (0.0, -0.30), (0.05, -0.19), (0.09, -0.24), (0.105, 0.0)], 0.024,
       (0, 0.14, 1.04), 'warning_red', rot=(5, 0, 0))
t.poly([(-0.15, 0.0), (-0.16, -0.14), (-0.10, -0.26), (-0.04, -0.17), (0.03, -0.31), (0.09, -0.2), (0.15, -0.24), (0.15, 0.0)], 0.024,
       (0, -0.14, 1.04), 'leather_dark', rot=(-6, 0, 0))

# ---------------------------------------------------------------- Head
h = m.part('Head', pivot=(0, 0, 1.55))
seg(h, (0, -0.005, 1.44), (0, 0.0, 1.66), 0.078, 0.07, 'cloth_dark', 6)
# Hood: drape over the collar, rounded skull.
seg(h, (0, -0.02, 1.59), (0, -0.015, 1.74), 0.15, 0.135, 'cloth_dark', 7, caps=True)
h.blob((0.27, 0.30, 0.31), (0, -0.01, 1.755), 'cloth_dark', rough=0.035, subdiv=2)
# Angled two-plate welded mask with a centre ridge.
MY, MZ, ANG = 0.170, 1.72, 26
COS, SIN = math.cos(math.radians(ANG)), math.sin(math.radians(ANG))
PLATE = [(0, -0.135), (0.055, -0.115), (0.105, -0.045), (0.128, 0.035), (0.098, 0.11), (0, 0.125)]
EYE = [(0.018, -0.022), (0.092, -0.005), (0.098, 0.05), (0.03, 0.022)]
MOUTH = [(0.0, -0.10), (0.05, -0.085), (0.075, -0.045), (0.0, -0.058)]
BROW = [(0.0, 0.052), (0.104, 0.082), (0.098, 0.115), (0.0, 0.127)]


def mirror(pts, s):
    return pts if s > 0 else [(-x, z) for x, z in reversed(pts)]


def plate_pos(s, x, z, out=0.0):
    """World position of a point on the mask plate on side s, x along the plate, out along its normal."""
    return (s * (x * COS + out * SIN), MY - x * SIN + out * COS, MZ + z)


for s in (-1, 1):
    rot = (0, 0, -s * ANG)
    loc = (0, MY, MZ)
    h.poly(mirror(PLATE, s), 0.028, loc, 'bone', rot=rot)
    h.poly(mirror(BROW, s), 0.04, loc, 'rust_dark', rot=rot)
    h.poly(mirror(EYE, s), 0.036, loc, 'black', rot=rot)
    h.poly(mirror(MOUTH, s), 0.034, loc, 'black', rot=rot)
    # Glint deep in the eye socket: hostile from across the yard.
    h.box((0.022, 0.008, 0.016), plate_pos(s, 0.062, 0.018, 0.018), 'red_bright', rot=rot)
    # Bolted-on tooth bars across the mouth slot.
    for x, top in ((0.022, -0.052), (0.052, -0.058)):
        h.box((0.011, 0.012, 0.062), plate_pos(s, x, top - 0.028, 0.018), 'steel', rot=rot)
    h.box((0.02, 0.012, 0.02), plate_pos(s, 0.098, 0.095, 0.022), 'steel', rot=rot)
# Nose ridge weld.
h.box((0.024, 0.03, 0.15), (0, MY + 0.008, MZ + 0.03), 'rust')
# Crest of rusty spikes on a steel strap, front to back.
def crest_z(dy):
    return 1.755 + 0.175 * math.sqrt(max(0.0, 1 - (dy / 0.165) ** 2))


h.poly([(-0.10, 1.76), (0.10, 1.76), (0.14, crest_z(0.14)), (0.07, crest_z(0.07)), (-0.07, crest_z(0.07)), (-0.14, crest_z(0.14))],
       0.045, (0, -0.01, 0), 'steel_dark', rot=(0, 0, 90))
for dy, hgt, r in ((0.105, 0.06, 0.021), (0.05, 0.075, 0.024), (-0.01, 0.075, 0.025), (-0.07, 0.07, 0.024)):
    z0 = crest_z(dy) - 0.015
    h.cyl(r, hgt, (0, -0.01 + dy, z0 + hgt / 2), 'rust_light', seg=5, r2=0.002, caps=False)

# ---------------------------------------------------------------- Left arm: hanging, big spiked pad
al = m.part('Arm_L', pivot=(-0.28, 0, 1.45))
al.blob((0.19, 0.19, 0.19), (-0.28, 0, 1.45), 'leather_dark', rough=0.02, subdiv=1)
seg(al, (-0.283, 0, 1.49), (-0.30, 0, 1.12), 0.085, 0.06, 'leather', 7)
seg(al, (-0.30, 0, 1.15), (-0.31, 0.012, 0.865), 0.064, 0.046, 'cloth_dark', 7)
al.box((0.085, 0.05, 0.13), (-0.30, -0.055, 1.12), 'steel_dark')                    # elbow cop
seg(al, (-0.303, 0.005, 1.05), (-0.31, 0.012, 0.89), 0.07, 0.058, 'leather_dark', 7)             # bracer
al.box((0.10, 0.115, 0.125), (-0.313, 0.015, 0.812), 'black', taper=(0.86, 0.9))                        # fist
al.box((0.04, 0.05, 0.05), (-0.263, 0.05, 0.82), 'leather_dark')                                 # thumb
# Layered pad: rust cap, tyre-rubber lower plate, red-tipped steel spikes.
al.box((0.25, 0.27, 0.05), (-0.325, 0, 1.475), 'rubber', rot=(0, -24, 0))
al.box((0.25, 0.29, 0.09), (-0.32, 0, 1.54), 'rust', rot=(0, -24, 0), bevel=0.018, taper=(0.76, 0.8))
for y, a, b in ((-0.085, (-0.36, -0.085, 1.56), (-0.46, -0.10, 1.70)),
                (0.0, (-0.34, 0.0, 1.57), (-0.42, 0.0, 1.75)),
                (0.085, (-0.36, 0.085, 1.56), (-0.46, 0.10, 1.70))):
    seg(al, a, b, 0.032, 0.003, 'steel', 5)

# ---------------------------------------------------------------- Right arm: bent, fist at the weapon point
HAND = (0.32, 0.25, 1.05)
ELBOW = (0.31, -0.03, 1.15)
ar = m.part('Arm_R', pivot=(0.28, 0, 1.45))
ar.blob((0.18, 0.18, 0.18), (0.28, 0, 1.45), 'leather_dark', rough=0.02, subdiv=1)
seg(ar, (0.283, 0, 1.49), ELBOW, 0.08, 0.06, 'leather', 7)
seg(ar, (0.31, -0.04, 1.155), (0.318, 0.20, 1.07), 0.064, 0.047, 'cloth_dark', 7)
ar.box((0.085, 0.07, 0.10), (0.31, -0.075, 1.15), 'steel_dark')                     # elbow cop
seg(ar, (0.313, 0.03, 1.13), (0.318, 0.185, 1.075), 0.07, 0.058, 'leather_dark', 7)              # bracer
ar.box((0.105, 0.125, 0.115), HAND, 'black', rot=(-18, 0, 0))                         # fist
ar.box((0.05, 0.05, 0.04), (0.285, 0.26, 1.098), 'leather_dark', rot=(-18, 0, 0))                # thumb over the grip
# Small leather pad, one spike, and the red rag knotted round the bicep.
ar.box((0.21, 0.23, 0.05), (0.31, 0, 1.53), 'leather_dark', rot=(0, 22, 0), taper=(0.8, 0.84))
ar.box((0.18, 0.2, 0.02), (0.31, 0, 1.56), 'steel_dark', rot=(0, 22, 0), taper=(0.76, 0.8))
seg(ar, (0.33, 0, 1.56), (0.40, 0, 1.67), 0.024, 0.003, 'steel', 5)
seg(ar, (0.292, -0.005, 1.33), (0.298, -0.012, 1.25), 0.082, 0.076, 'warning_red', 7)
ar.poly([(-0.03, 0.0), (0.03, 0.0), (0.042, -0.13), (0.01, -0.09), (-0.02, -0.16)], 0.016, (0.378, -0.01, 1.28), 'warning_red', rot=(0, 0, 90))

# ---------------------------------------------------------------- Legs
ll = m.part('Leg_L', pivot=(-0.12, 0, 0.95))
lr = m.part('Leg_R', pivot=(0.12, 0, 0.95))
for p, s in ((ll, -1), (lr, 1)):
    p.blob((0.21, 0.22, 0.21), (s * 0.12, 0, 0.96), 'army_green_dark', rough=0.02, subdiv=1)
    seg(p, (s * 0.12, 0, 1.02), (s * 0.135, 0.005, 0.55), 0.108, 0.072, 'army_green', 7)
    seg(p, (s * 0.135, 0.005, 0.58), (s * 0.15, -0.008, 0.24), 0.074, 0.062, 'army_green', 7)
    # Knee cop.
    p.box((0.125, 0.05, 0.13), (s * 0.135, 0.07, 0.55), 'leather_dark', taper=(0.82, 0.8))
    # Boot: ankle cuff, foot, steel toe, heavy sole.
    p.box((0.135, 0.17, 0.20), (s * 0.15, -0.005, 0.20), 'leather_dark', taper=(1.03, 1.0))
    p.box((0.138, 0.30, 0.10), (s * 0.15, 0.05, 0.085), 'leather_dark', taper=(0.96, 0.9))
    p.box((0.12, 0.08, 0.075), (s * 0.15, 0.17, 0.075), 'steel_dark', taper=(0.9, 0.7))
    p.box((0.15, 0.32, 0.038), (s * 0.15, 0.05, 0.019), 'black')
# Left leg: scrap shin guard strapped on, thigh rope.
ll.box((0.105, 0.03, 0.27), (-0.147, 0.08, 0.36), 'rust', rot=(-3, 0, 0))
for z in (0.42,):
    seg(ll, (-0.146, 0.0, z - 0.012), (-0.146, 0.0, z + 0.012), 0.083, 0.083, 'black', 7)
seg(ll, (-0.128, 0, 0.84), (-0.13, 0, 0.80), 0.104, 0.100, 'rope', 7)
# Right leg: cloth-wrapped shin, thigh pouch, knotted rag.
for i, z in enumerate((0.45, 0.37, 0.29)):
    seg(lr, (0.146, -0.008, z - 0.03), (0.147, -0.007, z + 0.03), 0.08 - i * 0.004, 0.081 - i * 0.004,
        'fabric_stain' if i % 2 == 0 else 'fabric_grey', 7)
seg(lr, (0.127, 0, 0.77), (0.129, 0, 0.73), 0.098, 0.094, 'black', 7)
lr.box((0.05, 0.13, 0.15), (0.225, 0.0, 0.71), 'leather_dark')

finish(m, bounds={'x': (-0.5, 0.5), 'y': (-0.5, 0.5), 'z': (0, 2.0)})
