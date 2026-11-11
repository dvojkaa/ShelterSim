from ss_art import Model, finish_icon
m = Model('boiled_water', seed=604)
m.cyl(.1,.02,(0,0,.01),'steel_dark',seg=14)
m.cyl(.1,.14,(0,0,.09),'steel',seg=14)
m.cyl(.085,.01,(0,0,.165),'water',seg=14)
m.box((.1,.025,.025),(.15,0,.13),'black')
for x in (-.03,.03):
    m.blob((.04,.04,.06),(x,0,.24),'paint_white',rough=.2)
finish_icon(m)
