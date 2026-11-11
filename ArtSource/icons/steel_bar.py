from ss_art import Model, finish_icon
m = Model('steel_bar', seed=12)
for i,(y,z) in enumerate([(-.06,.04),(.06,.04),(0,.12)]):
    m.box((.36,.1,.07),(0,y,z),'chrome' if i==2 else 'steel',rot=(0,0,25))
m.box((.37,.03,.075),(0,0,.12),'steel_dark',rot=(0,0,25)) if False else None
finish_icon(m)
