import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('dirt_pile', seed=74)
C = ['dirt', 'dirt', 'sand', 'wood_grey', 'dirt', 'concrete_dark']
m.blob((4.6, 4.4, 4.1), (0, 0, 0.0), 'dirt', rough=0.14, subdiv=2, colors=['dirt', 'dirt', 'dirt', 'sand', 'wood_grey'], flat_bottom=0.0)
m.blob((2.6, 2.4, 2.6), (-0.3, 0.2, 0.0), 'dirt', rough=0.16, subdiv=1, colors=['dirt', 'sand', 'dirt', 'wood_dark'], flat_bottom=0.0)
m.blob((2.0, 1.8, 1.0), (1.4, -1.0, 0.0), 'dirt', rough=0.2, subdiv=1, colors=['dirt', 'sand', 'dirt'], flat_bottom=0.0)
m.blob((1.8, 1.8, 0.9), (-1.6, -1.2, 0.0), 'dirt', rough=0.2, subdiv=1, colors=['dirt', 'dirt', 'wood_grey'], flat_bottom=0.0)
for p in ((1.0, 1.0, 0.8), (-1.5, 0.6, 0.5), (0.4, -1.6, 0.3), (-0.6, 1.6, 0.3)):
    m.blob((0.5, 0.45, 0.35), p, 'stone', rough=0.25, subdiv=1, colors=['stone', 'stone_dark', 'stone_light'], flat_bottom=0.0 if False else None)
finish(m, bounds={'x': (-2.5, 2.5), 'y': (-2.5, 2.5), 'z': (0, 2.0)})
