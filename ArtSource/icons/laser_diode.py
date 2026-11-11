from ss_art import Model, finish_icon
m = Model('laser_diode', seed=15196)
m.cyl(.09,.12,(0,0,0),'steel',seg=12)
m.cyl(.1,.02,(0,0,-.07),'chrome',seg=12)
m.cyl(.04,.08,(0,0,.09),'glass',seg=10)
m.cyl(.03,.02,(0,0,.14),'red_bright',seg=10)
for x in (-.04,0,.04): m.cyl(.008,.2,(x,0,-.17),'chrome')
finish_icon(m)
