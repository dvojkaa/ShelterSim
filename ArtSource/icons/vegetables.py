from ss_art import Model, finish_icon
m = Model('vegetables', seed=6)
m.blob((.14,.14,.12),(0,0,.06),'warning_red',rough=.04)
m.cyl(.03,.03,(0,0,.13),'leaf_green',seg=6)
m.cyl(.04,.3,(.14,.06,.07),'orange',rot=(0,80,30),seg=8,r2=.005)
m.blob((.16,.12,.1),(-.1,.14,.05),'leaf_green',rough=.08)
finish_icon(m)
