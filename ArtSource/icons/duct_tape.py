from ss_art import Model, finish_icon
m = Model('duct_tape', seed=1)
m.cyl(.09,.07,(0,0,.035),'steel',seg=16)
m.cyl(.095,.05,(0,0,.035),'steel_dark',seg=16)
m.cyl(.05,.075,(0,0,.035),'concrete_dark',seg=16)
m.box((.2,.07,.006),(.12,-.04,.003),'steel',rot=(0,0,-20))
finish_icon(m)
