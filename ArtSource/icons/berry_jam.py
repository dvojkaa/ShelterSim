from ss_art import Model, finish_icon
m = Model('berry_jam', seed=609)
m.cyl(.085,.16,(0,0,.08),'glass',seg=14)
m.cyl(.075,.12,(0,0,.075),'berry_blue',seg=14)
m.cyl(.09,.03,(0,0,.175),'warning_red',seg=14)
m.box((.1,.01,.07),(0,-.087,.08),'paint_white',rot=(0,0,0))
m.blob((.03,.03,.03),(0,-.095,.08),'berry_light',rough=.05)
finish_icon(m)
