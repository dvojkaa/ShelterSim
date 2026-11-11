from ss_art import Model, finish_icon
m = Model('anti_rad', seed=615)
m.cyl(.07,.12,(0,0,.06),'hazard_yellow',seg=12)
m.cyl(.072,.04,(0,0,.06),'black',seg=12)
m.cyl(.03,.03,(0,0,.13),'steel_dark',seg=10)
for a in (0,120,240):
    m.box((.025,.012,.03),(0,0,.065),'hazard_yellow',rot=(0,0,a))
m.cyl(.074,.012,(0,0,.06),'hazard_yellow',seg=3,r2=.074)
finish_icon(m)
