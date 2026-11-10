from ss_art import Model, finish
m = Model('item_bottle', seed=302)
# standing bottle
x,y=-.07,.03
m.cyl(.052,.07,(x,y,.035),'water',seg=8)
m.cyl(.052,.07,(x,y,.105),'glass_dirty',seg=8)
m.cyl(.052,.04,(x,y,.16),'glass_dirty',seg=8,r2=.02)  # shoulder
m.cyl(.02,.07,(x,y,.215),'glass_dirty',seg=8)    # neck
m.cyl(.027,.045,(x,y,.275),'wood_light',seg=6,r2=.022)  # cork
m.box((.1,.1,.012),(x,y,.006),'glass',bevel=.003)
# lying small bottle (along Y-ish, slight angle)
r=(0,90,0)
m.cyl(.038,.10,(.07,-.04,.04),'glass_dirty',rot=(90,0,-20),seg=8)
m.cyl(.0385,.06,(.07,-.04,.04),'water',rot=(90,0,-20),seg=8)
m.cyl(.038,.03,(.052,.015,.04),'glass_dirty',rot=(90,0,-20),seg=8,r2=.016)
m.cyl(.015,.04,(.043,.045,.04),'glass_dirty',rot=(90,0,-20),seg=8)
m.cyl(.02,.025,(.036,.07,.04),'wood_light',rot=(90,0,-20),seg=6)
finish(m, bounds={'x':(-.14,.14),'y':(-.1,.1),'z':(0,.3)})
