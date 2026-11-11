from ss_art import Model, finish_icon
m = Model('ammo_rifle', seed=8962)
for x in (-.1,0,.1):
    m.cyl(.035,.28,(x,0,-.04),'mustard',seg=8)
    m.cyl(.035,.12,(x,0,.16),'rust',seg=8,r2=.012)
    m.cyl(.034,.03,(x,0,.01),'mustard_dark',seg=8)
finish_icon(m)
