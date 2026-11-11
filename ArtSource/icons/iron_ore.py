from ss_art import Model, finish_icon
m = Model('iron_ore', seed=603)
m.blob((.4,.34,.3),(0,0,0),'ore_dark',colors=['ore_dark','ore_rust','stone_dark'],rough=.2,seed=4)
m.blob((.12,.1,.1),(.08,-.15,.1),'ore_metal',seed=2)
m.blob((.1,.09,.09),(-.1,-.12,-.02),'ore_metal',seed=7)
m.blob((.1,.09,.08),(.14,-.1,-.06),'ore_metal',seed=9)
finish_icon(m)
