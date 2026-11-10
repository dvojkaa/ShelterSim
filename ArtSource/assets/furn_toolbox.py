import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('furn_toolbox', seed=312)
# body
m.box((0.96, 0.42, 0.7), (0, 0.01, 0.5), 'warning_red', bevel=0.015)
m.box((1.0, 0.5, 0.05), (0, 0, 0.875), 'steel_dark', bevel=0.01)   # top plate
m.box((0.96, 0.02, 0.1), (0, 0.22, 0.5), 'rust_dark')
m.box((0.5, 0.01, 0.2), (0.2, 0.225, 0.5), 'rust', rot=(0, 0, 0))
# base frame and casters
m.box((1.0, 0.5, 0.06), (0, 0, 0.17), 'gunmetal')
for x in (-0.42, 0.42):
    for y in (-0.17, 0.17):
        m.cyl(0.035, 0.05, (x, y, 0.13), 'steel_dark', seg=6)
        m.cyl(0.07, 0.05, (x, y, 0.07), 'rubber', rot=(0, 90, 0), seg=8)
# side handles
for s in (-1, 1):
    m.box((0.03, 0.4, 0.03), (s * 0.47, 0, 0.75), 'steel', bevel=0.005)
    m.box((0.05, 0.03, 0.03), (s * 0.47, -0.2, 0.75), 'steel')
    m.box((0.05, 0.03, 0.03), (s * 0.47, 0.2, 0.75), 'steel')
# drawers
pivs = [(0, -0.215, 0.74), (0, -0.215, 0.54), (0, -0.215, 0.34)]
for i, pv in enumerate(pivs):
    d = m.part(f'Drawer_{i+1}', pivot=pv)
    z = pv[2]
    d.box((0.88, 0.03, 0.17), pv, 'warning_red' if i != 1 else 'rust', bevel=0.008)
    d.box((0.88, 0.02, 0.03), (0, -0.235, z + 0.065), 'steel_dark')
    d.box((0.5, 0.035, 0.03), (0, -0.23, z - 0.01), 'chrome', bevel=0.005)
    d.box((0.84, 0.4, 0.01), (0, 0.02, z - 0.075), 'steel_dark')
    d.box((0.01, 0.4, 0.1), (-0.42, 0.0, z - 0.02), 'steel_dark')
    d.box((0.01, 0.4, 0.1), (0.42, 0.0, z - 0.02), 'steel_dark')
    d.box((0.84, 0.01, 0.1), (0, 0.2, z - 0.02), 'steel_dark')
# scuffs and dents
m.box((0.2, 0.01, 0.03), (-0.3, -0.216, 0.64), 'rust_light', rot=(0, 20, 0))
m.box((0.12, 0.01, 0.02), (0.35, -0.216, 0.44), 'steel', rot=(0, -30, 0))
m.box((0.18, 0.01, 0.06), (0.4, -0.216, 0.25), 'rust_dark')
# vice on top, right side
m.box((0.3, 0.2, 0.06), (0.28, 0.0, 0.93), 'steel_dark', bevel=0.01)       # base
m.box((0.2, 0.16, 0.08), (0.28, 0.02, 0.99) , 'gunmetal', bevel=0.01) if False else None
m.box((0.12, 0.14, 0.09), (0.28, 0.04, 0.95), 'gunmetal', bevel=0.01)       # fixed jaw body
m.box((0.14, 0.03, 0.07), (0.28, -0.055, 0.95), 'steel', bevel=0.005)    # fixed jaw plate
m.box((0.14, 0.04, 0.09), (0.28, -0.12, 0.95), 'gunmetal', bevel=0.005)   # movable jaw
m.cyl(0.012, 0.16, (0.28, -0.14, 0.945), 'steel', rot=(90, 0, 0), seg=6)   # screw
m.cyl(0.012, 0.14, (0.28, -0.26, 0.975) if False else (0.28, -0.22, 0.945), 'steel', rot=(0, 90, 0), seg=6)  # tommy bar
m.box((0.04, 0.02, 0.02), (0.28, -0.07, 0.935), 'gunmetal')
# clamp screws
m.cyl(0.015, 0.04, (0.16, 0.0, 0.95), 'rust_light', seg=6)
# items on top: wrench, rag, tin
m.box((0.3, 0.04, 0.012), (-0.25, -0.05, 0.906), 'steel', rot=(0, 0, 25))
m.box((0.06, 0.06, 0.012), (-0.37, -0.13, 0.906), 'steel', rot=(0, 0, 25))
m.box((0.18, 0.14, 0.02), (-0.15, 0.08, 0.91), 'cloth_red', rot=(0, 0, -15))
m.cyl(0.05, 0.09, (-0.4, 0.1, 0.945), 'orange', seg=8)
finish(m, bounds={'x': (-0.5, 0.5), 'y': (-0.25, 0.25), 'z': (0, 1.0)})
