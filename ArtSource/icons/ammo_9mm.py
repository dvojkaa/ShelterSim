from ss_art import Model, finish_icon
m = Model('ammo_9mm', seed=7624)
for i,x in enumerate((-.14,-.07,0,.07,.14)):
    m.cyl(.03,.16,(x,0,0),'mustard',seg=8)
    m.cyl(.03,.07,(x,0,.115),'rust_light',seg=8,r2=.015)
m.box((.34,.1,.06),(0,0,-.1),'steel_dark')
finish_icon(m)
