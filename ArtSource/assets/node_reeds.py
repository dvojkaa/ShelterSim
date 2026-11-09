from pathlib import Path
import bpy
from ss_art import Model, finish


def blade(model, x, y, height, yaw, width, color):
    profile = [
        (0.00, 0.00), (-width * .55, height * .35), (-width * .34, height * .72),
        (-width * .08, height), (width * .28, height * .70), (width * .55, height * .27),
    ]
    model.poly(profile, .014, (x, y, 0.0), color, (0.0, 0.0, yaw))


m = Model('node_reeds', seed=2502)
stems = [(-.20,-.10,.78,-18),(-.10,.14,.94,8),(.02,-.16,1.08,-4),(.15,.09,.88,18),(.23,-.03,.72,42),(-.02,.02,.98,0),(.10,.22,.74,12),(-.25,.10,.70,-38)]
for i, (x,y,h,yaw) in enumerate(stems):
    blade(m, x, y, h, yaw, .065 if i % 2 == 0 else .045, 'leaf_green' if i % 3 else 'leaf_dry')
    if i in (1,2,5):
        m.cyl(.025, .14, (x + .015, y, h - .03), 'leaf_brown', seg=5, r2=.012)
finish(m, bounds={'x':(-.42,.42),'y':(-.42,.42),'z':(0,1.2)})
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).resolve().parents[1] / 'blend' / 'node_reeds.blend'))
