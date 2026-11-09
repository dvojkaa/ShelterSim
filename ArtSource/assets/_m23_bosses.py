"""Original M23 boss silhouettes. World-space geometry, +Y face, six rigid animation pivots."""
import math
from pathlib import Path
import bpy
from mathutils import Vector
from ss_art import Model, finish


def limb(p, a, b, ra, rb, color, sides=7):
    a,b=Vector(a),Vector(b)
    d=b-a
    p.cyl(ra,d.length,(a+b)/2,color,rot=tuple(math.degrees(v) for v in d.to_track_quat('Z','Y').to_euler()),seg=sides,r2=rb)


def deliver(m,bounds):
    finish(m,bounds=bounds)
    # Keep the native source scene clean: preview stage and cameras are expendable.
    for obj in list(bpy.data.objects):
        if obj.name not in m.parts:
            bpy.data.objects.remove(obj,do_unlink=True)
    bpy.context.scene.camera=None
    path=Path(__file__).resolve().parents[1]/'blend'/(m.name+'.blend')
    path.parent.mkdir(exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.context.preferences.filepaths.file_preview_type='NONE'
    bpy.ops.wm.save_as_mainfile(filepath=str(path))


def humanoid(name, heavy=False):
    m=Model(name,seed=310 if heavy else 312)
    # Heavy work mutant uses a wider, higher skeleton; commander has a human stance.
    k=1.15 if heavy else 1.0
    pivots={'Torso':(0,0,1.0*k),'Head':(0,0,1.55*k),'Arm_L':(-.31*k,0,1.45*k),'Arm_R':(.31*k,0,1.45*k),'Leg_L':(-.16*k,0,.95*k),'Leg_R':(.16*k,0,.95*k)}
    parts={n:m.part(n,pivot=p) for n,p in pivots.items()}
    def box(p,size,loc,c,**kw):
        p.box(tuple(v*k for v in size),tuple(v*k for v in loc),c,**kw)
    t=parts['Torso']; h=parts['Head']
    box(t,(.48,.32,.24),(0,0,1.0),'cloth_dark',bevel=.025)
    box(t,(.43,.32,.49),(0,0,1.32),'army_green_dark' if not heavy else 'leather_dark',taper=(1.25,1.05),bevel=.03)
    box(t,(.50,.11,.39),(0,.18,1.36),'rust_dark' if heavy else 'gunmetal',bevel=.035)
    box(t,(.34,.035,.26),(0,.25,1.40),'mustard_dark' if heavy else 'army_green',bevel=.02)
    box(t,(.49,.07,.12),(0,.18,1.16),'steel_dark',taper=(1.05,1))
    box(t,(.49,.35,.06),(0,0,1.06),'black')
    box(t,(.10,.025,.07),(0,.19,1.06),'steel')
    # Back-mounted scavenged battery/filter pack, strapped rather than floating.
    box(t,(.32,.19,.34),(0,-.23,1.39),'rust_dark' if heavy else 'army_green_dark',bevel=.025)
    for x in (-.15,.15):
        box(t,(.045,.42,.05),(x,-.03,1.45),'black')
        box(t,(.09,.09,.13),(x,.20,1.12),'leather')
    for z in (1.31,1.40,1.49):
        box(t,(.19,.025,.022),(0,-.335,z),'steel_dark')
    # Both helmet and neck are in the Head pivot, with generous collar overlap.
    limb(h,(0,0,1.46*k),(0,0,1.70*k),.10*k,.09*k,'cloth_dark')
    h.blob((.32*k,.32*k,.36*k),(0,-.015*k,1.77*k),'steel_dark',rough=.02,subdiv=1)
    box(h,(.36,.30,.10),(0,.015,1.89),'mustard_dark' if heavy else 'army_green',bevel=.02)
    box(h,(.29,.055,.12),(0,.175,1.76),'black')
    box(h,(.25,.025,.025),(0,.208,1.79),'orange' if heavy else 'lamp_warm')
    box(h,(.23,.085,.14),(0,.16,1.64),'steel_dark',bevel=.018)
    for x in (-.065,0,.065):
        box(h,(.025,.018,.075),(x,.211,1.64),'gunmetal')
    if heavy:
        # Protective jaw cage / industrial welding hood, no recognisable franchise motifs.
        for x in (-.18,.18):
            box(h,(.075,.22,.28),(x,.07,1.73),'rust',bevel=.02)
        box(h,(.12,.15,.08),(0,-.09,1.98),'rust_dark')
    else:
        # Commander's signal unit and asymmetric aerial distinguish a tactical leader.
        box(h,(.08,.10,.16),(-.19,-.02,1.78),'gunmetal')
        limb(h,(-.19,-.02,1.83),(-.21,-.04,2.13),.012,.006,'steel',5)
        t.poly([(-.18,0),(-.16,-.29),(-.07,-.24),(0,-.34),(.10,-.26),(.18,0)],.04,(0,-.17,1.07),'cloth_red')
    for s,side in ((-1,'L'),(1,'R')):
        a=parts['Arm_'+side]; l=parts['Leg_'+side]
        huge=heavy and s==-1
        shoulder=s*.34*k
        a.blob((.28*k,.30*k,.30*k),(shoulder,0,1.45*k),'leather_dark',rough=.02)
        limb(a,(shoulder,0,1.46*k),(s*.40*k,0,1.13*k),(.17 if huge else .105)*k,.095*k,'cloth_dark')
        limb(a,(s*.40*k,0,1.17*k),(s*.42*k,.04*k,.82*k),(.15 if huge else .09)*k,.08*k,'leather_dark')
        box(a,(.34 if huge else .24,.35,.18),(s*.36,0,1.49),'rust' if heavy else 'army_green',bevel=.03)
        box(a,(.28 if huge else .15,.22,.30),(s*.43,.035,.99),'steel_dark',bevel=.025)
        box(a,(.21 if huge else .14,.19,.16),(s*.43,.045,.78),'gunmetal',bevel=.02)
        for z in (1.05,1.15):
            box(a,(.29 if huge else .16,.035,.045),(s*.43,.16,z),'mustard_dark' if heavy else 'steel')
        # Legs remain straight in rest pose; overlap at hips and knees supports rigid swinging.
        limb(l,(s*.16*k,0,.99*k),(s*.19*k,0,.56*k),.14*k,.11*k,'army_green_dark' if not heavy else 'leather_dark')
        limb(l,(s*.19*k,0,.60*k),(s*.20*k,0,.17*k),.115*k,.09*k,'cloth_dark')
        box(l,(.21,.12,.23),(s*.19,.10,.55),'rust_dark' if heavy else 'gunmetal',bevel=.018)
        box(l,(.20,.10,.30),(s*.20,.10,.30),'steel_dark',bevel=.02)
        box(l,(.24,.40,.18),(s*.20,.07,.09),'black',bevel=.018)
        box(l,(.23,.16,.10),(s*.20,.18,.14),'steel_dark',bevel=.015)
    if heavy:
        # Single gigantic armored left shoulder: the encounter's visual weak/strong-side cue.
        a=parts['Arm_L']
        box(a,(.43,.43,.20),(-.40,-.015,1.62),'mustard_dark',bevel=.035)
        for i in range(3):
            box(a,(.07,.045,.17),(-.56+i*.12,.22,1.62),'black',rot=(0,-20,0))
        # Rear cooling/exhaust pipes buried deeply into the backpack.
        for x,z in ((-.14,1.94),(.13,1.86)):
            limb(t,(x,-.25,1.45),(x,-.27,z),.048,.048,'rust',7)
            t.cyl(.053,.045,(x,-.27,z),'gunmetal',seg=7)
    else:
        # A slab shield belongs to Arm_L and moves with it, never with the torso.
        a=parts['Arm_L']
        a.poly([(-.27,-.44),(.18,-.44),(.27,-.32),(.27,.38),(.16,.48),(-.22,.48),(-.29,.31)],.09,(-.47,.27,1.06),'gunmetal')
        a.poly([(-.22,-.32),(.14,-.32),(.20,-.23),(.20,.29),(.11,.36),(-.18,.36)],.04,(-.47,.33,1.06),'army_green')
        a.box((.36,.035,.08),(-.47,.36,1.15),'mustard_dark',rot=(0,-20,0))
        a.box((.10,.035,.10),(-.47,.36,1.40),'paint_white')
        # Short command mace, grip embedded in the right glove.
        a=parts['Arm_R']
        limb(a,(.43,.07,.73),(.43,.42,1.31),.027,.027,'wood_dark',6)
        a.box((.19,.18,.24),(.43,.44,1.35),'steel_dark',bevel=.02)
        for x in (.33,.53):
            a.box((.04,.21,.20),(x,.44,1.35),'steel')
    deliver(m,{'x':(-1,1),'y':(-.60,.65),'z':(0,2.45 if heavy else 2.2)})


def colossus():
    m=Model('boss_root_colossus',seed=311)
    t=m.part('Torso',pivot=(0,0,1.8));h=m.part('Head',pivot=(0,.03,3.13))
    # Bent trunk with a deep cleft, stone-grown sternum and a hollow amber heart.
    limb(t,(-.10,-.06,1.35),(0,-.10,2.55),.44,.58,'bark',8)
    t.blob((1.08,.76,.84),(0,-.07,2.66),'bark_light',rough=.09,subdiv=1,colors=['bark','bark_light','wood_dark'])
    for s in (-1,1):
        limb(t,(s*.29,.30,1.67),(s*.32,.32,2.80),.13,.20,'bark_light',6)
        t.blob((.48,.24,.52),(s*.31,.34,2.50),'stone_dark',rough=.12,colors=['stone_dark','stone','leaf_dark'])
        t.tube([(s*.29,.24,1.57),(s*.14,.39,1.91),(s*.24,.40,2.20)],.065,'wood_dark',seg=5)
        t.poly([(-.025,-.25),(.025,-.20),(.018,.23),(-.012,.15)],.018,(s*.23,-.41,2.12),'wood_dark',rot=(0,s*12,0))
    t.blob((.28,.14,.38),(0,.37,2.44),'black',rough=.02)
    t.poly([(-.065,-.14),(.045,-.08),(.08,.13),(-.04,.09)],.035,(0,.45,2.43),'mustard')
    limb(h,(0,-.05,2.94),(.04,0,3.48),.22,.27,'bark',7)
    h.poly([(-.23,-.19),(-.15,-.32),(.15,-.27),(.25,-.07),(.19,.27),(-.19,.23)],.20,(0,.17,3.33),'wood_grey')
    for s in (-1,1):
        h.box((.105,.032,.045),(s*.105,.286,3.38),'black',rot=(0,s*16,0))
        h.box((.055,.025,.022),(s*.105,.308,3.38),'hazard_yellow',rot=(0,s*16,0))
        # Branch antlers lean outward rather than forming a canopy blob.
        limb(h,(s*.14,-.04,3.45),(s*.39,-.10,3.79),.105,.06,'bark_light',6)
        limb(h,(s*.39,-.10,3.79),(s*.63,-.10,3.96),.06,.012,'wood_grey',5)
        limb(h,(s*.35,-.09,3.73),(s*.28,-.12,3.98),.045,.008,'bark',5)
        limb(h,(s*.39,-.10,3.79),(s*.56,.07,3.83),.045,.008,'bark',5)
    for s,side in ((-1,'L'),(1,'R')):
        a=m.part('Arm_'+side,pivot=(s*.61,0,2.75))
        l=m.part('Leg_'+side,pivot=(s*.30,0,1.55))
        a.blob((.52,.55,.56),(s*.62,-.02,2.76),'stone',rough=.10,colors=['stone','stone_dark','leaf_dark'])
        limb(a,(s*.62,0,2.79),(s*.91,.01,2.12),.24,.20,'bark',7)
        limb(a,(s*.91,.01,2.18),(s*1.12,.15,1.36),.22,.29,'bark_light',7)
        a.blob((.59,.51,.54),(s*1.13,.16,1.33),'bark',rough=.06,colors=['bark','bark_light','wood_dark'])
        for i in range(3):
            x=s*(.94+i*.16)
            limb(a,(x,.29,1.35),(x+s*.04,.39,1.00+i*.03),.085,.032,'wood_dark',5)
        limb(a,(s*.78,-.06,2.45),(s*1.06,-.12,2.65),.065,.006,'wood_grey',5)
        limb(l,(s*.30,0,1.61),(s*.39,-.04,.83),.26,.21,'bark',7)
        limb(l,(s*.39,-.04,.91),(s*.43,.08,.23),.24,.25,'bark_light',7)
        l.blob((.39,.30,.50),(s*.40,.20,.66),'stone_dark',rough=.08)
        # Low branching buttress roots plant each foot on the floor, with exact flat soles.
        for offset in (-.16,.16):
            limb(l,(s*.43,.05,.32),(s*.43+offset,.35,.10),.15,.09,'bark',6)
            l.box((.22,.48,.16),(s*.43+offset,.19,.08),'bark',taper=(.65,.65))
        limb(t,(s*.42,-.25,2.70),(s*.67,-.32,3.21),.095,.008,'bark_dead',6)
    # Creeping moss is sparse and concentrated on the shaded back.
    for x,z in ((-.25,2.19),(.18,2.86),(-.36,2.70)):
        t.blob((.26,.16,.27),(x,-.42,z),'leaf_dark',rough=.10)
    deliver(m,{'x':(-1.65,1.65),'y':(-.65,.65),'z':(0,4.05)})
