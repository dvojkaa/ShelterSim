"""Wall-mounted shelf. Origin at wall/floor, wall at y=0. Occupies z 1.2..2.3 so head height stays clear below."""
from ss_art import Model, finish
m = Model('bunker_shelf', seed=91)
S1, S2 = 1.42, 1.88   # top surfaces of the two boards
SEG = 7


def can(x, y, z, r, h, label, body='steel', tilt=(0, 0, 0)):
    """Label cylinder with a slimmer steel core poking out at both rims."""
    # Same segment count and orientation, so the label nests cleanly; open-ended because only its wall shows.
    m.cyl(r - 0.003, h, (x, y, z + h / 2), body, seg=SEG, rot=tilt)
    m.cyl(r + 0.002, h * 0.76, (x, y, z + h / 2), label, seg=SEG, rot=tilt, caps=False)


def jar(x, y, z, r, h, fill, lid='rust'):
    m.cyl(r, h * 0.56, (x, y, z + h * 0.28), fill, seg=SEG, caps=False)
    m.cyl(r, h * 0.28, (x, y, z + h * 0.70), 'glass_dirty', seg=SEG, r2=r * 0.8, caps=False)
    m.cyl(r * 0.88, h * 0.16, (x, y, z + h * 0.92), lid, seg=6)


def board(z_top):
    # Three slightly mismatched planks plus a steel lip
    m.box((1.40, 0.11, 0.04), (0, -0.065, z_top - 0.02), 'wood')
    m.box((1.37, 0.11, 0.04), (0.012, -0.175, z_top - 0.02), 'wood_dark')
    m.box((1.40, 0.11, 0.04), (0, -0.285, z_top - 0.02), 'wood_grey')
    m.box((1.40, 0.006, 0.058), (0, -0.341, z_top - 0.029), 'steel_dark')
    for x in (-0.55, 0.55):
        m.box((0.05, 0.31, 0.025), (x, -0.165, z_top - 0.052), 'gunmetal')
        m.tube([(x, -0.02, z_top - 0.2), (x, -0.27, z_top - 0.055)], 0.012, 'gunmetal', seg=4)


board(S1)
board(S2)
# Wall uprights
for x in (-0.55, 0.55):
    m.box((0.06, 0.02, 1.08), (x, -0.01, 1.74), 'steel_dark')
    for z in (1.27, 2.22):
        m.cyl(0.012, 0.012, (x, -0.024, z), 'chrome', rot=(90, 0, 0), seg=4)

# --- Radio on the top shelf ---
RX, RY = -0.27, -0.185
RZ = S2 + 0.02
m.box((0.42, 0.21, 0.25), (RX, RY, RZ + 0.125), 'army_green', bevel=0.012)
m.box((0.40, 0.006, 0.23), (RX, RY - 0.106, RZ + 0.125), 'army_green_dark')
for x in (-0.15, 0.15):
    m.box((0.05, 0.16, 0.02), (RX + x, RY, S2 + 0.01), 'rubber')
# Speaker
SX, SZ = RX - 0.095, RZ + 0.115
m.cyl(0.082, 0.006, (SX, RY - 0.111, SZ), 'gunmetal', rot=(90, 0, 0), seg=12)
for i in range(4):
    m.box((0.11 - abs(i - 1.5) * 0.022, 0.004, 0.012), (SX, RY - 0.116, SZ - 0.048 + i * 0.032), 'steel')
# Tuning window and needle
m.box((0.155, 0.006, 0.070), (RX + 0.095, RY - 0.111, RZ + 0.175), 'lamp_warm')
for i in range(3):
    m.box((0.006, 0.003, 0.018), (RX + 0.035 + i * 0.06, RY - 0.1155, RZ + 0.190), 'black')
m.box((0.006, 0.003, 0.050), (RX + 0.12, RY - 0.1158, RZ + 0.175), 'warning_red')
# Knobs
for x, r in ((0.045, 0.024), (0.105, 0.024), (0.155, 0.015)):
    m.cyl(r, 0.022, (RX + x, RY - 0.119, RZ + 0.065), 'chrome', rot=(90, 0, 0), seg=6)
# Carry handle, antenna, wear
HZ = RZ + 0.25
m.tube([(RX - 0.14, RY, HZ - 0.005), (RX - 0.12, RY, HZ + 0.035), (RX + 0.08, RY, HZ + 0.035), (RX + 0.10, RY, HZ - 0.005)], 0.010, 'leather_dark', seg=4)
m.tube([(RX + 0.17, RY + 0.06, HZ - 0.01), (RX + 0.27, RY + 0.09, 2.285)], 0.005, 'chrome', seg=4)
m.box((0.09, 0.004, 0.06), (RX + 0.165, RY - 0.1075, RZ + 0.018), 'rust')
m.box((0.05, 0.004, 0.04), (RX - 0.16, RY - 0.1075, RZ + 0.222), 'rust_dark')

# --- Top shelf right: jars, stacked cans and an ammo-style tin ---
jar(0.08, -0.17, S2, 0.055, 0.21, 'orange')
jar(0.215, -0.13, S2, 0.05, 0.18, 'berry_blue', lid='steel')
jar(0.20, -0.26, S2, 0.042, 0.15, 'leaf_dry', lid='rust_dark')
can(0.34, -0.11, S2, 0.046, 0.13, 'warning_red')
can(0.34, -0.11, S2 + 0.13, 0.046, 0.13, 'mustard')
m.box((0.20, 0.13, 0.09), (0.53, -0.14, S2 + 0.045), 'steel_dark', rot=(0, 0, 8))
m.box((0.205, 0.135, 0.015), (0.53, -0.14, S2 + 0.097), 'army_green_dark', rot=(0, 0, 8))
m.box((0.05, 0.145, 0.03), (0.53, -0.14, S2 + 0.06), 'rust_dark', rot=(0, 0, 8))

# --- Lower shelf: pyramid of food cans and big jars ---
R, H, BY = 0.05, 0.12, -0.2
for x, lab in ((-0.62, 'army_green'), (-0.51, 'paint_white'), (-0.40, 'warning_red')):
    can(x, BY, S1, R, H, lab)
for x, lab in ((-0.565, 'mustard'), (-0.455, 'plastic_blue')):
    can(x, BY, S1 + H, R, H, lab)
can(-0.51, BY, S1 + 2 * H, R, H, 'car_red', body='rust_dark')
# A knocked-over can rolled to the front of the shelf (axis along X so it lies flat on the board)
m.cyl(R - 0.003, H, (-0.24, -0.25, S1 + R - 0.003), 'steel', seg=SEG, rot=(0, 90, 0))
m.cyl(R + 0.002, H * 0.76, (-0.24, -0.25, S1 + R - 0.003), 'paint_green', seg=SEG, rot=(0, 90, 0), caps=False)

jar(0.0, -0.14, S1, 0.072, 0.26, 'leaf_green')
jar(0.145, -0.205, S1, 0.058, 0.20, 'orange', lid='steel_dark')
jar(0.03, -0.27, S1, 0.04, 0.14, 'skin_dark', lid='rust_dark')
can(0.30, -0.18, S1, 0.05, H, 'hazard_yellow')
# Battered paint tin with bail handle and a paint run
m.cyl(0.075, 0.15, (0.49, -0.19, S1 + 0.075), 'steel_dark', seg=8)
m.cyl(0.078, 0.012, (0.49, -0.19, S1 + 0.145), 'chrome', seg=8)
m.box((0.05, 0.03, 0.09), (0.49, -0.255, S1 + 0.08), 'warning_red')
m.tube([(0.415, -0.19, S1 + 0.12), (0.45, -0.19, S1 + 0.20), (0.53, -0.19, S1 + 0.20), (0.565, -0.19, S1 + 0.12)], 0.005, 'steel', seg=4)

# Rag draped over the lower shelf edge (stays above the 1.2 clear line)
m.poly([(-0.10, S1 + 0.008), (0.09, S1 + 0.008), (0.09, 1.31), (0.05, 1.26), (0.02, 1.30), (-0.03, 1.24), (-0.09, 1.29)],
       0.008, (0, -0.346, 0), 'fabric_stain')
m.box((0.19, 0.08, 0.008), (0, -0.31, S1 + 0.004), 'fabric_stain')

finish(m, bounds={'x': (-0.7, 0.7), 'y': (-0.35, 0), 'z': (1.2, 2.3)})
