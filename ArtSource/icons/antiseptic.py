from ss_art import Model, finish_icon
m = Model('antiseptic', seed=611)
m.box((.11,.07,.17),(0,0,.085),'plastic_blue',bevel=.01)
m.box((.112,.072,.06),(0,0,.09),'paint_white')
m.box((.04,.015,.04),(0,-.037,.09),'warning_red')
m.box((.015,.04,.04),(0,-.037,.09),'warning_red')
m.cyl(.025,.05,(0,0,.2),'paint_white',seg=10)
m.cyl(.015,.03,(0,.0,.24),'warning_red',seg=8,r2=.008)
finish_icon(m)
