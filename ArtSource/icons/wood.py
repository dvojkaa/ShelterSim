from ss_art import Model, finish_icon
m = Model('wood', seed=601)
for i,(y,z) in enumerate([(-.09,.06),(.09,.06),(0,.18)]):
    m.cyl(.07,.5,(0,y,z),'bark',rot=(0,90,0),seg=10)
    m.cyl(.055,.502,(0,y,z),'wood_light',rot=(0,90,0),seg=10)
    m.cyl(.07,.5,(0,y,z),'bark',rot=(0,90,0),seg=10)
m.cyl(.045,.06,(.26,0,.18),'wood_light',rot=(0,90,0),seg=10)
finish_icon(m)
