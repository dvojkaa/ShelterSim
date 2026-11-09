from pathlib import Path
import bpy
from ss_art import Model, finish


def blade(model, x, y, height, yaw, color):
    # Flat, slightly bent, tapered blades overlap at the crown to read as a tuft at game scale.
    profile = [
        (0.00, 0.00), (-0.035, height * 0.34), (-0.028, height * 0.69),
        (-0.012, height), (0.018, height * 0.72), (0.035, height * 0.30),
    ]
    model.poly(profile, 0.018, (x, y, 0.0), color, (0.0, 0.0, yaw))


m = Model('node_tall_grass', seed=2501)
stems = [(-.20,-.12,.48,-28),(-.11,.15,.58,18),(.02,-.18,.64,-8),(.14,.12,.54,34),(.22,-.03,.46,52),(-.03,.04,.52,0),(.08,.25,.42,12),(-.25,.10,.40,-50),(.28,.15,.39,62),(-.15,-.28,.43,-18)]
for i, (x,y,h,yaw) in enumerate(stems):
    blade(m, x, y, h, yaw, 'leaf_dry' if i % 3 else 'leaf_brown')
# A few crisp seed heads distinguish the dried seed stalks without making the patch look like a bush.
for x, y, h in [(-.11,.15,.58),(.02,-.18,.64),(.14,.12,.54)]:
    m.cyl(.026, .08, (x + .02, y, h - .04), 'mustard_dark', seg=5, r2=.008)
finish(m, bounds={'x':(-.42,.42),'y':(-.42,.42),'z':(0,.72)})
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).resolve().parents[1] / 'blend' / 'node_tall_grass.blend'))
