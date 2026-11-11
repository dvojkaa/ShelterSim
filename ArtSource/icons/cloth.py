from ss_art import Model, finish_icon
m = Model('cloth', seed=607)
m.box((.45,.35,.035),(0,0,0),'fabric_grey',rot=(0,0,12))
m.box((.4,.3,.035),(.02,0,.04),'fabric_blue',rot=(0,0,-8))
m.box((.35,.28,.035),(0,0,.08),'fabric_white',rot=(0,0,20))
m.box((.1,.1,.036),(.1,.04,.1),'cloth_red',rot=(0,0,20))
finish_icon(m)
