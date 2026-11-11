from ss_art import Model, finish_icon
m = Model('kit_workbench', seed=718)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
m.box((.24,.12,.025),(0,0,z+.1),'wood_light')
for sx in (-.1,.1):
    for sy in (-.045,.045):
        m.box((.02,.02,.1),(sx,sy,z+.05),'wood_dark')
m.box((.22,.01,.06),(0,.055,z+.07),'wood')
m.box((.04,.05,.04),(.08,0,z+.135),'steel_dark')
m.box((.01,.05,.04),(.1,0,z+.17),'steel')
m.cyl(.006,.06,(.1,.0,z+.14),'chrome',rot=(90,0,0),seg=6)
m.box((.07,.03,.015),(-.06,0,z+.12),'wood_dark')
m.cyl(.008,.07,(-.07,-.02,z+.13),'steel',rot=(0,90,60),seg=6)
finish_icon(m)
