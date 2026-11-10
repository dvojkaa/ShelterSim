import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('furn_shop_counter', seed=321)
# plinth + back body
m.box((2.0, 0.66, 0.08), (0, 0.0, 0.04), 'wood_dark')
m.box((2.0, 0.7, 0.04), (0, 0, 0.92), 'wood', bevel=0.01)          # worn top
m.box((0.5, 0.3, 0.005), (-0.3, 0.0, 0.944), 'wood_dark')           # wear patch
m.box((0.3, 0.1, 0.005), (0.7, -0.2, 0.944), 'wood_grey')
m.box((2.0, 0.04, 0.82), (0, 0.33, 0.49), 'wood_dark')               # back panel
m.box((0.05, 0.66, 0.82), (-0.975, 0, 0.49), 'wood_dark')
m.box((0.05, 0.66, 0.82), (0.975, 0, 0.49), 'wood_dark')
m.box((0.05, 0.66, 0.82), (0.0, 0, 0.49), 'wood_dark')               # divider
m.box((1.0, 0.6, 0.03), (0.5, 0, 0.1), 'wood_dark')                   # right inside floor
# right section: solid front panel w/ drawer opening
m.box((0.95, 0.03, 0.4), (0.5, -0.335, 0.3), 'wood', bevel=0.008)   # lower door panel
m.box((0.4, 0.015, 0.25), (0.3, -0.342, 0.3), 'wood_dark')
m.box((0.4, 0.015, 0.25), (0.72, -0.342, 0.3), 'wood_dark')
m.box((0.05, 0.02, 0.12), (0.5, -0.34, 0.3), 'steel_dark')
m.box((0.95, 0.03, 0.06), (0.5, -0.335, 0.87), 'wood', bevel=0.008)  # top rail
m.box((0.12, 0.03, 0.2), (0.93, -0.335, 0.7), 'wood')               # side stile
m.box((0.12, 0.03, 0.2), (0.07, -0.335, 0.7), 'wood')
m.box((0.9, 0.5, 0.01), (0.5, -0.05, 0.6), 'wood_dark')              # drawer cavity bottom (below drawer)
# left section: glass display
m.box((0.95, 0.5, 0.03), (-0.5, 0.0, 0.14), 'fabric_stain')          # display floor cloth
m.box((0.9, 0.03, 0.03), (-0.5, -0.335, 0.1), 'wood')
m.box((0.9, 0.03, 0.06), (-0.5, -0.335, 0.88), 'wood')
for x in (-0.94, -0.06):
    m.box((0.06, 0.03, 0.74), (x, -0.335, 0.5), 'wood')
m.box((0.8, 0.012, 0.66), (-0.5, -0.34, 0.5), 'glass', bevel=0.004)  # pane
m.box((0.38, 0.012, 0.3), (-0.72, -0.342, 0.62), 'glass_dirty')      # dirty smear
m.box((0.01, 0.014, 0.4), (-0.4, -0.341, 0.55), 'black', rot=(0, 20, 0))   # cracks
m.box((0.01, 0.014, 0.3), (-0.3, -0.341, 0.4), 'black', rot=(0, -35, 0))
m.box((0.01, 0.014, 0.25), (-0.35, -0.341, 0.68), 'black', rot=(0, 60, 0))
m.box((0.01, 0.014, 0.2), (-0.45, -0.341, 0.35), 'black', rot=(0, -10, 0))
# display shelf + wares
m.box((0.85, 0.4, 0.015), (-0.5, 0.0, 0.5), 'glass_dirty')
for i, (x, c, h) in enumerate([(-0.8, 'rust_light', 0.1), (-0.7, 'mustard_dark', 0.12), (-0.6, 'army_green', 0.09), (-0.3, 'rust', 0.11)]):
    m.cyl(0.04, h, (x, 0.05, 0.215 + h / 2 - 0.05 + 0.005), c, seg=7)
m.box((0.2, 0.14, 0.08), (-0.3, 0.1, 0.205), 'bone', bevel=0.01)
m.box((0.14, 0.1, 0.1), (-0.65, 0.05, 0.57), 'cloth_dark')
m.box((0.12, 0.08, 0.14), (-0.3, 0.05, 0.58), 'steel', bevel=0.008)
# cash register
m.box((0.42, 0.34, 0.08), (0.55, 0.05, 0.98), 'steel_dark', bevel=0.012)
m.box((0.38, 0.3, 0.08), (0.55, 0.06, 1.02), 'gunmetal', bevel=0.012, taper=(0.9, 0.9))
m.box((0.36, 0.1, 0.04), (0.55, -0.1, 1.02), 'rust_dark', rot=(0, 0, 0))
for r in range(3):
    for c in range(5):
        m.box((0.05, 0.035, 0.012), (0.43 + c * 0.06, -0.075 - r * 0.0, 1.065), 'bone') if False else None
for c in range(5):
    m.box((0.045, 0.03, 0.012), (0.43 + c * 0.06, -0.09, 1.062), 'bone')
    m.box((0.045, 0.03, 0.012), (0.43 + c * 0.06, -0.125, 1.058), 'paint_white')
m.box((0.3, 0.025, 0.1), (0.55, 0.14, 1.04), 'steel_dark')           # screen housing
m.box((0.26, 0.012, 0.07), (0.55, 0.125, 1.045), 'glass_dirty')      # blank screen
m.box((0.3, 0.2, 0.03), (0.55, 0.15, 0.97), 'steel_dark')
# bell
m.cyl(0.07, 0.015, (-0.25, 0.0, 0.955), 'steel_dark', seg=10)
m.cyl(0.06, 0.04, (-0.25, 0.0, 0.985), 'mustard', seg=10, r2=0.025)
m.cyl(0.012, 0.015, (-0.25, 0.0, 1.017), 'chrome', seg=6)
# paper stack and pen cup
m.box((0.2, 0.28, 0.02), (0.1, -0.05, 0.95), 'paint_white', rot=(0, 0, 14))
m.box((0.18, 0.25, 0.01), (0.1, -0.04, 0.965), 'plaster_dirty', rot=(0, 0, -6))
m.cyl(0.035, 0.07, (-0.65, 0.2, 0.975), 'rust', seg=7)
# damage: gouge, chipped corner, rust stains
m.box((0.2, 0.05, 0.02), (0.88, -0.30, 0.935), 'wood_dark')
m.box((0.5, 0.01, 0.1), (0.2, -0.345, 0.17), 'rust_dark')
# Drawer_1 till
pv = (0.5, -0.335, 0.77)
d = m.part('Drawer_1', pivot=pv)
d.box((0.5, 0.03, 0.12), pv, 'wood', bevel=0.006)
d.box((0.12, 0.02, 0.025), (0.5, -0.34, 0.78), 'steel')
d.box((0.02, 0.01, 0.02), (0.5, -0.343, 0.78), 'rust')
d.box((0.46, 0.4, 0.01), (0.5, -0.15, 0.715), 'steel_dark')
d.box((0.01, 0.4, 0.08), (0.27, -0.15, 0.75), 'steel_dark')
d.box((0.01, 0.4, 0.08), (0.73, -0.15, 0.75), 'steel_dark')
d.box((0.46, 0.01, 0.08), (0.5, 0.05, 0.75), 'steel_dark')
for i in range(4):
    d.box((0.08, 0.2, 0.012), (0.34 + i * 0.1, -0.15, 0.725), 'plaster_dirty' if i % 2 else 'paint_green')
finish(m, bounds={'x': (-1.0, 1.0), 'y': (-0.35, 0.35), 'z': (0, 1.1)})
