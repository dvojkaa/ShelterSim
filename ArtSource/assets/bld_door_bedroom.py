import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('bld_door_bedroom', seed=304)
C = 'fabric_blue'
m.box((1.8, 0.06, 2.2), (0, 0, 1.1), C)
# subtle panel lines
m.box((1.3, 0.01, 0.03), (0, -0.034, 2.0), 'car_blue')
m.box((1.3, 0.01, 0.03), (0, -0.034, 0.25), 'car_blue')
m.box((0.03, 0.01, 1.75), (-0.65, -0.034, 1.12), 'car_blue')
m.box((0.03, 0.01, 1.75), (0.65, -0.034, 1.12), 'car_blue')
# faded patches and chipped paint
m.box((0.5, 0.008, 0.4), (-0.2, -0.033, 1.5), 'fabric_grey')
m.box((0.2, 0.008, 0.12), (0.45, -0.033, 0.45), 'plaster')
m.box((0.1, 0.008, 0.18), (-0.78, -0.033, 0.7), 'wood_grey')
m.box((1.8, 0.063, 0.07), (0, 0, 0.035), 'plastic_blue')
# height chart: vertical ruler strip with tick marks (no text) on the hinge side
m.box((0.05, 0.01, 1.3), (-0.5, -0.034, 0.85), 'bone')
for i in range(7):
    m.box((0.1 if i % 2 == 0 else 0.07, 0.006, 0.012), (-0.5, -0.04, 0.3 + i * 0.2), 'wood_dark')
# pencil height marks beside it, with scribbled bars
m.box((0.12, 0.006, 0.01), (-0.38, -0.037, 1.05), 'wood_dark')
m.box((0.12, 0.006, 0.01), (-0.62, -0.037, 1.3), 'wood_dark')
# sticker: star-ish diamond on yellow disc and a red rect sticker
m.cyl(0.07, 0.008, (0.0, -0.034, 1.3), 'hazard_yellow', rot=(90, 0, 0), seg=8)
m.box((0.07, 0.01, 0.07), (0.0, -0.039, 1.3), 'orange', rot=(0, 45, 0))
m.box((0.12, 0.008, 0.08), (0.22, -0.034, 0.95), 'warning_red', rot=(0, 10, 0))
# hairline crack
m.box((0.012, 0.01, 0.45), (0.5, -0.034, 1.6), 'gunmetal', rot=(0, 12, 0))
m.box((0.012, 0.01, 0.2), (0.43, -0.034, 1.3), 'gunmetal', rot=(0, -20, 0))
# lever handle
m.box((0.06, 0.02, 0.06), (0.76, -0.04, 1.0), 'steel_dark', bevel=0.005)
m.cyl(0.012, 0.05, (0.76, -0.06, 1.0), 'steel', rot=(90, 0, 0), seg=6)
m.box((0.17, 0.025, 0.03), (0.69, -0.085, 1.0), 'steel', bevel=0.006)
m.cyl(0.012, 0.05, (0.76, 0.06, 1.0), 'steel', rot=(90, 0, 0), seg=6)
m.box((0.17, 0.025, 0.03), (0.69, 0.085, 1.0), 'steel', bevel=0.006)
m.box((0.03, 0.04, 0.16), (0.9, 0, 1.0), 'steel_dark')
for z in (0.3, 1.1, 1.9):
    m.cyl(0.016, 0.13, (-0.905, -0.01, z), 'steel_dark', seg=6)
    m.box((0.07, 0.065, 0.12), (-0.86, 0, z), 'steel')
finish(m, bounds={'x': (-0.95, 0.95), 'y': (-0.12, 0.12), 'z': (0, 2.2)})
