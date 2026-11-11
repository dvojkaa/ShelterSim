from ss_art import Model, finish_icon
m = Model('metal_plate', seed=13)
m.box((.3,.22,.015),(0,0,.0075),'steel_dark',rot=(0,0,10))
m.box((.3,.22,.015),(.01,.01,.025),'steel',rot=(0,0,-5))
for x,y in [(-.12,-.08),(.12,-.08),(-.12,.08),(.12,.08)]:
    m.cyl(.015,.01,(x,y,.036),'rust_dark',seg=6)
finish_icon(m)
