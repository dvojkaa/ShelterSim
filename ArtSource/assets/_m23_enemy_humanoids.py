"""Six original wasteland humanoids. +Y faces, metre units, floor-centre roots.
Raider-compatible world-space limb pivots; clothing overlaps each moving joint.
"""
import math, os
import bpy
from mathutils import Vector
from ss_art import Model, finish, _world_bounds, _render_preview

def segment(p,a,b,ra,rb,col,n=6):
    a,b=Vector(a),Vector(b); d=b-a
    p.cyl(ra,d.length,(a+b)/2,col,rot=tuple(math.degrees(v) for v in d.to_track_quat('Z','Y').to_euler()),seg=n,r2=rb)

def plate(p,pts,y,z,col,th=.025):
    p.poly(pts,th,(0,y,z),col)

def build(kind):
    name='enemy_'+kind
    m=Model(name,seed=541+['wanderer','runner','scavenger','screamer','plated','breacher'].index(kind))
    civilian=kind in ('wanderer','runner','screamer')
    jacket={'wanderer':'fabric_blue','runner':'cloth_red','scavenger':'army_green','screamer':'fabric_white','plated':'mustard_dark','breacher':'car_blue'}[kind]
    corpse=civilian or kind=='plated'
    flesh='glass_dirty' if corpse else 'skin_dark'
    highlight='fabric_stain' if corpse else 'skin'
    pants='cloth_dark' if civilian else 'army_green_dark'
    slim=kind in ('runner','screamer')
    w=.34 if slim else .40
    t=m.part('Torso',(0,0,1))
    t.box((.35,.235,.22),(0,0,.99),pants,bevel=.018)
    t.box((w-.05,.22,.26),(0,0,1.15),jacket,taper=(1.12,1.05))
    t.box((w,.25,.34),(0,.012,1.37),jacket,bevel=.025,taper=(1.12,1.06))
    t.box((.44,.22,.105),(0,.012,1.51),jacket,taper=(.7,.7))
    t.box((.37,.255,.06),(0,0,1.04),'leather_dark')
    t.box((.055,.026,.05),(0,.14,1.04),'rust_light')
    # Visible jacket seam and offset chest pocket; damaged hem.
    t.box((.018,.018,.40),(.018,.147,1.29),'leather_dark')
    t.box((.105,.025,.11),(-.09,.15,1.36),'fabric_stain' if civilian else 'leather')
    if civilian:
        plate(t,[(-.17,0),(-.18,-.12),(-.1,-.075),(-.035,-.17),(.03,-.10),(.12,-.14),(.17,0)],.10,.99,jacket)
        t.box((.10,.02,.09),(.11,-.13,1.28),'fabric_stain',rot=(0,12,0))
    # Six animation parts. All variants maintain shoulders/hips at raider's pivots.
    arms={}
    for s,label in ((-1,'L'),(1,'R')):
        p=m.part('Arm_'+label,(s*.28,0,1.45)); arms[label]=p
        elbow=(s*.30,.025,1.12)
        hand=(s*.31,.10,.81)
        if kind=='wanderer' and s==-1:
            elbow=(-.30,.06,1.15); hand=(-.31,.27,.95)
        if kind in ('scavenger','breacher') and s==1:
            elbow=(.31,-.015,1.16); hand=(.32,.24,1.05)
        p.blob((.19,.20,.20),(s*.265,.008,1.45),flesh if kind=='runner' else jacket,rough=.025,subdiv=1)
        segment(p,(s*.27,0,1.47),elbow,.085 if not slim else .066,.058,flesh if kind=='runner' else jacket)
        segment(p,elbow,hand,.061,.044,flesh if civilian else jacket)
        p.box((.085,.10,.12),hand,flesh if civilian else 'leather_dark',bevel=.008)
        p.box((.033,.05,.06),(hand[0]-s*.044,hand[1]+.015,hand[2]+.015),highlight if civilian else 'leather')
        if not civilian:
            segment(p,(elbow[0],elbow[1],elbow[2]-.04),(hand[0],hand[1]-.012,hand[2]+.065),.069,.057,'leather_dark')
    for s,label in ((-1,'L'),(1,'R')):
        p=m.part('Leg_'+label,(s*.12,0,.95))
        segment(p,(s*.12,0,1.01),(s*.135,.008,.53),.104 if not slim else .085,.067,pants)
        segment(p,(s*.135,.008,.58),(s*.145,-.01,.18),.07,.049,pants)
        p.box((.12,.15,.19),(s*.145,-.005,.18),'leather_dark',taper=(1.05,1.0))
        p.box((.14,.28,.10),(s*.145,.055,.075),'leather_dark',bevel=.018)
        p.box((.145,.295,.035),(s*.145,.055,.0175),'rubber')
        if s==-1 or not civilian:
            p.box((.10,.035,.125),(s*.135,.075,.55),'fabric_stain' if civilian else 'steel_dark',taper=(.85,1))
    h=m.part('Head',(0,0,1.55))
    segment(h,(0,.015,1.45),(0,.035,1.67),.07,.065,flesh)
    head_y=.055 if kind=='wanderer' else .025
    h.box((.225,.22,.265),(0,head_y,1.697),flesh if corpse else 'skin',bevel=.035,taper=(1.04,1.03))
    # Angular sockets and brows on forward (+Y) face.
    for s in (-1,1):
        h.box((.067,.015,.027),(s*.057,head_y+.116,1.73),'black',rot=(0,s*11,0))
        h.box((.021,.019,.012),(s*.057,head_y+.125,1.73),'bone' if civilian else 'fabric_stain')
        h.box((.077,.026,.021),(s*.057,head_y+.11,1.755),flesh,rot=(0,s*12,0))
    h.box((.035,.04,.05),(0,head_y+.125,1.686),highlight)
    if civilian:
        h.box((.115,.018,.03),(0,head_y+.117,1.635),'cloth_red')
        # Sparse scalp cap leaving face visible; asymmetrical hair tuft.
        h.box((.23,.218,.065),(0,head_y-.008,1.82),'hair',bevel=.018)
        h.box((.045,.055,.08),(-.07,head_y-.03,1.855),'hair',rot=(0,-18,0))
    else:
        h.box((.20,.046,.11),(0,head_y+.125,1.64),'cloth_dark',bevel=.018)
    if kind=='wanderer':
        # Torn civilian coat, one hanging cuff and worn shoe wrap.
        arms['L'].box((.13,.14,.14),(-.30,.025,1.12),jacket,taper=(1.15,1.1))
        t.box((.085,.03,.16),(.10,.15,1.22),'fabric_grey',rot=(0,-8,0))
        t.box((.06,.025,.04),(-.12,.15,1.46),'cloth_red')
        plate(t,[(-.06,-.06),(.02,-.09),(.055,-.01),(.01,.065),(-.04,.03)],.16,1.29,'cloth_dark')
        t.box((.035,.025,.085),(-.015,.18,1.29),'cloth_red',rot=(0,23,0))
        h.box((.055,.022,.065),(-.077,head_y+.123,1.66),'cloth_red',rot=(0,-15,0))
    elif kind=='runner':
        # Stripped racing silhouette: sleeveless red vest, exposed ribs, head wrap.
        for z in (1.19,1.245,1.30):
            t.box((.095,.025,.025),(.105,.142,z),'cloth_red',rot=(0,12,0))
        h.box((.055,.025,.075),(.076,head_y+.12,1.64),'cloth_red')
        h.box((.237,.235,.04),(0,head_y,1.79),'cloth_red')
        for label,s in (('L',-1),('R',1)):
            p=arms[label]
            segment(p,(s*.30,.035,1.05),(s*.31,.07,.94),.063,.055,'fabric_stain')
        m.parts['Leg_R'].box((.095,.03,.24),(.14,.064,.34),'fabric_white',rot=(-3,0,0))
    elif kind=='scavenger':
        # Loaded frame pack and rolled bedroll distinctly read from the rear.
        t.box((.36,.22,.43),(0,-.225,1.31),'army_green_dark',bevel=.025)
        t.box((.30,.025,.25),(0,-.345,1.29),'leather',bevel=.015)
        t.cyl(.085,.39,(0,-.23,1.60),'fabric_stain',rot=(0,90,0),seg=8)
        for s in (-1,1):
            t.box((.045,.038,.47),(s*.145,.149,1.29),'leather_dark')
            t.box((.06,.26,.035),(s*.12,-.23,1.59),'leather_dark')
        h.box((.26,.27,.08),(0,.015,1.82),'army_green_dark',bevel=.025)
        h.box((.25,.12,.018),(0,.145,1.79),'army_green_dark')
        t.box((.105,.09,.16),(-.205,.035,1.02),'leather',bevel=.012)
    elif kind=='screamer':
        # Open black mouth, taut throat bands, dirty medical coat and side bag.
        h.box((.10,.035,.115),(0,head_y+.126,1.638),'black',bevel=.016)
        for s in (-1,1):
            h.box((.025,.035,.12),(s*.065,head_y+.125,1.64),highlight)
        h.box((.08,.018,.018),(0,head_y+.148,1.686),'bone')
        for s in (-1,1):
            segment(h,(s*.045,.08,1.48),(s*.06,.10,1.59),.014,.011,highlight)
        t.box((.20,.10,.23),(-.225,-.03,1.02),'fabric_stain',bevel=.025)
        t.box((.15,.023,.065),(-.225,.032,1.03),'cloth_red')
        t.box((.038,.04,.54),(0,.16,1.28),'fabric_stain',rot=(0,-30,0))
        # Cloth tabs add an uneven, long coat silhouette.
        for x,z in ((-.12,.89),(.12,.92)):
            t.box((.105,.03,.19),(x,-.105,z),'fabric_white',rot=(0,x*35,0))
    elif kind=='plated':
        # Salvaged industrial front armour; exposed back and offset rusty plates.
        plate(t,[(-.22,-.19),(-.13,-.25),(.17,-.21),(.235,-.10),(.205,.22),(-.18,.24)],.176,1.31,'steel_dark',.045)
        plate(t,[(-.18,-.09),(.18,-.07),(.16,.08),(-.16,.10)],.207,1.42,'mustard',.015)
        for x in (-.15,.15):
            for z in (1.16,1.48):t.box((.025,.025,.025),(x,.224,z),'chrome')
        for s,label in ((-1,'L'),(1,'R')):
            arms[label].box((.20,.27,.09),(s*.29,.01,1.52),'steel_dark',bevel=.015,rot=(0,s*18,0))
        h.box((.27,.28,.12),(0,.015,1.81),'mustard',bevel=.025)
        h.box((.29,.31,.025),(0,.035,1.765),'mustard_dark')
        t.box((.17,.035,.24),(0,-.15,1.35),'rust_dark',rot=(0,9,0))
    elif kind=='breacher':
        # Raider demolition worker: compact welding hood, tool yoke, heavy forearm.
        h.box((.27,.16,.29),(0,.15,1.70),'steel_dark',bevel=.025)
        h.box((.18,.022,.055),(0,.242,1.75),'black')
        h.box((.135,.025,.024),(0,.255,1.75),'glass')
        for z in (1.61,1.64):h.box((.12,.02,.013),(0,.242,z),'rust')
        t.box((.34,.06,.30),(0,.17,1.31),'leather_dark',bevel=.015)
        for x in (-.10,0,.10):
            t.box((.065,.045,.15),(x,.22,1.22),'rust')
        arms['L'].box((.19,.23,.14),(-.29,0,1.51),'rust',bevel=.02)
        arms['R'].box((.15,.18,.17),(.32,.16,1.10),'steel_dark',bevel=.015)
        # Short hooked demolition iron is integral to right hand, clear of ground.
        p=arms['R']
        segment(p,(.32,.23,.94),(.32,.25,1.43),.024,.024,'rust')
        segment(p,(.32,.25,1.43),(.32,.35,1.48),.024,.021,'steel')
        segment(p,(.32,.35,1.48),(.32,.40,1.45),.021,.012,'steel')
        t.box((.29,.18,.26),(0,-.21,1.28),'gunmetal',bevel=.02)
        t.box((.045,.025,.44),(-.135,.16,1.29),'hazard_yellow')
    # Civilian zombies slump through the chest; runner leans into its charge.
    # Apply to world vertices while preserving the exact named animation pivots.
    if kind in ('wanderer','runner'):
        lean=.20 if kind=='wanderer' else .11
        for pn in ('Torso','Head','Arm_L','Arm_R'):
            for v in m.parts[pn].bm.verts:
                v.co.y += lean * max(0.,v.co.z-.95)
                if pn=='Head': v.co.z -= .055 if kind=='wanderer' else .015
    finish(m,bounds={'x':(-.46,.46),'y':(-.38,.43),'z':(0,1.92)},preview=False)
    objs=[o for o in bpy.context.scene.objects if o.type=='MESH']
    root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    os.makedirs(os.path.join(root,'blend'),exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(root,'blend',name+'.blend'))
    lo,hi=_world_bounds(objs)
    _render_preview(objs,name,lo,hi)
