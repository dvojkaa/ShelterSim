"""Hanging grow-light bar. Origin = ceiling mount point; hangs down to z=-0.5. x 1.6, y 0.4."""
import math
from ss_art import Model, finish
m = Model('prop_grow_light', seed=61)

ZT, ZB = -0.30, -0.40          # housing top / bottom
# Suspension chains (alternating links) and ceiling plates
for x in (-0.58, 0.58):
    m.box((0.12, 0.08, 0.02), (x, 0, -0.01), 'steel_dark', bevel=0.004)
    for i in range(9):
        z = -0.03 - i * 0.027
        if i % 2:
            m.box((0.012, 0.03, 0.03), (x, 0, z), 'chrome')
        else:
            m.box((0.03, 0.012, 0.03), (x, 0, z), 'steel')
    m.box((0.05, 0.05, 0.03), (x, 0, ZT + 0.015), 'gunmetal')      # eye block
    m.cyl(0.012, 0.03, (x, 0, -0.275), 'rust_dark', seg=5)
# Housing: sloped reflector trough, open to the underside
m.box((1.54, 0.22, 0.03), (0, 0, ZT - 0.01), 'steel_dark', bevel=0.005)          # top spine
m.box((1.54, 0.03, 0.12), (0, -0.115, -0.35), 'steel', rot=(0, 0, 0), taper=(1, 1), bevel=0.004)
m.box((1.54, 0.03, 0.12), (0, 0.115, -0.35), 'steel', bevel=0.004)
m.box((1.54, 0.34, 0.012), (0, 0, -0.405), 'fabric_white')                        # reflector lip plate (underside rim)
# end caps
for x in (-0.80, 0.80):
    m.box((0.05, 0.36, 0.14), (x * 0.96, 0, -0.35), 'gunmetal', bevel=0.006)
    m.box((0.02, 0.2, 0.06), (x * 0.96 + (0.03 if x < 0 else -0.03), 0, -0.35), 'rust_dark')
# Light strip as a separate part `Strip` (pivot at its centre) so the game can give it an emissive material.
strip = m.part('Strip', pivot=(0, 0, -0.45))
# Light strip (its own part, so the game can give it an emissive material): deep glowing tube-bank under the housing, white core, violet flanks and ends
strip.box((1.44, 0.24, 0.07), (0, 0, -0.44), 'lamp_warm', bevel=0.006)
strip.box((1.40, 0.07, 0.02), (0, 0, -0.482), 'fabric_white')
for x in (-0.55, -0.18, 0.18, 0.55):
    strip.box((0.28, 0.25, 0.04), (x, 0, -0.45), 'berry_light')
    strip.box((0.10, 0.26, 0.05), (x, 0, -0.455), 'lamp_warm')
strip.box((1.46, 0.02, 0.06), (0, -0.125, -0.445), 'berry_light')
strip.box((1.46, 0.02, 0.06), (0, 0.125, -0.445), 'berry_light')
# Bottom diffuser edge frame
m.box((1.48, 0.025, 0.04), (0, -0.14, -0.41), 'gunmetal')
m.box((1.48, 0.025, 0.04), (0, 0.14, -0.41), 'gunmetal')
# Power cable and ballast box on top
m.box((0.30, 0.12, 0.07), (0.22, 0, -0.27), 'army_green_dark', bevel=0.006)
m.tube([(0.35, 0, -0.27), (0.5, 0.05, -0.14), (0.62, 0.06, -0.02)], 0.008, 'black', seg=4)
# wear
m.box((0.2, 0.03, 0.012), (-0.35, -0.131, -0.35), 'rust', rot=(0, 0, 0))
m.box((0.12, 0.004, 0.08), (0.45, -0.132, -0.35), 'rust_dark')
for x in (-0.6, -0.2, 0.2, 0.6):
    m.cyl(0.012, 0.01, (x, -0.133, -0.35), 'chrome', rot=(90, 0, 0), seg=5)
finish(m, bounds={'x': (-0.8, 0.8), 'y': (-0.2, 0.2), 'z': (-0.5, 0)})
