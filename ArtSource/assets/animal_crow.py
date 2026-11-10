"""Glossy black crow, wings folded. Origin at body centre (it flies in game), feet at z=-0.15, front +Y.
Parts: Torso Head Wing_L Wing_R (pivots at shoulders; flap rotates about Y)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _animal_shift import ShModel
_m = Model('animal_crow', seed=33)
m = ShModel(_m, -.0)

t = m.part('Torso', (0, 0, 0))
t.blob((.18, .36, .16), (0, 0, 0), 'black', rough=.06, subdiv=2, colors=['black', 'black', 'gunmetal'])
t.blob((.13, .2, .1), (0, .02, -.06), 'gunmetal', rough=.05)
t.box((.1, .2, .014), (0, -.28, -.01), 'black', rot=(-10, 0, 0), taper=(1.0, .7))   # tail
t.box((.06, .16, .012), (0, -.3, -.02), 'gunmetal', rot=(-14, 0, 0))
for s in (-1, 1):
    t.tube([(s*.035, .03, -.07), (s*.035, .03, -.14)], .008, 'steel_dark', seg=4)
    for k in (-1, 0, 1):
        t.tube([(s*.035, .03, -.15), (s*.035 + k*.018, .075, -.15)], .006, 'steel_dark', seg=3)

h = m.part('Head', (0, .17, .08))
h.blob((.12, .16, .14), (0, .14, .05), 'black', rough=.04)  # neck
h.blob((.14, .17, .14), (0, .2, .09), 'black', rough=.05)
h.cyl(.03, .13, (0, .31, .075), 'gunmetal', rot=(-90, 0, 0), seg=4, r2=.004)   # beak
for s in (-1, 1):
    h.box((.015, .015, .015), (s*.055, .24, .115), 'bone')

for s, n in ((-1, 'L'), (1, 'R')):
    w = m.part('Wing_' + n, (s*.09, .06, .04))
    w.blob((.05, .3, .14), (s*.1, -.03, .0), 'black', rough=.05, rot=(-8, 0, 0))
    w.box((.03, .2, .06), (s*.1, -.17, -.04), 'gunmetal', rot=(-8, 0, 0), taper=(1, .5))

finish(_m, bounds={'x': (-.55, .55), 'y': (-.4, .4), 'z': (-.16, .17)})
