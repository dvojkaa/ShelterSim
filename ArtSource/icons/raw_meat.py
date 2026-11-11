from ss_art import Model, finish_icon
m = Model('raw_meat', seed=609)
m.blob((.4,.28,.22),(0,0,0),'red_bright',colors=['warning_red','red_bright','cloth_red'],rough=.12,seed=11)
m.cyl(.04,.3,(.25,.0,0),'bone',rot=(0,90,0),seg=8)
m.cyl(.06,.06,(.4,.04,0),'bone',rot=(0,90,0),seg=8)
m.box((.3,.04,.04),(0,-.1,.12),'bone')
finish_icon(m)
