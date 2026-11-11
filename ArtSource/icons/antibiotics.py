from ss_art import Model, finish_icon
m = Model('antibiotics', seed=613)
m.cyl(.055,.14,(0,0,.07),'mustard',seg=12)
m.cyl(.058,.03,(0,0,.155),'paint_white',seg=12)
m.box((.1,.06,.08),(0,-.03,.07),'paint_white',rot=(0,0,0))
for i,(x,y,c) in enumerate([(.13,.03,'red_bright'),(.17,-.03,'paint_white')]):
    m.cyl(.025,.07,(x,y,.025),c,seg=10,rot=(0,90,i*30))
    m.blob((.05,.05,.05),(x+.035,y,.025),c,rough=.02)
finish_icon(m)
