import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('lmk_hospital_ambulance', seed=1108)
# chassis and wheels (flat tyres: low, wide)
m.box((2.2, 5.4, 0.35), (0, 0, 0.5), 'gunmetal', bevel=0.02)
for sx in (-1, 1):
    for y in (-1.8, 1.7):
        m.cyl(0.36, 0.28, (sx * 1.08, y, 0.36), 'rubber', rot=(0, 90, 0), seg=10)
        m.cyl(0.2, 0.3, (sx * 1.08, y, 0.36), 'rust', rot=(0, 90, 0), seg=8)
# box body (rear, +Y) and cab (front, -Y)
m.box((2.4, 3.4, 1.9), (0, 1.1, 1.55), 'paint_white', bevel=0.05, taper=(0.96, 0.98))
m.box((2.4, 1.9, 1.0), (0, -1.85, 0.95), 'paint_white', bevel=0.05)          # hood/bonnet block
m.box((2.3, 1.2, 0.9), (0, -1.55, 1.8), 'paint_white', bevel=0.05, taper=(0.9, 0.7))   # cab
m.box((2.0, 0.06, 0.55), (0, -2.04, 1.88), 'glass', rot=(-12, 0, 0))          # windscreen (smashed)
m.box((0.05, 0.9, 0.5), (1.18, -1.5, 1.9), 'glass_dirty') ; m.box((0.05, 0.9, 0.5), (-1.18, -1.5, 1.9), 'glass_dirty')
# grille, bumper, headlamps
m.box((1.6, 0.06, 0.4), (0, -2.72, 0.8), 'gunmetal')
m.box((2.4, 0.2, 0.2), (0, -2.72, 0.45), 'steel_dark')
for sx in (-1, 1):
    m.cyl(0.11, 0.06, (sx * 0.9, -2.72, 1.1), 'glass_dirty', rot=(90, 0, 0), seg=8)
m.box((2.4, 0.2, 0.2), (0, 2.72, 0.45), 'steel_dark')
# rust and stripes
m.box((2.42, 5.0, 0.18), (0, 0.0, 1.0), 'warning_red') if False else None
m.box((2.44, 3.4, 0.2), (0, 1.1, 1.3), 'warning_red', taper=(0.96, 0.98)) if False else None
for sx in (-1, 1):
    m.box((0.03, 3.2, 0.2), (sx * 1.2, 1.1, 1.15), 'warning_red')
    m.box((0.03, 1.8, 0.2), (sx * 1.21, -1.85, 0.95), 'warning_red')
    h, w = 0.18, 0.5
    m.poly([(-w, -h), (-h, -h), (-h, -w), (h, -w), (h, -h), (w, -h), (w, h), (h, h), (h, w), (-h, w), (-h, h), (-w, h)], 0.04, (sx * 1.22, 1.1, 1.75), 'red_bright', rot=(0, 0, 90))
for i in range(8):
    sx = r = 1 if i % 2 else -1
    m.box((0.03, 0.4 + 0.15 * (i % 3), 0.35), (sx * 1.2, -0.4 + i * 0.4, 1.6 + 0.1 * (i % 2)), 'rust' if i % 3 else 'rust_dark')
m.box((1.0, 0.04, 0.6), (0, -2.0 + 0, 1.5), 'rust_dark', rot=(0, 0, 0)) if False else None
m.box((1.5, 0.8, 0.02), (0.1, -2.2, 1.455), 'rust') if False else None
m.box((1.0, 1.0, 0.04), (0, 1.1, 2.47), 'rust_dark', rot=(0, 0, 8))
# heavy weathering: roof/hood rust, grime skirts, dented panel
m.box((1.6, 1.8, 0.04), (0.4, 1.6, 2.47), 'rust')
m.box((1.2, 1.0, 0.03), (-0.5, 0.2, 2.47), 'rust_dark')
m.box((1.4, 0.9, 0.03), (0.3, -2.3, 1.46), 'rust')
for sx in (-1, 1):
    m.box((0.03, 3.4, 0.3), (sx * 1.2, 1.1, 0.8), 'rust_dark')
    m.box((0.03, 1.8, 0.25), (sx * 1.21, -1.85, 0.7), 'rust_dark')
    m.box((0.04, 0.8, 0.5), (sx * 1.2, 2.2, 1.9), 'plaster_dirty')
m.box((0.8, 0.04, 0.5), (-0.6, -2.74, 1.0), 'rust_dark')
# rear: doors (open slightly), roof light
m.box((2.2, 0.06, 1.6), (0, 2.77, 1.55), 'steel')
m.box((0.04, 0.07, 1.6), (0, 2.79, 1.55), 'gunmetal')
m.box((1.7, 0.08, 0.5), (0, 2.88, 1.85), 'glass_dirty') if False else None
m.box((0.9, 0.3, 0.14), (0, -0.9, 2.45), 'red_bright', bevel=0.02) if False else None
m.cyl(0.12, 0.2, (-0.5, -1.4, 2.35), 'warning_red', seg=8)
m.cyl(0.12, 0.2, (0.5, -1.4, 2.35), 'plastic_blue', seg=8)
m.box((1.4, 0.2, 0.08), (0, -1.45, 2.3), 'steel_dark')
finish(m, bounds={'x': (-1.25, 1.25), 'y': (-2.85, 2.85), 'z': (-0.01, 2.52)})
