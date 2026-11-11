from ss_art import Model, finish_icon
m = Model('solar_cell', seed=25812)
m.box((.36,.36,.03),(0,0,0),'steel')
for i in range(3):
    for j in range(3):
        m.box((.1,.1,.02),(-.11+i*.11,-.11+j*.11,.02),'berry_blue')
m.box((.37,.01,.035),(0,0,.015),'chrome')
m.box((.01,.37,.035),(0,0,.015),'chrome')
finish_icon(m)
