from ss_art import Model, finish
m = Model('piece_generator', seed=11)
# Skid base
m.box((1.2, 1.2, 0.08), (0, 0, 0.14), 'steel_dark', bevel=0.01)
for x in (-0.45, 0.45):
    m.box((0.12, 1.2, 0.1), (x, 0, 0.05), 'rust_dark')
# Body
m.box((1.0, 0.9, 0.62), (0, 0, 0.49), 'mustard', bevel=0.025)
m.box((1.02, 0.92, 0.05), (0, 0, 0.3), 'mustard_dark')
# Fuel tank on top
m.cyl(0.2, 0.62, (-0.18, 0.12, 0.92), 'army_green', rot=(0, 90, 0), seg=12)
m.cyl(0.05, 0.05, (-0.25, 0.12, 1.13), 'gunmetal', seg=8)
# Vent panel on the front (-Y)
m.box((0.5, 0.02, 0.34), (-0.12, -0.46, 0.48), 'gunmetal')
for i in range(5):
    m.box((0.44, 0.03, 0.03), (-0.12, -0.47, 0.36 + i * 0.06), 'steel_dark')
# Control box with hazard stripe + lamp
m.box((0.26, 0.04, 0.3), (0.32, -0.47, 0.5), 'steel')
m.box((0.26, 0.05, 0.05), (0.32, -0.47, 0.62), 'hazard_yellow')
m.cyl(0.035, 0.02, (0.32, -0.5, 0.52), 'warning_red', rot=(90, 0, 0), seg=8)
m.cyl(0.03, 0.02, (0.32, -0.5, 0.42), 'black', rot=(90, 0, 0), seg=8)
# Exhaust stack with cap
m.tube([(0.32, -0.15, 0.8), (0.32, -0.15, 1.1)], 0.055, 'gunmetal')
m.cyl(0.075, 0.08, (0.32, -0.15, 1.16), 'rust', seg=10)
# Rust patches and side cables
m.box((0.3, 0.012, 0.16), (-0.25, 0.455, 0.42), 'rust')
m.tube([(0.5, 0.2, 0.6), (0.56, 0.3, 0.45), (0.56, 0.45, 0.25)], 0.015, 'black', seg=6)
finish(m, bounds={'x': (-0.6, 0.6), 'y': (-0.6, 0.6), 'z': (0, 1.2)})
