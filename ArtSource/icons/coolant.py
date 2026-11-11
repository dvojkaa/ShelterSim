from ss_art import Model, finish_icon
m = Model('coolant', seed=245)
m.cyl(.14,.36,(0,0,0),'plastic_blue',seg=12)
m.cyl(.09,.08,(0,0,.2),'plastic_blue',seg=10,r2=.05)
m.cyl(.055,.05,(0,0,.27),'paint_white',seg=10)
m.cyl(.145,.14,(0,0,-.02),'water',seg=12)
m.box((.14,.15,.08),(0,-.01,-.02),'paint_white')
finish_icon(m)
