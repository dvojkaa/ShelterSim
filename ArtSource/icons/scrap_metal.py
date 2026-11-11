from ss_art import Model, finish_icon
m = Model('scrap_metal', seed=604)
m.box((.4,.3,.03),(0,0,0),'steel',rot=(0,0,20))
m.box((.3,.22,.03),(.04,-.02,.05),'rust',rot=(0,0,-15))
m.box((.28,.16,.03),(-.02,.03,.1),'steel_dark',rot=(10,0,40))
m.cyl(.025,.3,(.1,-.1,.15),'rust_dark',rot=(70,0,30),seg=6)
finish_icon(m)
