from ss_art import Model, finish_icon
m = Model('bottled_water', seed=603)
m.cyl(.055,.2,(0,0,.1),'plastic_blue',seg=12)
m.cyl(.055,.2,(0,0,.1),'water',seg=12,r2=.055)
m.cyl(.057,.06,(0,0,.1),'paint_white',seg=12)
m.cyl(.025,.05,(0,0,.225),'plastic_blue',seg=10,r2=.02)
m.cyl(.028,.025,(0,0,.27),'paint_white',seg=10)
finish_icon(m)
