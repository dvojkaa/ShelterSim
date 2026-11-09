"""Original scavenged first-person firearms, authored in Blender metres.

The grip centre is the origin, muzzle points +Z, top points +Y.
"""
import math
from mathutils import Vector
from ss_art import Model, finish

BOUNDS = {
    'weapon_pistol': ((.10,.15,.12,.42), 'pistol'),
    'weapon_smg': ((.13,.18,.22,.64), 'smg'),
    'weapon_shotgun': ((.12,.15,.32,.90), 'shotgun'),
    'weapon_hunting_rifle': ((.13,.16,.34,1.00), 'hunting'),
    'weapon_assault_rifle': ((.14,.20,.30,.88), 'assault'),
    'weapon_laser_pistol': ((.13,.16,.13,.48), 'laser_pistol'),
    'weapon_laser_rifle': ((.16,.18,.30,.90), 'laser_rifle'),
    'weapon_plasma_rifle': ((.20,.20,.30,.95), 'plasma'),
}

def build(name):
    (wx, wy, back, front), kind = BOUNDS[name]
    m = Model(name, seed=220 + list(BOUNDS).index(name))
    energy = kind.startswith('laser') or kind == 'plasma'
    pistol = kind in ('pistol','laser_pistol')
    long = not pistol and kind != 'smg'
    c = 'paint_green' if energy else ('army_green' if kind in ('assault','smg') else 'wood')
    d = 'plastic_blue' if energy else ('army_green_dark' if kind in ('assault','smg') else 'wood_dark')
    body_end = front - (.10 if long else .075)
    body_start = -.065 if pistol else -.095
    width = .145 if kind == 'plasma' else (.115 if kind in ('laser_rifle','assault') else .09)
    height = .105 if kind == 'plasma' else .086

    def box(s, p, col, bevel=.005, rot=(0,0,0)):
        m.box(s,p,col,bevel=bevel,rot=rot)
    def cyl(r,h,p,col,seg=12,r2=None,rot=(0,0,0)):
        m.cyl(r,h,p,col,seg=seg,r2=r2,rot=rot)
    def line(a,b,r,col,seg=6):
        m.tube([a,b],r,col,seg=seg)
    def axial(z0,z1,r,col,seg=12,r2=None,y=0):
        cyl(r,z1-z0,(0,y,(z0+z1)/2),col,seg,r2)

    # Forged receiver, stepped housing, breech cap and long sight plane.
    body_len = body_end-body_start
    box((width,height,body_len), (0,.025,(body_start+body_end)/2), 'gunmetal', .012)
    box((width*.90,.011,body_len*.78),(0,.025+height/2-.001,(body_start+body_end)/2),'steel_dark',.003)
    box((width*.72,.010,body_len*.51),(0,.025-height/2,(body_start+body_end)/2),d,.002)
    box((width*.85,.07,.035),(0,.025,body_start+.012),'steel_dark',.007)
    for side in (-1,1):
        x = side*(width/2+.002)
        box((.006,.042,body_len*.50),(x,.026,(body_start+body_end)/2),c,.002)
        for zz in (body_start+.05, body_end-.045):
            cyl(.006,.007,(x+side*.003,.016,zz),'rust' if not energy else 'chrome',8,rot=(0,90,0))

    # Grip: angled leather/plastic slab, end cap and five broad ribs.
    grip_y=-.053
    box((.059,.071,.148),(0,grip_y,-.028),'leather_dark' if not energy else 'steel_dark',.008,rot=(-10,0,0))
    box((.063,.069,.023),(0,-.079,-.105),d,.005)
    for i in range(5):
        box((.062,.005,.010),(0,-.089,-.071+i*.023),'leather' if not energy else 'plastic_blue',.002,rot=(-10,0,0))
    # Dark open trigger guard and a visible trigger tongue.
    line((-.034,-.016,.065),(-.034,-.088,.049),.006,'steel_dark')
    line((-.034,-.088,.049),(-.034,-.091,-.010),.006,'steel_dark')
    line((-.034,-.091,-.010),(-.034,-.061,-.014),.006,'steel_dark')
    line((.034,-.016,.065),(.034,-.088,.049),.006,'steel_dark')
    line((.034,-.088,.049),(.034,-.091,-.010),.006,'steel_dark')
    line((.034,-.091,-.010),(.034,-.061,-.014),.006,'steel_dark')
    box((.006,.034,.009),(0,-.052,.028),'chrome',.001,rot=(-18,0,0))

    if kind == 'pistol':
        box((.084,.042,.255),(0,.057,.18),'steel',.009)
        box((.062,.009,.21),(0,.083,.185),'rust_dark',.003)
        for zz in (.105,.13,.155):
            box((.086,.009,.006),(0,.067,zz),'gunmetal',.001)
        axial(.305,.398,.029,'gunmetal')
        axial(.37,.405,.034,'rust_dark')
    elif kind == 'smg':
        axial(.13,.55,.035,'gunmetal')
        for zz in (.31,.365,.42,.475):
            axial(zz,zz+.015,.044,'steel_dark',10)
        box((.072,.065,.14),(0,-.025,.30),'army_green',.006)
        # Bent wire folding stock and rear shoulder frame.
        for sx in (-.048,.048):
            line((sx,.012,-.065),(sx,.012,-.200),.006,'steel')
            line((sx,.012,-.200),(sx,-.056,-.212),.006,'steel')
        line((-.048,-.056,-.212),(.048,-.056,-.212),.007,'steel')
        axial(.54,.617,.032,'gunmetal')
        axial(.602,.626,.045,'steel_dark')
    elif kind == 'shotgun':
        # Two visible parallel tubes and large ribbed pump sleeve.
        for yy,rr in ((.035,.027),(-.028,.020)):
            axial(.16,.855,rr,'gunmetal',12,y=yy)
            axial(.60,.615,rr+.005,'rust_dark',12,y=yy)
        box((.088,.083,.19),(0,.005,.43),'wood',.012)
        for zz in (.366,.395,.424,.453,.482):
            box((.091,.087,.007),(0,.005,zz),'wood_dark',.002)
        axial(.85,.885,.032,'rust_dark',12,y=.035)
        # Wide rough timber stock, joined by narrow neck.
        box((.055,.082,.11),(0,.012,-.125),'wood_dark',.008)
        box((.088,.11,.16),(0,.012,-.238),'wood',.012)
        box((.093,.118,.022),(0,.012,-.308),'rubber',.003)
    elif kind == 'hunting':
        axial(.18,.94,.022,'gunmetal',12)
        axial(.50,.52,.026,'rust_dark',12)
        box((.085,.070,.43),(0,-.014,.405),'wood',.012)
        box((.065,.038,.40),(0,-.045,.395),'wood_dark',.006)
        box((.072,.07,.16),(0,.018,-.16),'wood',.008)
        box((.096,.115,.13),(0,.013,-.266),'wood',.012)
        box((.10,.12,.018),(0,.013,-.329),'leather_dark',.003)
        # Scope in elevated cradles, with blue glass at each end.
        for zz in (.15,.32):
            box((.019,.050,.022),(0,.076,zz),'steel_dark',.003)
        axial(.11,.38,.022,'gunmetal',12,y=.11)
        axial(.11,.13,.03,'steel_dark',12,y=.11)
        axial(.36,.385,.032,'steel_dark',12,y=.11)
        cyl(.019,.002,(0,.11,.386),'berry_blue',12)
        axial(.938,.976,.026,'rust_dark')
    elif kind == 'assault':
        axial(.21,.79,.031,'gunmetal')
        box((.103,.086,.27),(0,.027,.40),'army_green',.008)
        for sx in (-.054,.054):
            for zz in (.32,.37,.42,.47):
                box((.005,.023,.018),(sx,.03,zz),'black',.001)
        # Angular skeleton stock and cheek rest.
        for sx in (-.05,.05):
            line((sx,.022,-.085),(sx,.073,-.25),.009,'steel_dark')
            line((sx,.073,-.25),(sx,-.066,-.25),.009,'steel_dark')
        box((.112,.018,.025),(0,.085,-.22),'army_green',.003)
        box((.12,.15,.02),(0,.004,-.282),'rubber',.003)
        axial(.78,.845,.029,'gunmetal')
        axial(.83,.865,.047,'steel_dark')
        for zz in (.83,.847):
            box((.006,.014,.01),(.046,.0,zz),'rust',.001)
    elif kind == 'laser_pistol':
        box((.092,.072,.26),(0,.044,.198),'plastic_blue',.012)
        for sx in (-.061,.061):
            axial(.145,.37,.014,'berry_blue',8,y=.071)
            box((.020,.033,.17),(sx,.053,.24),'steel_dark',.004)
        for zz in (.15,.22,.29,.35):
            box((.103,.009,.008),(0,.084,zz),'berry_light',.002)
        axial(.36,.446,.028,'gunmetal')
        axial(.423,.458,.041,'chrome')
    elif kind == 'laser_rifle':
        axial(.21,.81,.023,'gunmetal')
        for sx in (-.075,.075):
            for zz in (.28,.40,.52,.64):
                cyl(.026,.014,(sx,.02,zz),'berry_blue',8)
            line((sx,.02,.255),(sx,.02,.685),.012,'plastic_blue')
        for zz in (.25,.68):
            box((.15,.076,.022),(0,.02,zz),'steel_dark',.005)
        box((.075,.07,.17),(0,.008,-.18),'plastic_blue',.007)
        box((.105,.10,.035),(0,.008,-.28),'rubber',.005)
        axial(.80,.873,.038,'gunmetal')
        axial(.85,.892,.052,'chrome')
    elif kind == 'plasma':
        axial(.20,.86,.033,'gunmetal')
        # Three armoured emitter rails form a triangular open cage.
        for xx,yy in ((-.10,.005),(.10,.005),(0,.145)):
            line((xx,yy,.25),(xx,yy,.77),.016,'plastic_blue')
            for zz in (.32,.43,.54,.65):
                cyl(.022,.024,(xx,yy,zz),'berry_light',8)
        for zz in (.29,.52,.74):
            box((.172,.019,.018),(0,.016,zz),'chrome',.003)
        axial(.30,.70,.063,'berry_blue',10,y=.065)
        box((.13,.09,.22),(0,.01,-.18),'steel_dark',.009)
        box((.15,.12,.025),(0,.01,-.286),'rubber',.004)
        axial(.85,.925,.046,'gunmetal')
        axial(.90,.94,.064,'steel_dark')

    # Magazine or cell: silhouette descends beneath the receiver.
    if kind in ('pistol','laser_pistol'):
        pass
    elif kind == 'shotgun':
        for zz in (.10,.16):
            cyl(.026,.038,(0,-.055,zz),'rust',10,rot=(90,0,0))
    elif kind == 'hunting':
        box((.050,.033,.09),(0,-.074,.16),'gunmetal',.004)
    else:
        zmag = .16 if kind == 'smg' else .145
        box((.061,.075,.11),(0,-.055,zmag),d,.006,rot=(-18,0,0))
        box((.066,.015,.105),(0,-.094,zmag), 'steel_dark',.003,rot=(-18,0,0))
        for zz in (zmag-.031,zmag,zmag+.031):
            box((.068,.006,.005),(0,-.101,zz),'rust' if not energy else 'berry_light',.001)

    # A simple rear notch and front bead. Muzzle is a recessed black opening.
    box((.048,.019,.026),(0,.073,body_start+.035),'steel_dark',.003)
    box((.026,.010,.016),(0,.080,body_start+.035),'black',.001)
    box((.035,.022,.012),(0,.064,body_end-.025),'steel_dark',.003)
    muzzle_y=.035 if kind=='shotgun' else 0
    muzzle_tip = {'pistol':.404,'smg':.625,'shotgun':.884,'hunting':.975,
                  'assault':.864,'laser_pistol':.457,'laser_rifle':.891,'plasma':.939}[kind]
    cyl(.017 if kind=='hunting' else (.026 if kind=='plasma' else .021),.003,
        (0,muzzle_y,muzzle_tip),'black',12)
    # Scratched edge plates and repair bands have deliberate asymmetry.
    for sx in (-1,1):
        box((.007,.030,.045),(sx*(width/2+.005),.025,body_start+.095),
            'rust' if not energy else 'chrome',.001,rot=(0,sx*7,0))
    if energy:
        box((.023,.006,.06),(-width/2-.004,.041,body_start+.14),'lamp_warm',.002)
    else:
        box((.022,.006,.047),(-width/2-.004,.042,body_start+.14),'rust_light',.002)

    finish(m, bounds={'x':(-wx,wx),'y':(-wy, .28 if kind=='plasma' else (.25 if kind=='laser_rifle' else (.23 if kind=='assault' else (.22 if kind=='hunting' else (.20 if kind in ('smg','laser_pistol') else .18))))),
                      'z':(-back,front)})
