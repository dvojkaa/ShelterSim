from ss_art import Model, finish_icon
m = Model('kit_bed', seed=700)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
m.box((.22,.12,.02),(0,0,z+.04),'fabric_blue')
m.box((.22,.12,.02),(0,0,z+.11),'fabric_grey')
for sx in (-.1,.1):
    for sy in (-.05,.05):
        m.box((.015,.015,.15),(sx,sy,z+.075),'steel_dark')
m.box((.05,.1,.02),(-.08,0,z+.06),'fabric_white')
finish_icon(m)
