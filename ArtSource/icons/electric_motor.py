from ss_art import Model, finish_icon
m = Model('electric_motor', seed=5091)
m.cyl(.13,.3,(0,0,0),'steel_dark',rot=(0,90,0),seg=14)
m.cyl(.135,.05,(-.1,0,0),'rust',rot=(0,90,0),seg=14)
m.cyl(.135,.05,(.1,0,0),'rust',rot=(0,90,0),seg=14)
m.cyl(.03,.18,(.24,0,0),'chrome',rot=(0,90,0))
m.box((.3,.2,.04),(0,0,-.14),'steel')
m.box((.07,.08,.06),(-.03,0,.14),'gunmetal')
finish_icon(m)
