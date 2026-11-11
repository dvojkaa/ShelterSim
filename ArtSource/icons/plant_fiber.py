from ss_art import Model, finish_icon
m = Model('plant_fiber', seed=606)
for i in range(7):
    m.cyl(.035,.5,(0,(i-3)*.045,.03*(i%3)),'leaf_dry' if i%2 else 'leaf_green',rot=(0,90,(i-3)*4),seg=6)
m.cyl(.09,.07,(0,0,.03),'rope',rot=(0,90,0),seg=10)
m.cyl(.09,.07,(.0,0,.03),'rope',rot=(0,90,0),seg=10)
m.tube([(-.15,-.12,0),(-.05,.12,.06),(.2,.1,.05)],.015,'rope',seg=5)
finish_icon(m)
