from ss_art import Model, finish_icon
m = Model('gunpowder', seed=18786)
m.cyl(.14,.26,(0,0,0),'rubber',seg=14)
m.cyl(.145,.04,(0,0,.11),'steel_dark',seg=14)
m.cyl(.146,.1,(0,0,-.02),'hazard_yellow',seg=14)
m.blob((.3,.22,.07),(0,-.05,-.13),'gunmetal',rough=.25,flat_bottom=-.04)
m.box((.05,.05,.05),(0,0,.1),'warning_red',rot=(0,0,45))
finish_icon(m)
