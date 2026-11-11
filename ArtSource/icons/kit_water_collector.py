from ss_art import Model, finish_icon
m = Model('kit_water_collector', seed=717)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
m.cyl(.065,.11,(0,0,z+.055),'plastic_blue',seg=14)
m.cyl(.068,.012,(0,0,z+.04),'steel_dark',seg=14)
m.cyl(.068,.012,(0,0,z+.09),'steel_dark',seg=14)
m.cyl(.07,.07,(0,0,z+.145),'chrome',seg=14,r2=.02) if False else None
m.cyl(.08,.07,(0,0,z+.145),'steel',seg=14,r2=.025)
m.cyl(.015,.02,(.06,0,z+.04),'chrome',rot=(0,90,0),seg=8)
finish_icon(m)
