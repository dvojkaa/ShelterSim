from ss_art import Model, finish_icon
m = Model('food_berries', seed=601)
for x,y,z in [(-.07,0,.05),(.06,.02,.05),(0,-.07,.05),(.0,.07,.05),(-.03,.01,.12),(.05,-.04,.11)]:
    m.blob((.085,.085,.085),(x,y,z),'berry_blue',rough=.08)
    m.blob((.03,.03,.03),(x+.015,y-.02,z+.04),'berry_light',rough=.05)
m.box((.08,.012,.012),(.03,.03,.17),'leaf_green',rot=(0,0,30))
m.box((.07,.04,.01),(-.03,.05,.15),'leaf_green',rot=(0,0,-20))
finish_icon(m)
