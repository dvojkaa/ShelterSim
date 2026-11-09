from pathlib import Path
import bpy
from ss_art import Model, finish

m = Model('piece_bicycle', seed=2511)
# Two compact spoked wheels (front faces toward -Y).
for y in (-.24, .24):
    ring = [(0.45 * __import__('math').sin(i * 6.28318 / 24), y, .51 + 0.45 * __import__('math').cos(i * 6.28318 / 24)) for i in range(25)]
    m.tube(ring, .035, 'rubber', seg=7)
    rim = [(0.40 * __import__('math').sin(i * 6.28318 / 16), y, .51 + 0.40 * __import__('math').cos(i * 6.28318 / 16)) for i in range(17)]
    m.tube(rim, .014, 'steel_dark', seg=5)
    for i in range(8):
        a = i * 3.14159 / 4
        m.tube([(0, y, .51), (.39 * __import__('math').sin(a), y, .51 + .39 * __import__('math').cos(a))], .008, 'chrome', seg=4)
    m.cyl(.055, .11, (0, y, .51), 'rust', seg=8, r2=.055, rot=(90, 0, 0))
# Rusted triangular frame, crank, saddle, handlebars, and a salvage basket.
def tube(a, b, radius, color):
    m.tube([a, b], radius, color, seg=7)

rear=(0,.24,.51); front=(0,-.24,.51); crank=(0,.025,.48); seat=(0,.13,1.0); stem=(0,-.17,.98)
for a,b in ((rear,crank),(crank,front),(front,stem),(stem,seat),(seat,rear),(seat,crank),(front,(0,-.48,1.02))): tube(a,b,.035,'rust')
m.box((.26,.14,.20),(0,-.065,.87),'rust_dark',bevel=.025)
m.box((.3,.14,.055),(0,.12,1.06),'black',bevel=.025)
m.box((.36,.07,.055),(0,-.26,1.06),'steel_dark',bevel=.015)
m.cyl(.055,.21,(0,.025,.48),'steel',seg=8)
finish(m, bounds={'x':(-.9,.9),'y':(-.65,.65),'z':(0,1.3)})
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).resolve().parents[1] / 'blend' / 'piece_bicycle.blend'))
