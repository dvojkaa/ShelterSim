from ss_art import Model, finish_icon
m = Model('empty_bottle', seed=5)
m.cyl(.065,.15,(0,0,.075),'glass',seg=12)
m.cyl(.065,.05,(0,0,.17),'glass',seg=12,r2=.025)
m.cyl(.025,.06,(0,0,.23),'glass',seg=10)
m.cyl(.03,.02,(0,0,.27),'rust',seg=10)
m.box((.13,.13,.06),(0,0,.07),'paint_white',rot=(0,0,45)) if False else None
finish_icon(m)
