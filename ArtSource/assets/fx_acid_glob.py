import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'lib'))
from ss_art import Model, finish
m = Model('fx_acid_glob', seed=73)
G = ['leaf_green'] * 5 + ['leaf_dry']
# elongated teardrop: round head at +Y, smoothly decreasing blobs to a point at -Y
m.blob((0.38, 0.32, 0.38), (0, 0.11, 0), 'leaf_green', rough=0.04, subdiv=1, colors=G)
m.blob((0.3, 0.28, 0.3), (0, -0.03, 0), 'leaf_green', rough=0.04, subdiv=1, colors=G)
m.blob((0.2, 0.24, 0.2), (0, -0.12, 0), 'leaf_green', rough=0.04, subdiv=1, colors=G)
m.blob((0.11, 0.18, 0.11), (0, -0.18, 0), 'leaf_green', rough=0.03, subdiv=1, colors=G)
m.cyl(0.045, 0.12, (0, -0.21, 0), 'leaf_green', rot=(90, 0, 0), seg=6, r2=0.0)   # pointed tail
for p, s in [((-0.14, -0.1, 0.09), 0.07), ((0.14, -0.15, -0.08), 0.06), ((0.13, 0.02, 0.12), 0.055)]:
    m.blob((s, s * 1.3, s), p, 'leaf_green', rough=0.05, subdiv=1, colors=G)
finish(m, bounds={'x': (-0.2, 0.2), 'y': (-0.275, 0.275), 'z': (-0.2, 0.2)})
