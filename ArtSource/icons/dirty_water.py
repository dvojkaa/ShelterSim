from ss_art import Model, finish_icon
m = Model('dirty_water', seed=2)
m.cyl(.07,.17,(0,0,.085),'glass',seg=12)
m.cyl(.072,.1,(0,0,.06),'dirt',seg=12)
m.cyl(.07,.04,(0,0,.18),'glass',seg=12,r2=.03)
m.cyl(.03,.03,(0,0,.215),'rust',seg=10)
finish_icon(m)
