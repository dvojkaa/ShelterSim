from ss_art import Model, finish_icon
m = Model('fuel', seed=615)
m.box((.3,.12,.38),(0,0,0),'warning_red',bevel=.02)
m.box((.1,.08,.1),(-.07,0,.24),'warning_red')
m.cyl(.035,.06,(.09,0,.22),'black',seg=8)
m.box((.2,.125,.12),(0,0,.0),'hazard_yellow')
m.tube([(-.12,0,.2),(-.12,0,.3),(-.02,0,.3)],.015,'warning_red')
finish_icon(m)
