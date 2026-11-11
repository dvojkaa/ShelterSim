from ss_art import Model, finish_icon
m = Model('trail_mix', seed=617)
m.box((.17,.06,.22),(0,0,.11),'sand',bevel=.015,taper=(.85,.9))
m.box((.19,.065,.03),(0,0,.225),'rope')
for x,z,c in [(-.04,.12,'berry_blue'),(.03,.14,'rust_light'),(.0,.07,'leaf_brown'),(.05,.08,'berry_light')]:
    m.blob((.04,.012,.04),(x,-.034,z),c,rough=.1)
for x,y,c in [(.15,.02,'berry_blue'),(.2,-.03,'wood'),(.14,-.06,'leaf_dry'),(.2,.04,'rust_light')]:
    m.blob((.04,.04,.03),(x,y,.015),c,rough=.1)
finish_icon(m)
