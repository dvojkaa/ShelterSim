from ss_art import Model, finish_icon
m = Model('seeds', seed=618)
m.box((.3,.04,.4),(0,0,0),'fabric_white',rot=(10,0,0))
m.box((.3,.045,.1),(0,0,.14),'leaf_green',rot=(10,0,0))
m.blob((.12,.06,.12),(0,-.05,0),'leaf_green',seed=3)
for x,y in [(.22,-.12),(.3,-.06),(.26,-.18)]:
    m.blob((.06,.04,.04),(x-.0,y,-.17),'leaf_brown',seed=int(x*100))
finish_icon(m)
