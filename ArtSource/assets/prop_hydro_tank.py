"""Hydroponics nutrient tank with pump, pipes, gauge, tap at -Y. Origin floor centre."""
from ss_art import Model, finish
m = Model('prop_hydro_tank', seed=62)

R, TX, TY = 0.30, -0.14, 0.0      # tank radius / centre
PX = 0.34                          # pump column x
# Skid
m.box((0.96, 0.9, 0.06), (0, 0, 0.03), 'steel_dark', bevel=0.01)
for x in (-0.42, 0.42):
    for y in (-0.37, 0.37):
        m.box((0.1, 0.1, 0.1), (x, y, 0.05), 'gunmetal')
# Tank body, bands, shoulder, neck
m.cyl(R, 1.30, (TX, TY, 0.74), 'plastic_blue', seg=10)
for z in (0.28, 0.74, 1.2):
    m.cyl(R * 1.03, 0.05, (TX, TY, z), 'steel_dark', seg=10)
m.cyl(R, 0.1, (TX, TY, 1.44), 'steel', seg=10, r2=R * 0.45)
m.cyl(0.10, 0.06, (TX, TY, 1.52), 'black', seg=8)
m.cyl(0.12, 0.04, (TX, TY, 1.57), 'chrome', seg=8)
# Sight glass with water level, tick marks (front, -Y)
FY = TY - R - 0.005
m.box((0.08, 0.02, 0.9), (TX, FY, 0.78), 'glass_dirty')
m.box((0.05, 0.015, 0.5), (TX, FY - 0.008, 0.6), 'water')
for z in (0.4, 0.6, 0.8, 1.0, 1.2):
    m.box((0.18, 0.012, 0.014), (TX, FY - 0.014, z), 'black')
# Tap at -Y low, red handle
m.cyl(0.03, 0.14, (TX, TY - R - 0.06, 0.2), 'chrome', rot=(90, 0, 0), seg=6)
m.cyl(0.042, 0.04, (TX, TY - R - 0.13, 0.2), 'steel_dark', rot=(90, 0, 0), seg=6)
m.box((0.14, 0.025, 0.03), (TX, TY - R - 0.12, 0.26), 'warning_red')
m.cyl(0.02, 0.07, (TX, TY - R - 0.14, 0.15), 'chrome', seg=5, r2=0.012)
# Labels / wear on the tank
m.box((0.26, 0.01, 0.2), (TX - 0.1, FY + 0.01, 1.0), 'paint_green', rot=(0, 0, 18))
m.box((0.2, 0.012, 0.05), (TX - 0.1, FY + 0.006, 1.1), 'hazard_yellow', rot=(0, 0, 18))
m.box((0.16, 0.008, 0.1), (TX - 0.2, FY + 0.01, 0.45), 'rust_dark')
# Pump unit on the right: base, motor can, impeller housing
m.box((0.3, 0.34, 0.07), (PX, 0, 0.095), 'steel_dark', bevel=0.008)
m.cyl(0.11, 0.26, (PX + 0.02, 0.0, 0.25), 'mustard', rot=(0, 90, 0), seg=8)
for i in range(4):
    m.cyl(0.118, 0.02, (PX - 0.08 + i * 0.06, 0.0, 0.25), 'mustard_dark', rot=(0, 90, 0), seg=8)
m.cyl(0.12, 0.08, (PX - 0.14, 0.0, 0.25), 'army_green', rot=(0, 90, 0), seg=8)
m.box((0.1, 0.08, 0.07), (PX + 0.06, 0.0, 0.40), 'black', bevel=0.005)
m.tube([(PX + 0.1, 0.0, 0.43), (PX + 0.1, 0.1, 0.55)], 0.008, 'black', seg=4)
# Suction line tank -> pump (rust-dark), delivery riser up to header, return into tank top
m.tube([(TX + R - 0.02, 0.05, 0.12), (PX - 0.14, 0.05, 0.12), (PX - 0.14, 0.0, 0.2)], 0.03, 'rust_dark', seg=6)
m.tube([(PX - 0.14, 0.0, 0.3), (PX - 0.14, 0.12, 0.3), (PX - 0.14, 0.12, 1.3)], 0.028, 'steel', seg=6)
m.tube([(PX - 0.14, 0.12, 1.3), (TX + 0.0, 0.12, 1.3), (TX, 0.0, 1.3), (TX, 0.0, 1.45)], 0.026, 'steel', seg=6)
for p in ((PX - 0.14, 0.12, 0.3), (PX - 0.14, 0.12, 1.3), (TX, 0.12, 1.3)):
    m.blob((0.075, 0.075, 0.075), p, 'steel_dark', rough=0.0, subdiv=1)
# Valve wheel on riser
m.cyl(0.04, 0.06, (PX - 0.14, 0.12, 0.62), 'rust', seg=6)
m.cyl(0.07, 0.015, (PX - 0.14, 0.12, 0.68), 'warning_red', seg=8)
# Pressure gauge, face toward -Y, on a stub off the riser
GX, GY, GZ = PX + 0.02, -0.17, 0.95
m.tube([(PX - 0.14, 0.12, 0.95), (GX - 0.04, 0.0, 0.95), (GX - 0.02, GY + 0.03, GZ)], 0.013, 'chrome', seg=4)
m.cyl(0.075, 0.045, (GX, GY, GZ), 'steel_dark', rot=(90, 0, 0), seg=10)
m.cyl(0.062, 0.01, (GX, GY - 0.026, GZ), 'fabric_white', rot=(90, 0, 0), seg=10)
m.box((0.008, 0.006, 0.05), (GX + 0.012, GY - 0.033, GZ + 0.012), 'warning_red', rot=(0, 40, 0))
m.box((0.035, 0.006, 0.012), (GX - 0.035, GY - 0.033, GZ - 0.04), 'hazard_yellow')
m.box((0.03, 0.006, 0.012), (GX + 0.03, GY - 0.033, GZ - 0.04), 'warning_red')
finish(m, bounds={'x': (-0.5, 0.5), 'y': (-0.5, 0.5), 'z': (0, 1.6)})
