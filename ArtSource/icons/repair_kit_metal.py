from ss_art import Model, finish_icon
m = Model('repair_kit_metal', seed=21039)
m.box((.34,.22,.12),(0,0,.06),'steel_dark',bevel=.01)
m.box((.34,.23,.03),(0,0,.12),'steel')
m.box((.1,.06,.05),(0,0,.16),'chrome')
m.box((.03,.24,.13),(0,0,.065),'hazard_yellow')
m.cyl(.025,.3,(.05,-.2,.03),'wood',rot=(0,90,25))
m.box((.12,.06,.06),(.17,-.2,.05),'steel',rot=(0,0,25))
finish_icon(m)
