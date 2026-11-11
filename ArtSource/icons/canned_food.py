from ss_art import Model, finish_icon
m = Model('canned_food', seed=610)
m.cyl(.09,.15,(0,0,.075),'steel',seg=14)
m.cyl(.092,.08,(0,0,.075),'warning_red',seg=14)
m.cyl(.094,.012,(0,0,.14),'chrome',seg=14)
m.cyl(.094,.012,(0,0,.01),'chrome',seg=14)
m.cyl(.03,.012,(0,0,.158),'chrome',seg=10)
finish_icon(m)
