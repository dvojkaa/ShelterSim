import math
def sign_base(m, icon_bg):
    for sx in (-1, 1):
        m.box((0.16, 0.16, 3.6), (sx * 1.6, 0.1, 1.8), 'steel_dark', bevel=0.01)
        m.box((0.4, 0.4, 0.08), (sx * 1.6, 0.1, 0.04), 'concrete_dark')
    m.box((4.0, 0.12, 2.0), (0, 0, 2.5), 'rust_dark', bevel=0.02)       # frame
    m.box((3.8, 0.06, 1.8), (0, -0.07, 2.5), icon_bg, bevel=0.01)       # face
    for z in (1.7, 3.3):
        m.box((3.4, 0.06, 0.06), (0, 0.1, z - 0.0), 'steel_dark')
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.cyl(0.04, 0.04, (sx * 1.8, -0.11, 2.5 + sz * 0.85), 'steel', rot=(90, 0, 0), seg=6)
    m.box((1.2, 0.07, 0.4), (-1.2, -0.085, 1.75), 'rust', rot=(0, 0, 0))   # rust runs
    m.box((0.3, 0.07, 0.7), (1.3, -0.085, 2.0), 'rust_dark')
