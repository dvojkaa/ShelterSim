import math, random
from ss_art import Model, finish
m = Model('bld_stairs_house', seed=71)
r = random.Random(7101)

N = 16
RISE = 2.9 / N          # 0.18125, top tread surface exactly z=2.9
RUN = 0.25
Y0 = -2.0
ANG = math.degrees(math.atan2(RISE, RUN))
TH = 0.05               # tread thickness
HX = 0.7                # half width
CRACK = 9
WORN = {2, 5, 6, 10, 12, 13}

for i in range(N):
    ytop = Y0 + i * RUN
    ztop = (i + 1) * RISE
    c = 'wood_grey' if i in WORN else ('wood' if i % 2 else 'wood_light')
    yc = ytop + RUN / 2 - 0.01
    if i == CRACK:
        # split tread: one half sagged and tilted, dark gap between
        m.box((0.62, RUN + 0.03, TH), (-0.37, yc, ztop - TH / 2), c)
        m.box((0.62, RUN + 0.03, TH), (0.38, yc - 0.015, ztop - TH / 2 - 0.035), 'wood_dark', rot=(5, 0, 0))
        m.box((0.05, RUN, 0.01), (0.04, yc, ztop - 0.012), 'black')
        m.box((0.07, 0.03, 0.004), (0.0, yc + 0.04, ztop + 0.0), 'black')
    else:
        m.box((1.24, RUN + 0.03, TH), (0, yc, ztop - TH / 2), c)
        if i in WORN:  # worn dip in the middle
            m.box((0.5, RUN * 0.55, 0.006), (0.0, yc, ztop + 0.002), 'wood_dark')
    # riser
    m.box((1.2, 0.02, RISE - TH + 0.01), (0, ytop - 0.005, ztop - TH - (RISE - TH) / 2 + 0.01), 'wood_dark')

# stringers: open-string planks along the nose line (profile in y,z, extruded along x)
def line_z(y):
    return RISE + (y + 2.0) * RISE / RUN
for sx, c in ((-1, 'wood_dark'), (1, 'wood')):
    y_lo = Y0 + (0.40 - RISE) / (RISE / RUN)
    prof = [(Y0, 0.0), (y_lo, 0.0), (2.0, line_z(2.0) - 0.40), (2.0, line_z(2.0) - 0.04), (Y0, line_z(Y0) - 0.04)]
    m.poly(prof, 0.06, (sx * 0.65, 0, 0), c, rot=(0, 0, 90))
# base plate and top ledger
m.box((1.3, 0.1, 0.06), (0, Y0 + 0.02, 0.03), 'wood_dark')
m.box((1.3, 0.08, 0.1), (0, 2.0 - 0.04, 2.9 - 0.16), 'wood_dark')

# handrail on +X: posts on every 3rd step, sloped rail 0.9 above the nose line
HR = 0.95
posts = [0, 3, 6, 9, 12, 15]
for i in posts:
    y = Y0 + i * RUN + RUN / 2
    zt = (i + 1) * RISE
    m.box((0.06, 0.06, HR), (0.665, y, zt + HR / 2 - 0.04), 'wood_dark')
yA, zA = Y0 + RUN / 2, RISE + HR - 0.04
yB, zB = Y0 + 15 * RUN + RUN / 2, 16 * RISE + HR - 0.04
Lr = math.hypot(yB - yA, zB - zA) + 0.1
m.box((0.07, Lr, 0.05), (0.665, (yA + yB) / 2, (zA + zB) / 2 + 0.02), 'wood_light', rot=(ANG, 0, 0))
# mid rail with a snapped section, and a loose spindle
m.box((0.035, Lr * 0.55, 0.04), (0.665, (yA + yB) / 2 - 0.5, (zA + zB) / 2 - 0.42 - 0.5 * math.tan(math.radians(ANG)) * 0 - 0.0), 'wood_grey', rot=(ANG, 0, 0))
for i in (1, 4, 7, 10, 13):
    if True:
        y = Y0 + i * RUN + RUN / 2
        zt = (i + 1) * RISE
        m.box((0.025, 0.025, 0.5), (0.665, y, zt + 0.25 - 0.03), 'wood_grey')

finish(m, bounds={'x': (-0.7, 0.7), 'y': (-2.03, 2.1), 'z': (0, 3.9)})
