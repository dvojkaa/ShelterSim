from ss_art import Model, finish_icon
m = Model('glass', seed=616)
m.cyl(.15,.3,(0,0,0),'glass_dirty',seg=12,r2=.15)
m.cyl(.1,.08,(0,0,.18),'glass_dirty',seg=12,r2=.07)
m.cyl(.085,.04,(0,0,.24),'steel',seg=12)
m.cyl(.1,.2,(0,0,-.03),'water',seg=12)
m.box((.04,.02,.2),(-.08,-.145,.03),'chrome')
finish_icon(m)
