from ss_art import Model, finish
m = Model('item_scrap_bundle', seed=304)
# stacked, uneven sheets
m.box((.42,.28,.025),(0,0,.0135),'rust_dark',rot=(0,0,-5))
m.box((.40,.27,.02),(.01,.01,.04),'steel_dark',rot=(0,0,7))
m.box((.38,.25,.022),(-.02,-.01,.062),'rust_light',rot=(0,0,-10))
m.box((.40,.24,.018),(.0,.0,.084),'steel',rot=(0,0,5))
# bent sheet: two halves meeting at a ridge
m.box((.2,.24,.016),(-.1,.02,.115),'rust',rot=(0,-16,0))
m.box((.2,.24,.016),(.09,.02,.118),'steel',rot=(0,18,0))
# rusty plate standing at back
m.box((.24,.014,.16),(.05,.12,.16),'rust',rot=(-10,0,6))
# pipe along the top
m.cyl(.03,.46,(0,-.05,.165),'steel_dark',rot=(0,90,0),seg=6)
m.cyl(.036,.03,(-.21,-.05,.165),'rust_dark',rot=(0,90,0),seg=6)
m.cyl(.036,.03,(.2,-.05,.165),'rust_dark',rot=(0,90,0),seg=6)
# gear leaning at the front
m.cyl(.085,.03,(-.13,-.15,.1),'steel',rot=(90,0,0),seg=8)
m.cyl(.03,.045,(-.13,-.15,.1),'gunmetal',rot=(90,0,0),seg=6)
# wire/rope binding rings round the bundle
for x in (-.12,.12):
    m.box((.025,.34,.012),(x,0,.205),'leather')
    m.box((.025,.012,.2),(x,-.17,.105),'leather')
    m.box((.025,.012,.2),(x,.17,.105),'leather')
    m.box((.025,.34,.012),(x,0,.005),'leather')
m.box((.04,.03,.03),(.12,-.17,.205),'leather_dark')
finish(m, bounds={'x':(-.23,.23),'y':(-.185,.185),'z':(0,.3)})
