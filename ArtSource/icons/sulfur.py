from ss_art import Model, finish_icon
m = Model('sulfur', seed=611)
m.blob((.36,.3,.26),(0,0,0),'hazard_yellow',colors=['hazard_yellow','mustard','mustard_dark'],rough=.22,seed=21)
m.blob((.18,.16,.15),(.22,-.1,-.04),'hazard_yellow',colors=['hazard_yellow','mustard'],seed=22)
m.cyl(.03,.1,(-.1,-.1,.15),'hazard_yellow',rot=(20,0,0),seg=5)
finish_icon(m)
