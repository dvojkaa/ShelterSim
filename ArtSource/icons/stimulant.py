from ss_art import Model, finish_icon
m = Model('stimulant', seed=614)
m.cyl(.035,.17,(-.02,0,.0),'plastic_blue',rot=(0,70,0),seg=10)
m.cyl(.037,.08,(-.0,0,.0),'hazard_yellow',rot=(0,70,0),seg=10)
m.cyl(.015,.07,(.095,0,.0),'chrome',rot=(0,70,0),seg=8,r2=.004)
m.cyl(.045,.025,(-.11,0,.0),'red_bright',rot=(0,70,0),seg=10)
m.box((.05,.1,.015),(-.115,0,.0),'red_bright',rot=(0,70,0))
finish_icon(m)
