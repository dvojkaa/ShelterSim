from ss_art import Model, finish_icon
m = Model('herbs', seed=3)
for i,(a,c) in enumerate([(0,'leaf_green'),(50,'leaf_dark'),(-50,'leaf_green'),(100,'leaf_dry'),(-100,'leaf_dark')]):
    m.blob((.16,.05,.02),(0,0,.02+i*.012),c,rot=(0,0,a),rough=.05)
m.cyl(.012,.18,(0,0,.05),'bark_light',rot=(90,0,25),seg=6)
m.cyl(.04,.05,(0,0,.06),'rope',seg=8)
finish_icon(m)
