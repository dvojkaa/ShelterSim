from ss_art import Model, finish_icon
m = Model('leather', seed=608)
m.box((.45,.34,.04),(0,0,0),'leather',rot=(0,0,15))
m.box((.4,.3,.04),(0,0,.05),'leather_dark',rot=(0,0,-5))
m.box((.2,.06,.02),(.05,.0,.09),'rope',rot=(0,0,-5))
m.cyl(.025,.02,(.12,0,.1),'chrome',seg=8)
finish_icon(m)
