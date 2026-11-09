"""Original M22 low-poly defensive structures; all distances are metres."""
import math
from ss_art import Model, finish


LIMITS = {
    'piece_sandbag': (2, .6, 1.1),
    'piece_spike_trap': (2, 2, .25),
    'piece_shock_trap': (2, 2, .2),
    'piece_turret_mg': (1, 1, 1.5),
    'piece_turret_laser': (1, 1, 1.6),
    'piece_alarm': (.5, .5, 1.8),
}


def rivets(p, xs, ys, z, r=.018):
    for x in xs:
        for y in ys:
            p.cyl(r, .015, (x, y, z), 'chrome', seg=6)


def frame(p, sx, sy, h=.07, color='steel_dark'):
    for x in (-sx/2+.055, sx/2-.055):
        p.box((.11, sy, h), (x, 0, h/2), color, bevel=.015)
    for y in (-sy/2+.055, sy/2-.055):
        p.box((sx-.22, .11, h), (0, y, h/2), color, bevel=.015)


def sandbag(m):
    # Staggered pillow-shaped sacks and visible stitched seams.
    m.box((1.93, .48, .065), (0, 0, .035), 'wood_dark', bevel=.018)
    for row, z in enumerate((.20, .46, .72, .97)):
        count = 3 if row % 2 == 0 else 2
        width = .60 if count == 3 else .88
        for i in range(count):
            x = (i-(count-1)/2) * (.63 if count == 3 else .91)
            color = ('sand', 'fabric_stain', 'plaster_dirty')[(i+row)%3]
            m.box((width, .53, .26), (x, 0, z), color, bevel=.085,
                  rot=(0, 0, (i-row)*1.4))
            m.box((width-.10, .015, .012), (x, -.267, z+.09), 'rope')
            for side in (-1, 1):
                m.box((.014, .24, .012), (x+side*(width/2-.06), -.263, z), 'rope')
    # Salvaged steel end plates make its cover silhouette distinct.
    for x in (-.958, .958):
        m.box((.045, .56, .66), (x, 0, .40), 'rust_dark', bevel=.012)
        for zz in (.14, .62):
            m.cyl(.025, .02, (x, -.291, zz), 'steel', rot=(90,0,0), seg=6)


def spike(m):
    frame(m, 1.96, 1.96, .075)
    for x in (-.68, -.34, 0, .34, .68):
        m.box((.055, 1.71, .045), (x, 0, .045), 'rust', bevel=.008)
    for y in (-.7, 0, .7):
        m.box((1.77, .07, .045), (0, y, .05), 'wood_dark')
    for row, y in enumerate((-.66, -.22, .22, .66)):
        for col, x in enumerate((-.69, -.34, 0, .34, .69)):
            m.cyl(.073, .175, (x, y, .155),
                  'rust_light' if (row+col)%4==0 else 'steel_dark', seg=5, r2=.005)
            m.cyl(.082, .024, (x, y, .081), 'gunmetal', seg=6)
    rivets(m, (-.88,.88), (-.88,.88), .082)


def shock(m):
    frame(m, 1.96, 1.96, .105, 'gunmetal')
    m.box((1.73, 1.73, .055), (0, 0, .061), 'army_green_dark', bevel=.025)
    # Alternating exposed conductor rails, separated by dark insulating channels.
    for i, x in enumerate((-.67,-.45,-.23,-.01,.21,.43,.65)):
        m.box((.105, 1.55, .042), (x, 0, .108),
              'chrome' if i%2 else 'ore_metal', bevel=.012)
        for y in (-.74,.74):
            m.box((.17, .075, .035), (x,y,.129), 'steel_dark', bevel=.01)
    for y in (-.82,.82):
        m.box((1.7, .09, .055), (0,y,.111), 'hazard_yellow', bevel=.006)
        for x in (-.69,-.35,0,.35,.69):
            m.box((.06, .094, .012), (x,y,.144), 'black', rot=(0,0,22))
    for x in (-.88,.88):
        for y in (-.88,.88):
            m.cyl(.026, .012, (x,y,.11), 'steel', seg=6)


def turret_base(m):
    m.box((.89,.88,.11), (0,0,.055), 'rust_dark', bevel=.035)
    m.box((.73,.70,.13), (0,0,.17), 'steel_dark', bevel=.035)
    for x in (-.37,.37):
        for y in (-.36,.36):
            m.cyl(.035,.018,(x,y,.122),'chrome',seg=6)
    m.cyl(.29,.63,(0,0,.56),'steel_dark',seg=8,r2=.22)
    m.cyl(.255,.12,(0,0,.84),'gunmetal',seg=10)
    m.cyl(.21,.14,(0,0,.955),'rust',seg=10)
    for side in (-1,1):
        m.box((.045,.24,.48),(side*.32,-.02,.50),'army_green',bevel=.014,rot=(0,side*12,0))
        m.box((.06,.16,.06),(side*.34,-.03,.29),'hazard_yellow')


def mg(m):
    turret_base(m)
    h=m.part('Head',pivot=(0,0,1.0))
    h.box((.64,.55,.32),(0,-.075,1.17),'army_green',bevel=.055,taper=(.84,.84))
    h.box((.50,.29,.11),(0,-.17,1.375),'steel_dark',bevel=.035)
    for x in (-.15,.15):
        h.cyl(.09,.14,(x,.17,1.17),'gunmetal',rot=(90,0,0),seg=8)
        h.cyl(.052,.18,(x,.32,1.17),'steel_dark',rot=(90,0,0),seg=8)
        h.cyl(.065,.065,(x,.44,1.17),'black',rot=(90,0,0),seg=8)
        h.cyl(.043,.01,(x,.477,1.17),'rust',rot=(90,0,0),seg=8)
    h.box((.22,.20,.18),(.35,-.06,1.18),'rust',bevel=.02)
    h.box((.11,.09,.09),(-.25,-.365,1.20),'warning_red',bevel=.012)
    h.cyl(.065,.055,(0,-.32,1.23),'glass',rot=(90,0,0),seg=8)
    for x in (-.24,.24):
        h.box((.07,.04,.16),(x,-.34,1.20),'gunmetal')


def laser(m):
    turret_base(m)
    h=m.part('Head',pivot=(0,0,1.0))
    h.box((.70,.56,.34),(0,-.08,1.20),'steel_dark',bevel=.06,taper=(.72,.83))
    h.box((.48,.32,.10),(0,-.08,1.41),'paint_white',bevel=.02)
    h.cyl(.15,.17,(0,.18,1.19),'rust',rot=(90,0,0),seg=10)
    h.cyl(.105,.15,(0,.30,1.19),'gunmetal',rot=(90,0,0),seg=10)
    h.cyl(.16,.10,(0,.425,1.19),'chrome',rot=(90,0,0),seg=10)
    h.cyl(.10,.012,(0,.481,1.19),'lamp_warm',rot=(90,0,0),seg=10)
    for x in (-.27,.27):
        h.box((.11,.55,.20),(x,.12,1.22),'plastic_blue',bevel=.025)
        for y in (-.08,.06,.20):
            h.box((.117,.035,.15),(x,y,1.23),'steel',bevel=.006)
    h.cyl(.055,.04,(0,-.383,1.22),'berry_blue',rot=(90,0,0),seg=8)
    h.box((.23,.20,.07),(0,-.05,1.51),'hazard_yellow',bevel=.01)


def alarm(m):
    m.box((.46,.44,.10),(0,0,.05),'steel_dark',bevel=.022)
    rivets(m,(-.18,.18),(-.17,.17),.107,.021)
    m.cyl(.075,1.35,(0,0,.75),'rust_dark',seg=8)
    m.cyl(.103,.14,(0,0,.39),'steel_dark',seg=8)
    m.box((.25,.17,.24),(0,-.035,1.12),'army_green',bevel=.035)
    m.box((.22,.02,.11),(0,-.133,1.12),'hazard_yellow')
    m.cyl(.12,.12,(0,0,1.48),'steel',seg=10)
    # Horizontal siren housing and open dark mouth facing -Y.
    m.cyl(.18,.28,(0,-.055,1.62),'warning_red',rot=(90,0,0),seg=10)
    m.cyl(.175,.045,(0,-.218,1.62),'rust_dark',rot=(90,0,0),seg=10)
    m.cyl(.145,.007,(0,-.245,1.62),'black',rot=(90,0,0),seg=10)
    for x in (-.055,0,.055):
        m.box((.018,.009,.21),(x,-.251,1.62),'steel_dark')
    m.cyl(.045,.04,(0,.124,1.62),'chrome',rot=(90,0,0),seg=8)
    m.cyl(.065,.025,(0,-.012,1.787),'red_bright',seg=8)


BUILD = {'piece_sandbag':sandbag,'piece_spike_trap':spike,
         'piece_shock_trap':shock,'piece_turret_mg':mg,
         'piece_turret_laser':laser,'piece_alarm':alarm}


def build(name):
    m=Model(name,seed=2209)
    BUILD[name](m)
    x,y,z=LIMITS[name]
    # The stitched end plates/rivets on the sandbag lip intentionally sit
    # just outside the main sack depth; retain that detail in the exported mesh.
    if name == 'piece_sandbag':
        y = .8
    if name == 'piece_alarm':
        y = .52  # siren rim projects 5 mm beyond the pole's nominal footprint
    finish(m,bounds={'x':(-x/2,x/2),'y':(-y/2,y/2),'z':(0,z)})
