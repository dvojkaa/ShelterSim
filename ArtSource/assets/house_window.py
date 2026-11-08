import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
def build(name, broken):
    m = Model(name, seed=32)
    W, Hh, z0 = 1.6, 1.5, 0.0
    m.box((1.5, 0.12, 0.08), (0, 0.0, 0.5), 'paint_white')  # lower frame (above box)
    for s in (-1, 1): m.box((0.1, 0.12, 1.0), (s * 0.65, 0, 0.99), 'paint_white')
    m.box((1.2, 0.12, 0.1), (0, 0, 1.45), 'paint_white')
    m.box((1.4, 0.04, 0.9), (0, 0.01, 0.99), 'glass')
    # sill
    m.box((1.6, 0.16, 0.05), (0, -0.0, 0.46), 'concrete_light')
    # flower box
    m.box((1.3, 0.14, 0.26), (0, -0.0, 0.31), 'wood' if not broken else 'wood_dark', bevel=0.01)
    if not broken:
        m.box((0.06, 0.12, 0.9), (0, -0.015, 0.99), 'paint_white')
        m.box((1.2, 0.12, 0.06), (0, -0.015, 1.0), 'paint_white')
        for i in range(9):
            x = -0.55 + i * 0.138
            m.blob((0.22, 0.12, 0.2), (x, -0.01, 0.47), 'leaf_green', rough=0.2, colors=['leaf_green', 'leaf_dark'])
        for i, c in enumerate(['red_bright', 'hazard_yellow', 'fabric_white', 'orange', 'red_bright', 'hazard_yellow']):
            m.box((0.07, 0.07, 0.07), (-0.5 + i * 0.2, -0.03, 0.6 + (i % 2) * 0.03), c)
        m.box((1.4, 0.015, 0.9), (0, -0.04, 0.99), 'glass') if False else None
        # glass highlights (lighter stripe)
        m.box((0.18, 0.012, 0.8), (-0.35, -0.012, 1.0), 'glass_dirty')
        m.box((0.1, 0.012, 0.8), (-0.1, -0.012, 1.0), 'glass_dirty') if False else None
    else:
        m.box((0.06, 0.12, 0.9), (0, -0.015, 0.99), 'paint_white')
        # jagged glass shards remaining at the corners
        m.poly([(-0.7, 0.55), (-0.2, 0.55), (-0.5, 0.95), (-0.7, 0.9)], 0.02, (0, 0.04, 0), 'glass_dirty')
        m.poly([(0.7, 1.4), (0.25, 1.4), (0.45, 1.05), (0.7, 1.0)], 0.02, (0, 0.04, 0), 'glass_dirty')
        # boards nailed across
        m.box((1.55, 0.04, 0.2), (0.0, -0.055, 0.85), 'wood_grey', rot=(0, 8, 0))
        m.box((1.5, 0.04, 0.2), (0.0, -0.055, 1.2), 'wood_dark', rot=(0, -6, 0))
        m.box((1.2, 0.05, 0.18), (0.0, -0.075, 1.0), 'wood_grey', rot=(0, 25, 0)) if False else None
        for x, z in ((-0.62, 0.9), (0.62, 0.8), (-0.6, 1.2), (0.6, 1.25)):
            m.cyl(0.025, 0.01, (x, -0.07, z), 'steel_dark', rot=(90, 0, 0), seg=6)
        m.box((0.4, 0.1, 0.2), (-0.35, -0.0, 0.5 + 0.12), 'wood_dark', rot=(0, 20, 0)) if False else None
    finish(m, bounds={'x': (-0.8, 0.8), 'y': (-0.08, 0.08), 'z': (0, 1.5)})
build('house_window', False)
