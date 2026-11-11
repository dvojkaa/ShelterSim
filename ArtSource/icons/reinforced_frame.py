from ss_art import Model, finish_icon
m = Model('reinforced_frame', seed=14)
for s in (-1,1):
    m.box((.04,.04,.28),(s*.13,0,.14),'steel_dark')
    m.box((.3,.04,.04),(0,0,.04+(s>0)*.2),'steel_dark')
m.cyl(.012,.34,(0,0,.14),'rust',rot=(0,36,0),seg=6) if False else None
m.box((.34,.02,.02),(0,-.03,.14),'rust',rot=(0,40,0))
m.box((.34,.02,.02),(0,-.03,.14),'rust',rot=(0,-40,0))
finish_icon(m)
