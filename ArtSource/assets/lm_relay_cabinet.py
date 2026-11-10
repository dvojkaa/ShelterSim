import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('lm_relay_cabinet', seed=301)
# concrete plinth
m.box((1.4, 0.8, 0.12), (0, 0, 0.06), 'concrete', bevel=0.02)
m.box((1.36, 0.76, 0.03), (0, 0, 0.135), 'concrete_dark')
# body
m.box((1.3, 0.66, 1.34), (0, 0.02, 0.84), 'rust_dark', bevel=0.02)
m.box((1.36, 0.72, 0.08), (0, 0.02, 1.54), 'steel_dark', bevel=0.015)   # roof lip
m.box((1.3, 0.04, 1.2), (0, 0.38, 0.84), 'rust')                          # back patch
for x in (-0.6, 0.6):
    m.box((0.05, 0.05, 1.34), (x, -0.31, 0.84), 'steel_dark')
# rust patches and side vents
m.box((0.4, 0.012, 0.3), (0.2, -0.345, 0.3), 'rust') if False else None
for k in range(4):
    m.box((0.012, 0.3, 0.04), (0.655, 0.02, 0.9 + k * 0.07), 'gunmetal')
    m.box((0.012, 0.3, 0.04), (-0.655, 0.02, 0.9 + k * 0.07), 'gunmetal')
# fixed panel side (right): blank warning plate, hazard stripes, gauges
m.box((0.5, 0.02, 0.5), (0.3, -0.345, 0.95), 'steel_dark')
m.box((0.42, 0.015, 0.3), (0.3, -0.36, 1.0), 'paint_white')              # blank plate
for i in range(5):
    m.box((0.07, 0.015, 0.1), (0.12 + i * 0.09, -0.365, 0.74), 'hazard_yellow' if i % 2 == 0 else 'black', rot=(0, 25, 0))
m.cyl(0.05, 0.03, (0.18, -0.355, 1.3), 'black', rot=(90, 0, 0), seg=8)
m.cyl(0.05, 0.03, (0.42, -0.355, 1.3), 'gunmetal', rot=(90, 0, 0), seg=8)
m.cyl(0.035, 0.02, (0.18, -0.372, 1.3), 'glass_dirty', rot=(90, 0, 0), seg=8)
# dead indicator lamp + live one (part Lamp) on the roof
m.cyl(0.06, 0.1, (-0.4, 0.0, 1.6), 'rust_dark', seg=8)
m.cyl(0.07, 0.05, (-0.4, 0.0, 1.5 + 0.03), 'steel_dark', seg=8) if False else None
m.cyl(0.07, 0.1, (-0.4, 0.0, 1.62), 'glass', seg=8)                   # dead lamp
lamp = m.part('Lamp', pivot=(0.4, 0.0, 1.6))
lamp.cyl(0.075, 0.1, (0.4, 0.0, 1.62), 'lamp_warm', seg=8)
m.cyl(0.09, 0.03, (0.4, 0.0, 1.575), 'gunmetal', seg=8)
for a in (-0.1, 0.1):
    m.box((0.012, 0.012, 0.07), (0.4 + a, 0, 1.6), 'gunmetal')
# cable stubs from the left side into the ground and up
m.tube([(-0.65, 0.2, 1.1), (-0.69, 0.22, 0.9), (-0.69, 0.2, 0.3), (-0.75, 0.0, 0.14)], 0.03, 'black', seg=6)
m.tube([(-0.65, -0.1, 1.2), (-0.69, -0.12, 1.0), (-0.69, -0.2, 0.5), (-0.7, -0.35, 0.14)], 0.022, 'rubber', seg=6)
m.tube([(0.65, 0.2, 0.5), (0.69, 0.2, 0.4), (0.69, 0.0, 0.14)], 0.03, 'black', seg=6)
# door: left half, hinged at left edge (x=-0.6) on front face
door = m.part('Door', pivot=(-0.6, -0.36, 0.0))
door.box((0.62, 0.04, 1.22), (-0.28, -0.37, 0.8), 'rust', bevel=0.01)
door.box((0.5, 0.015, 1.08), (-0.28, -0.395, 0.8), 'rust_dark')
door.box((0.1, 0.03, 0.2), (-0.03, -0.4, 0.8), 'chrome')               # handle
door.box((0.04, 0.03, 0.16), (-0.03, -0.42, 0.8), 'black')
door.box((0.3, 0.015, 0.2), (-0.28, -0.405, 1.1), 'paint_white')        # blank plate
for z in (0.3, 0.75, 1.2):
    door.cyl(0.03, 0.06, (-0.58, -0.36, z), 'steel_dark', seg=6)
for i in range(4):
    door.box((0.5, 0.012, 0.05), (-0.28, -0.4, 0.45 + 0.04 * i - 0.1), 'rust_dark') if False else None
door.box((0.62, 0.02, 0.07), (-0.28, -0.398, 0.4), 'hazard_yellow')
door.box((0.62, 0.02, 0.07), (-0.28, -0.398, 0.55), 'hazard_yellow') if False else None
finish(m, bounds={'x': (-0.7, 0.7), 'y': (-0.4, 0.4), 'z': (0, 1.6)})
