import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'lib'))
from ss_art import Model, finish
m = Model('fx_chem_flask', seed=72)
m.cyl(0.13, 0.17, (0, 0, -0.14), 'orange', seg=8, r2=0.145)            # wide body of sloshing orange chemical
m.cyl(0.145, 0.06, (0, 0, -0.025), 'orange', seg=8, r2=0.05)           # rounded shoulder into the neck
m.cyl(0.05, 0.08, (0, 0, 0.045), 'glass_dirty', seg=8)                 # narrow neck (1/3 of body width)
m.cyl(0.044, 0.04, (0, 0, 0.105), 'wood_light', seg=8, r2=0.05)        # cork
m.cyl(0.145, 0.055, (0, 0, -0.14), 'fabric_white', seg=8, r2=0.148)    # taped label, no text
m.cyl(0.15, 0.01, (0, 0, -0.105), 'rust_light', seg=8)
m.cyl(0.15, 0.01, (0, 0, -0.175), 'rust_light', seg=8)
m.blob((0.07, 0.05, 0.07), (0.05, 0.126, -0.16), 'lamp_warm', rough=0.05, subdiv=0)  # bright slosh glint
m.tube([(0.0, 0.0, 0.1), (0.012, 0.008, 0.15)], 0.016, 'cloth_red', seg=5)            # rag fuse
m.cyl(0.05, 0.11, (0.012, 0.008, 0.17), 'red_bright', seg=6, r2=0.0)                 # flame
m.cyl(0.032, 0.08, (0.012, 0.008, 0.16), 'orange', seg=6, r2=0.0)
m.cyl(0.018, 0.05, (0.012, 0.008, 0.15), 'lamp_warm', seg=6, r2=0.0)
finish(m, bounds={'x': (-0.15, 0.15), 'y': (-0.15, 0.15), 'z': (-0.225, 0.225)})
