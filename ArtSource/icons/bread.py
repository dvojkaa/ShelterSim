from ss_art import Model, finish_icon
m = Model('bread', seed=608)
m.blob((.3,.16,.12),(0,0,.06),'wood_light',rough=.03,flat_bottom=0.0,colors=['wood_light','wood','wood_light'],subdiv=2)
for i in (-.08,0,.08):
    m.box((.012,.07,.01),(i,0,.115),'wood_dark',rot=(0,0,35))
finish_icon(m)
