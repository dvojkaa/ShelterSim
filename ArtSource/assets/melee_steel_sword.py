"""First-person steel sword. Grip middle at origin, length +Z, edge toward +Y, flats face +-X."""
import math
from ss_art import Model, finish

m = Model('melee_steel_sword', seed=71)
R = (0, 0, 90)  # poly profile (u, z): u -> world Y, thickness -> X

# Blade (thin in X), nick cut into the +Y edge around z 0.5
blade = [(-0.035, 0.10), (0.035, 0.10), (0.035, 0.49), (0.027, 0.505), (0.035, 0.52), (0.035, 0.78), (0.0, 0.92), (-0.035, 0.78)]
m.poly(blade, 0.012, (0, 0, 0), 'steel', rot=R)
# Bright ground edge bands on the +Y side (broken at the nick), slightly proud of the flats
m.poly([(0.022, 0.12), (0.035, 0.12), (0.035, 0.48), (0.026, 0.49)], 0.0135, (0, 0, 0), 'chrome', rot=R)
m.poly([(0.026, 0.535), (0.035, 0.53), (0.035, 0.775), (0.0, 0.915), (0.02, 0.76)], 0.0135, (0, 0, 0), 'chrome', rot=R)
# Spine strip (darker) on the -Y side
m.poly([(-0.035, 0.12), (-0.026, 0.12), (-0.026, 0.775), (-0.035, 0.78)], 0.0135, (0, 0, 0), 'steel', rot=R)
# Fuller grooves on both flats
for sx in (-1, 1):
    m.box((0.003, 0.012, 0.58), (sx * 0.0065, 0.0, 0.43), 'steel_dark')
    m.box((0.003, 0.006, 0.07), (sx * 0.0065, 0.0, 0.76), 'steel_dark', taper=(1, 0.2))
# Ricasso / blade root
m.box((0.016, 0.05, 0.03), (0, 0, 0.105), 'steel_dark')

# Crossguard along Y, upswept quillon tips
m.box((0.032, 0.13, 0.032), (0, 0, 0.075), 'steel', bevel=0.004)
for sy in (-1, 1):
    m.box((0.028, 0.05, 0.03), (0, sy * 0.082, 0.082), 'steel', rot=(sy * 18, 0, 0), bevel=0.003)
    m.cyl(0.017, 0.026, (0, sy * 0.088, 0.07), 'chrome', rot=(90, 0, 0), seg=8)
m.box((0.036, 0.05, 0.016), (0, 0, 0.098), 'chrome', bevel=0.003)

# Leather-wrapped grip, bound with cord turns, rounded oval cross-section
m.cyl(0.022, 0.19, (0, 0, -0.04), 'leather', seg=8)
for i in range(6):
    z = -0.115 + i * 0.03
    m.cyl(0.0245, 0.011, (0, 0, z), 'leather_dark', seg=8)
m.cyl(0.026, 0.012, (0, 0, 0.052), 'steel_dark', seg=8)
# Pommel: collar + round ball
m.cyl(0.027, 0.014, (0, 0, -0.136), 'steel_dark', seg=8)
m.blob((0.062, 0.062, 0.05), (0, 0, -0.135), 'steel', rough=0.0, subdiv=1)
m.cyl(0.012, 0.01, (0, 0, -0.155), 'chrome', seg=6, r2=0.006)

finish(m, bounds={'x': (-0.04, 0.04), 'y': (-0.11, 0.11), 'z': (-0.16, 0.92)})
