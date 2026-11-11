from ss_art import Model, finish_icon
m = Model('planks', seed=7)
for i,(y,z,c) in enumerate([(-.07,.02,'wood_light'),(.0,.02,'wood'),(.07,.02,'wood_light'),(-.035,.06,'wood'),(.035,.06,'wood_light')]):
    m.box((.4,.065,.035),(0,y,z),c,rot=(0,0,8*(i%2)))
finish_icon(m)
