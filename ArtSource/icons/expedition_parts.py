from ss_art import Model, finish_icon
m = Model('expedition_parts', seed=605)
m.box((.4,.3,.22),(0,0,0),'army_green')
m.box((.42,.32,.03),(0,0,.12),'army_green_dark')
m.box((.12,.03,.05),(0,-.16,.0),'steel')
m.cyl(.05,.04,(.1,0,.15),'hazard_yellow',seg=10)
m.box((.1,.08,.02),(-.1,0,.15),'warning_red')
m.cyl(.03,.45,(0,0,.0),'steel_dark',rot=(0,90,0),seg=6)
finish_icon(m)
