"""Original M22 station geometry. Blender metres; front is -Y."""
from ss_art import Model, finish


DESIGN = {
    'piece_armory': (1.8, .9, 1.4), 'piece_console': (.8, .6, 1.4),
    'piece_radio_console': (1.2, .8, 1.4), 'piece_solar': (2, 2, 1.2),
    'piece_battery': (1, .8, 1.2), 'piece_cooler': (1, 1, 1.6),
    'piece_charger': (.8, .6, 1.2), 'piece_pump': (1, 1, 1.4),
    'piece_irrigator': (1.2, 1.2, 1.2), 'piece_fabricator': (1.8, 1.2, 1.8),
    'piece_repair': (1.5, .9, 1.2),
}

# Claude's actual M22 collider envelopes, converted from Unity X/Y/Z to Blender X/Y/Z.
BOUNDS = {
    'piece_armory': (1.6,.8,1.1), 'piece_console': (.8,.4,1.4),
    'piece_radio_console': (1.2,.7,1.4), 'piece_solar': (2,2,1.2),
    'piece_battery': (1,.6,1.3), 'piece_cooler': (1.2,.8,1.6),
    'piece_charger': (.8,.6,1.1), 'piece_pump': (.8,.8,1.2),
    'piece_irrigator': (.6,.6,1.5), 'piece_fabricator': (1.6,1,1.6),
    'piece_repair': (1,1,1.8),
}


def foot(m, x, y):
    m.box((x, y, .075), (0, 0, .075), 'steel_dark', bevel=.012)
    for a in (-x*.42, x*.42):
        for b in (-y*.39, y*.39):
            m.box((.1, .1, .055), (a, b, .03), 'rust_dark')


def leg(m, x, y, top):
    for a in (-x, x):
        for b in (-y, y):
            m.box((.07,.07,top-.04), (a,b,top/2), 'steel_dark', bevel=.009)


def bolts(m, xs, y, z):
    for x in xs:
        m.cyl(.025,.018,(x,y,z),'chrome',rot=(90,0,0),seg=6)


def vent(m, x, y, z, w, h):
    m.box((w,.018,h),(x,y,z),'gunmetal')
    for i in range(4):
        m.box((w-.07,.022,.018),(x,y-.015,z-h*.35+i*h*.23),'steel')


def screen(m,x,y,z,w,h):
    m.box((w+.045,.035,h+.045),(x,y,z),'black',bevel=.008)
    m.box((w,.039,h),(x,y-.014,z),'glass')
    m.box((w*.7,.043,.018),(x,y-.02,z+h*.18),'plastic_blue')
    m.box((w*.35,.043,.014),(x-w*.18,y-.021,z-h*.2),'lamp_warm')


def led(m,x,y,z,c='lamp_warm'):
    m.cyl(.025,.018,(x,y,z),c,rot=(90,0,0),seg=8)


def hose(m,pts,r=.018,c='black'):
    m.tube(pts,r,c,seg=6)


def armory(m):
    foot(m,1.76,.86); leg(m,.79,.32,1.25)
    m.box((1.68,.8,.10),(0,0,.72),'wood_dark',bevel=.015)
    for x in (-.65,.65):
        m.box((.075,.09,.58),(x,.30,1.05),'steel')
    m.box((1.65,.07,.64),(0,.32,1.05),'army_green_dark')
    for x in (-.58,-.27,.04,.35,.64):
        m.box((.18,.035,.46),(x,.265,1.05),'steel_dark',bevel=.015)
        m.box((.06,.06,.18),(x,.20,.99),'wood')
        m.box((.12,.06,.035),(x,.18,1.17),'chrome')
    m.box((1.70,.14,.11),(0,-.32,.68),'rust')
    for x in (-.72,.72):
        m.box((.12,.18,.22),(x,-.31,.78),'gunmetal')
    m.box((.55,.48,.20),(-.44,-.08,.86),'army_green',bevel=.015)
    m.box((.48,.03,.045),(-.44,-.335,.90),'hazard_yellow')
    m.box((.30,.30,.13),(.43,-.05,.82),'steel_dark',bevel=.015)
    for x in (.30,.42,.54):
        m.cyl(.025,.26,(x,-.06,.99),'chrome',seg=6)
    bolts(m,(-.75,.75),-.40,.67)


def console(m):
    foot(m,.76,.56)
    m.box((.70,.50,1.18),(0,.015,.69),'army_green',bevel=.025,taper=(.91,.95))
    m.box((.67,.07,.70),(0,-.252,.97),'steel_dark')
    screen(m,-.09,-.296,1.11,.37,.30)
    for x in (-.20,-.09,.02):
        led(m,x,-.30,.84,'warning_red' if x==.02 else 'lamp_warm')
    m.box((.27,.10,.28),(.19,-.278,.59),'mustard_dark')
    for x in (.11,.20,.28):
        m.cyl(.025,.04,(x,-.34,.64),'black',rot=(90,0,0),seg=8)
    vent(m,0,.27,.41,.50,.22)
    m.box((.64,.04,.05),(0,-.276,.30),'hazard_yellow')
    bolts(m,(-.30,.30),-.29,1.27)


def radio_console(m):
    foot(m,1.16,.76); leg(m,.51,.30,.76)
    m.box((1.10,.70,.085),(0,0,.77),'wood_dark',bevel=.015)
    m.box((1.07,.12,.50),(0,.29,1.10),'army_green_dark',bevel=.015)
    m.box((.68,.27,.30),(-.19,.18,.96),'steel_dark',bevel=.018)
    screen(m,-.23,.025,1.00,.39,.13)
    for x in (-.42,-.25,-.08):
        m.cyl(.025,.025,(x,.021,.87),'chrome',rot=(90,0,0),seg=8)
    for i in range(5):
        m.box((.025,.025,.18),(.22+i*.07,.027,.98),'gunmetal')
    m.box((.27,.29,.055),(.34,-.16,.85),'wood')
    hose(m,[(.48,-.13,.85),(.48,-.30,.72),(.30,-.34,.66)],.015)
    m.cyl(.035,.32,(.45,.29,1.38),'steel',seg=8)
    m.cyl(.055,.035,(.45,.29,1.25),'rust',seg=8)
    bolts(m,(-.5,.5),-.35,.75)


def solar(m):
    foot(m,1.55,1.35)
    m.box((.18,.18,.60),(0,0,.39),'steel_dark')
    m.box((1.55,.11,.09),(0,0,.73),'steel')
    for x in (-.67,.67):
        m.box((.06,1.45,.06),(x,0,.90),'steel_dark',rot=(22,0,0))
    for y in (-.53,.53):
        m.box((1.79,.06,.055),(0,y,1.02),'steel',rot=(22,0,0))
    # Four separated cells on the single inclined frame.
    for x in (-.45,.45):
        for y in (-.46,.46):
            z=1.00+y*.40
            m.box((.82,.80,.035),(x,y,z),'glass',rot=(22,0,0),bevel=.012)
            for dx in (-.18,.18):
                m.box((.014,.76,.042),(x+dx,y,z+.012),'plastic_blue',rot=(22,0,0))
    m.box((.36,.20,.22),(0,-.32,.25),'mustard')
    hose(m,[(0,-.29,.35),(0,-.48,.38),(.12,-.57,.56)],.02)


def battery(m):
    foot(m,.96,.76)
    for x in (-.30,0,.30):
        m.box((.25,.63,.73),(x,0,.56),'army_green_dark',bevel=.025)
        m.box((.23,.62,.10),(x,0,.93),'steel')
        for y in (-.21,.21):
            m.cyl(.037,.07,(x,y,1.02),'chrome',seg=8)
        m.box((.20,.018,.21),(x,-.326,.63),'mustard_dark')
        led(m,x,-.342,.72,'lamp_warm')
    for x in (-.3,0):
        hose(m,[(x,.21,1.04),(x+.10,.28,1.10),(x+.30,.21,1.04)],.023,'warning_red')
    m.box((.89,.055,.11),(0,-.355,.26),'steel_dark')
    for x in (-.40,.40):
        m.box((.06,.68,.09),(x,0,.90),'rust')


def cooler(m):
    foot(m,.96,.96)
    m.box((.78,.78,1.30),(0,.01,.80),'steel_dark',bevel=.02)
    for x in (-.36,.36):
        m.box((.055,.79,1.38),(x,0,.80),'rust')
    for z in (.36,.58,.80,1.02,1.24):
        m.box((.68,.81,.055),(0,0,z),'steel')
    for z in (.53,1.04):
        m.cyl(.24,.07,(0,-.43,z),'gunmetal',rot=(90,0,0),seg=12)
        m.cyl(.18,.075,(0,-.48,z),'steel_dark',rot=(90,0,0),seg=12)
        for a in (-.10,.10):
            m.box((.23,.016,.04),(a,-.53,z),'chrome',rot=(0,35 if a<0 else -35,0))
        m.cyl(.045,.083,(0,-.54,z),'rust',rot=(90,0,0),seg=8)
    hose(m,[(.35,.29,.32),(.43,.29,.32),(.43,.29,1.42),(.30,.29,1.42)],.028,'rust')
    m.box((.64,.04,.045),(0,-.432,1.45),'hazard_yellow')


def charger(m):
    foot(m,.76,.56)
    m.box((.70,.50,.85),(0,0,.58),'mustard_dark',bevel=.024)
    m.box((.61,.045,.57),(0,-.257,.71),'gunmetal')
    for x in (-.20,0,.20):
        m.box((.13,.05,.40),(x,-.29,.70),'black',bevel=.012)
        m.box((.09,.057,.24),(x,-.31,.68),'plastic_blue',bevel=.01)
        led(m,x,-.327,.95,'lamp_warm' if x<.2 else 'warning_red')
    m.box((.68,.52,.075),(0,0,1.05),'steel')
    m.box((.39,.02,.09),(0,-.263,.27),'hazard_yellow')
    hose(m,[(.30,.23,.43),(.36,.25,.35),(.35,.25,.16)],.025)


def pump(m):
    foot(m,.96,.96)
    m.box((.32,.32,.27),(0,.05,.24),'steel_dark',bevel=.012)
    m.cyl(.34,.52,(0,.05,.46),'army_green',seg=12)
    m.cyl(.37,.07,(0,.05,.72),'steel_dark',seg=12)
    m.cyl(.22,.15,(0,.05,.82),'gunmetal',seg=10)
    m.cyl(.12,.10,(0,.05,.94),'rust',seg=10)
    for x in (-.37,.37):
        hose(m,[(x,-.33,.34),(x,-.15,.34),(x,-.15,.72),(x*.55,-.15,.72)],.07,'steel')
        m.cyl(.10,.08,(x,-.37,.34),'rust',rot=(90,0,0),seg=8)
    m.box((.34,.25,.25),(0,.30,.61),'mustard',bevel=.012)
    m.box((.25,.022,.13),(0,.16,.67),'black')
    led(m,0,.14,.67,'warning_red')
    m.cyl(.14,.04,(.35,.14,1.05),'steel',seg=8)
    hose(m,[(.35,.14,.76),(.35,.14,1.08)],.025,'rust')


def irrigator(m):
    foot(m,1.15,1.15)
    m.cyl(.34,.70,(0,0,.48),'army_green',seg=10)
    m.cyl(.37,.07,(0,0,.86),'steel_dark',seg=10)
    m.cyl(.13,.10,(0,0,.94),'rust',seg=8)
    for x in (-.48,.48):
        hose(m,[(0,0,.33),(x,0,.33),(x,0,.64)],.055,'steel')
        m.cyl(.07,.08,(x,0,.67),'chrome',seg=8)
        hose(m,[(x,-.40,.64),(x,.40,.64)],.032,'steel')
        for y in (-.34,0,.34):
            m.cyl(.018,.12,(x,y,.64),'plastic_blue',rot=(90,0,0),seg=6)
    for y in (-.48,.48):
        hose(m,[(0,0,.33),(0,y,.33),(0,y,.58)],.04,'rust')
        m.cyl(.065,.06,(0,y,.61),'chrome',seg=8)
    m.box((.27,.05,.22),(0,-.35,.55),'mustard')
    led(m,0,-.39,.59)


def fabricator(m):
    foot(m,1.75,1.15)
    for x in (-.78,.78):
        for y in (-.49,.49):
            m.box((.09,.09,1.58),(x,y,.85),'steel_dark')
    m.box((1.69,1.06,.12),(0,0,1.61),'mustard_dark',bevel=.012)
    m.box((1.55,.91,.16),(0,0,.53),'steel_dark')
    m.box((1.38,.79,.08),(0,0,.64),'steel')
    for x in (-.61,.61):
        m.box((.065,.96,.86),(x,0,1.10),'rust')
    m.box((.46,.44,.36),(0,.13,1.33),'gunmetal',bevel=.015)
    m.cyl(.13,.48,(0,.13,1.03),'steel',seg=10)
    m.box((.44,.40,.10),(0,.13,.76),'rust')
    m.box((.83,.075,.72),(0,-.52,1.06),'glass_dirty')
    m.box((.27,.11,.43),(.65,-.45,1.11),'army_green')
    screen(m,.65,-.51,1.20,.16,.12)
    for z in (.96,1.05):
        led(m,.65,-.52,z,'warning_red' if z<1 else 'lamp_warm')
    m.box((1.65,.08,.08),(0,-.51,.69),'hazard_yellow')
    hose(m,[(.67,.31,.64),(.73,.36,.84),(.73,.36,1.44)],.023)


def repair(m):
    foot(m,1.45,.86); leg(m,.64,.33,.82)
    m.box((1.39,.79,.11),(0,0,.82),'wood_dark',bevel=.01)
    m.box((1.32,.09,.36),(0,.34,1.06),'army_green_dark')
    for x in (-.60,.60):
        m.box((.07,.08,.42),(x,.34,1.01),'steel_dark')
    m.box((.29,.24,.11),(-.38,-.10,.94),'steel_dark')
    m.box((.07,.24,.20),(-.48,-.10,1.03),'steel')
    m.box((.07,.24,.20),(-.26,-.10,1.03),'steel')
    m.cyl(.075,.10,(-.54,-.10,.96),'rust',rot=(0,90,0),seg=8)
    m.box((.32,.07,.05),(.24,-.14,.92),'chrome',rot=(0,0,25))
    m.box((.12,.06,.05),(.51,-.11,.93),'rust')
    m.box((.30,.20,.14),(.44,.16,.95),'mustard',bevel=.015)
    for x in (-.49,-.27,.05,.34,.56):
        m.box((.035,.05,.18),(x,.29,1.07),'steel')
    m.box((1.35,.04,.055),(0,-.40,.81),'rust')
    bolts(m,(-.61,.61),-.41,.81)


BUILD = {
    'piece_armory': armory, 'piece_console': console,
    'piece_radio_console': radio_console, 'piece_solar': solar,
    'piece_battery': battery, 'piece_cooler': cooler,
    'piece_charger': charger, 'piece_pump': pump,
    'piece_irrigator': irrigator, 'piece_fabricator': fabricator,
    'piece_repair': repair,
}


def build(name):
    m = Model(name, seed=2200+list(BOUNDS).index(name))
    BUILD[name](m)
    # Fit the runtime collider without changing the shared exporter or its palette.
    from mathutils import Vector
    import bmesh
    old = DESIGN[name]
    target = BOUNDS[name]
    scale = Vector((target[0]/old[0], target[1]/old[1], min(1, target[2]/old[2])))
    bmesh.ops.scale(m.default.bm, vec=scale, verts=m.default.bm.verts)
    verts = list(m.default.bm.verts)
    actual = [max(abs(v.co[i]) for v in verts) for i in (0,1)]
    actual.append(max(v.co.z for v in verts))
    fit = Vector((min(1,(target[0]/2-.005)/actual[0]),
                  min(1,(target[1]/2-.005)/actual[1]),
                  min(1,(target[2]-.005)/actual[2])))
    bmesh.ops.scale(m.default.bm, vec=fit, verts=m.default.bm.verts)
    x,y,z = BOUNDS[name]
    finish(m, bounds={'x':(-x/2,x/2),'y':(-y/2,y/2),'z':(0,z)})
