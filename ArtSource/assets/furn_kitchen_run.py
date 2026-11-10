import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('furn_kitchen_run', seed=101)
FY = -0.29            # carcass front face; doors occupy -0.315..-0.29, handles reach -0.325
BY, BH = 0.325, 0.9   # back wall, base height
# --- base carcass: toe kick, side/divider panels, back, worktop
m.box((2.4, 0.55, 0.1), (0, 0.04, 0.05), 'gunmetal')
m.box((2.4, 0.615, 0.8), (0, (FY + BY) / 2, 0.5), 'paint_white', bevel=0.01)
for x in (-1.19, -0.1, 0.1, 1.19):   # dark openings shown only as divider lines
    m.box((0.02, 0.01, 0.8), (x, FY - 0.001, 0.5), 'steel_dark')
m.box((0.2, 0.01, 0.74), (0.0, FY - 0.003, 0.5), 'wood_dark')   # narrow bottle cupboard (fixed)
m.box((0.16, 0.01, 0.1), (0.0, FY - 0.008, 0.72), 'steel_dark')
m.box((2.4, 0.65, 0.05), (0, 0, 0.925), 'wood', bevel=0.008)      # worktop to full depth
m.box((2.38, 0.03, 0.04), (0, -0.31, 0.93), 'wood_dark')         # worn front lip
m.box((2.4, 0.03, 0.18), (0, 0.31, 1.04), 'plaster_dirty')        # splash back
# stained / chipped patches on worktop
m.box((0.5, 0.3, 0.004), (-0.6, -0.05, 0.952), 'fabric_stain')
m.box((0.3, 0.2, 0.004), (-0.1, 0.1, 0.952), 'wood_dark')
# --- sink set into worktop, right pair
m.box((0.62, 0.42, 0.012), (0.65, -0.02, 0.953), 'steel')
m.box((0.52, 0.32, 0.014), (0.65, -0.02, 0.954), 'steel_dark')
m.box((0.2, 0.15, 0.016), (0.58, -0.01, 0.955), 'rust_dark')
m.tube([(0.65, 0.24, 0.95), (0.65, 0.24, 1.17), (0.65, 0.08, 1.2), (0.65, 0.05, 1.12)], 0.016, 'chrome', seg=5)
m.box((0.05, 0.05, 0.05), (0.52, 0.24, 0.98), 'warning_red', bevel=0.01)
m.box((0.05, 0.05, 0.05), (0.78, 0.24, 0.98), 'plastic_blue', bevel=0.01)
# --- hob/jar clutter on worktop
m.cyl(0.07, 0.16, (-0.95, 0.15, 1.03), 'glass_dirty', seg=8)
m.cyl(0.075, 0.03, (-0.95, 0.15, 1.12), 'rust', seg=8)
m.cyl(0.12, 0.1, (-0.5, 0.0, 0.99), 'steel_dark', seg=9, r2=0.1)  # pot
m.box((0.3, 0.02, 0.03), (-0.5, 0.0, 1.05), 'black') if False else None
# --- wall cupboards (z 1.4..2.2), two bays, open gap in the middle
def wallcab(x0, x1, tag):
    w = x1 - x0; cx = (x0 + x1) / 2
    m.box((w, 0.33, 0.8), (cx, 0.16, 1.8), 'paint_white' if tag else 'plaster_dirty', bevel=0.01)
    m.box((w, 0.33, 0.04), (cx, 0.16, 2.18), 'steel_dark')
    n = 2
    for i in range(n):
        dx0 = x0 + w / n * i
        m.box((w / n - 0.02, 0.025, 0.76), (dx0 + w / n / 2, -0.0165, 1.8), 'paint_green' if i == 0 else 'paint_white', bevel=0.006)
        hx = dx0 + (w / n - 0.07 if i == 0 else 0.05)
        m.box((0.025, 0.025, 0.16), (hx, -0.04, 1.62), 'chrome')
wallcab(-1.2, -0.12, True)
wallcab(0.45, 1.2, False)
m.box((0.5, 0.25, 0.04), (0.0, 0.2, 2.0), 'steel_dark') if False else None
# extractor hood in the gap with pipe
m.box((0.4, 0.22, 0.12), (-0.0, 0.2, 1.75), 'steel_dark', taper=(0.8, 0.8)) if False else None
m.box((0.5, 0.3, 0.1), (0.165, 0.17, 1.55), 'steel_dark', taper=(0.6, 0.7))
m.tube([(0.165, 0.2, 1.6), (0.165, 0.2, 2.2)], 0.05, 'steel', seg=6)
# rust drip and grime on cabinets
m.box((0.2, 0.01, 0.4), (-0.55, FY - 0.0015, 0.35), 'rust_dark') if False else None
# --- base doors (movable parts)
def door(name, x_hinge, sign, col, handle_col='chrome'):
    wd = 0.55
    p = m.part(name, pivot=(x_hinge, FY - 0.0125, 0.5))
    cx = x_hinge + sign * wd / 2
    p.box((wd - 0.012, 0.025, 0.78), (cx, FY - 0.0125, 0.5), col, bevel=0.006)
    # raised panel
    p.box((wd - 0.14, 0.008, 0.6), (cx, -0.3185, 0.5), 'steel_dark' if col == 'rust' else 'paint_white')
    hx = x_hinge + sign * (wd - 0.06)
    p.box((0.03, 0.02, 0.2), (hx, FY - 0.0275 + 0.0, 0.72), handle_col) if False else None
    p.box((0.028, 0.02, 0.22), (hx, -0.315, 0.72), handle_col)
    p.box((0.028, 0.012, 0.02), (hx, -0.305, 0.82), 'steel_dark')
    p.box((0.028, 0.012, 0.02), (hx, -0.305, 0.62), 'steel_dark')
    return p
d1 = door('Door_1', -1.2, +1, 'paint_white')
d2 = door('Door_2', -0.1, -1, 'paint_green')
d3 = door('Door_3', 0.1, +1, 'paint_green')
d4 = door('Door_4', 1.2, -1, 'paint_white')
# grime/rust lower edge of doors
d1.box((0.5, 0.006, 0.1), (-0.93, -0.3185, 0.15), 'rust_dark')
d3.box((0.4, 0.006, 0.08), (0.35, -0.3185, 0.14), 'rust_dark')
d4.box((0.3, 0.006, 0.12), (1.0, -0.3185, 0.16), 'rust')
d2.box((0.12, 0.006, 0.3), (-0.5, -0.3185, 0.55), 'fabric_stain')
finish(m, bounds={'x': (-1.2, 1.2), 'y': (-0.325, 0.325), 'z': (0, 2.2)})
