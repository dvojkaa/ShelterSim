"""Original M23 boss silhouettes. World-space geometry, +Y face, six rigid animation pivots."""
import math
from pathlib import Path
import bpy
from mathutils import Vector, Euler
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
    animate_boss(m)
    path=Path(__file__).resolve().parents[1]/'blend'/(m.name+'.blend')
    path.parent.mkdir(exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.context.preferences.filepaths.file_preview_type='NONE'
    bpy.ops.wm.save_as_mainfile(filepath=str(path))


def animate_boss(model):
    """Five whole-character transform takes, driven by one NLA controller.

    The FBX exporter isolates NLA strips; a single controller therefore bakes all
    six driven mesh transforms in each take, instead of exporting one limb/take.
    """
    scene=bpy.context.scene
    scene.render.fps=30
    root=bpy.data.objects.new('BossMotion',None)
    bpy.context.collection.objects.link(root)
    visual_root=bpy.data.objects.new(model.name,None)
    bpy.context.collection.objects.link(visual_root)
    root.parent=visual_root
    root['animation_notes']='Idle/Walk loop; Attack impact 0.65s; Hit/Death one-shot; no root motion'
    parts={n:bpy.data.objects[n] for n in model.parts if n in bpy.data.objects and bpy.data.objects[n].type=='MESH'}
    torso=parts['Torso']
    for n,obj in parts.items():
        world=obj.matrix_world.copy()
        obj.parent=visual_root
        obj.matrix_world=world
    scene.view_layers.update() if hasattr(scene.view_layers,'update') else bpy.context.view_layer.update()
    base={n:obj.location.copy() for n,obj in parts.items()}
    sole_extent={n:max(abs(v.co.y) for v in obj.data.vertices) for n,obj in parts.items() if n.startswith('Leg_')}
    # Driver controls remain editable in the native scene; FBX contains sampled transforms.
    for n,obj in parts.items():
        obj.rotation_mode='XYZ'
        for channel in ('rx','ry','rz','tx','ty','tz'):
            key=n+'_'+channel
            root[key]=0.0
        for i,axis in enumerate('xyz'):
            for path,channel in (('rotation_euler','r'+axis),('location','t'+axis)):
                fc=obj.driver_add(path,i)
                driver=fc.driver
                var=driver.variables.new();var.name='value'
                var.targets[0].id=root;var.targets[0].data_path='["'+n+'_'+channel+'"]'
                driver.expression='value' if path=='rotation_euler' else str(base[n][i])+'+value'
                # Legs rotate around their planted sole through a pivot correction.
                if n.startswith('Leg_') and path=='location' and i in (1,2):
                    var=driver.variables.new();var.name='angle'
                    var.targets[0].id=root;var.targets[0].data_path='["'+n+'_rx"]'
                    height=base[n].z
                    driver.expression+=('-'+str(height)+'*sin(angle)' if i==1 else '+'+str(height)+'*(cos(angle)-1)+'+str(sole_extent[n])+'*abs(sin(angle))')
    root.animation_data_create()
    colossus=model.name=='boss_root_colossus'
    heavy=model.name=='boss_gatekeeper'
    durations={'Idle':60,'Walk':27,'Attack':30,'Hit':11,'Death':39}
    controls=[n+'_'+c for n in parts for c in ('rx','ry','rz','tx','ty','tz')]
    for clip,frames in durations.items():
        action=bpy.data.actions.new(clip)
        root.animation_data.action=action
        start=1+list(durations).index(clip)*100
        for frame in range(frames+1):
            u=frame/frames
            values={key:0.0 for key in controls}
            def put(part,**channels):
                for channel,value in channels.items():values[part+'_'+channel]=value
            if clip=='Idle':
                wave=math.sin(u*math.tau)
                put('Torso',rx=math.radians(1.5)*wave,ty=.005*wave)
                put('Head',rx=math.radians(-1)*wave)
                put('Arm_L',rx=math.radians(2)*wave)
                put('Arm_R',rx=math.radians(-2)*wave)
            elif clip=='Walk':
                wave=math.sin(u*math.tau)
                stride=math.radians(8 if colossus else 11)*wave
                put('Leg_L',rx=stride);put('Leg_R',rx=-stride)
                put('Arm_L',rx=-stride*.65);put('Arm_R',rx=stride*.65)
                put('Torso',rz=math.radians(2)*wave,tz=-.015*(1-math.cos(u*math.tau*2)))
                put('Head',rz=math.radians(-1.5)*wave)
            elif clip=='Attack':
                # Slow anticipation, decisive contact at frame 20 (~0.65s), then recovery.
                if u<=.48: lift=u/.48;strike=0
                elif u<=.65: lift=1-(u-.48)/.17;strike=(u-.48)/.17
                else:lift=0;strike=max(0,1-(u-.65)/.35)
                put('Torso',rx=math.radians(8)*lift-math.radians(13)*strike)
                put('Head',rx=-math.radians(6)*lift+math.radians(8)*strike)
                if colossus:
                    put('Arm_L',rx=math.radians(125)*lift+math.radians(35)*strike,rz=math.radians(-12)*lift)
                    put('Arm_R',rx=math.radians(125)*lift+math.radians(35)*strike,rz=math.radians(12)*lift)
                elif heavy:
                    put('Arm_L',rx=math.radians(145)*lift+math.radians(40)*strike)
                    put('Arm_R',rx=math.radians(20)*lift)
                else:
                    put('Arm_R',rx=math.radians(125)*lift+math.radians(40)*strike)
                    put('Arm_L',rx=math.radians(18)*lift)
            elif clip=='Hit':
                recoil=math.sin(math.pi*u)
                put('Torso',rx=math.radians(8)*recoil,rz=math.radians(-3)*recoil)
                put('Head',rx=math.radians(10)*recoil)
                put('Arm_L',rx=math.radians(9)*recoil);put('Arm_R',rx=math.radians(9)*recoil)
            else:
                collapse=min(1,u/.85);ease=collapse*collapse*(3-2*collapse)
                angle=-math.radians(66)*ease
                hip=base['Leg_L'].z
                put('Leg_L',rx=angle);put('Leg_R',rx=angle)
                put('Torso',rx=-math.radians(76)*ease,ty=-hip*math.sin(angle),tz=hip*(math.cos(angle)-1)+sole_extent['Leg_L']*abs(math.sin(angle)))
                put('Head',rx=math.radians(12)*ease)
                put('Arm_L',rx=math.radians(25)*ease,rz=-math.radians(12)*ease)
                put('Arm_R',rx=math.radians(20)*ease,rz=math.radians(12)*ease)
                if model.name=='boss_shift_commander':
                    values['Arm_L_ty']=-.065*ease
            # Compose torso motion into upper-part sibling transforms. This bakes
            # connected motion without nesting Head/arms below Torso, so runtime
            # visual.Find("Head") continues to resolve the original part names.
            torso_rotation=Euler(tuple(values['Torso_r'+a] for a in 'xyz'),'XYZ').to_matrix()
            torso_position=base['Torso']+Vector(tuple(values['Torso_t'+a] for a in 'xyz'))
            for n in ('Head','Arm_L','Arm_R'):
                local_offset=Vector(tuple(values[n+'_t'+a] for a in 'xyz'))
                position=torso_position+torso_rotation@(base[n]-base['Torso']+local_offset)
                rotation=(torso_rotation@Euler(tuple(values[n+'_r'+a] for a in 'xyz'),'XYZ').to_matrix()).to_euler('XYZ')
                for i,a in enumerate('xyz'):
                    values[n+'_t'+a]=position[i]-base[n][i]
                    values[n+'_r'+a]=rotation[i]
            for key,value in values.items():
                root[key]=value
                root.keyframe_insert(data_path='["'+key+'"]',frame=start+frame,group=key.rsplit('_',1)[0])
        track=root.animation_data.nla_tracks.new();track.name=clip
        strip=track.strips.new(clip,start,action);strip.name=clip
        strip.extrapolation='NOTHING';strip.blend_type='REPLACE'
        root.animation_data.action=None
    scene.frame_start=1;scene.frame_end=61
    scene.frame_set(1)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    for obj in [visual_root,root,*parts.values()]:obj.select_set(True)
    bpy.context.view_layer.objects.active=root
    from ss_art import MODELS_DIR
    bpy.ops.export_scene.fbx(filepath=str(Path(MODELS_DIR)/(model.name+'.fbx')),
        use_selection=True,object_types={'MESH','EMPTY'},axis_forward='Z',axis_up='Y',
        global_scale=1.0,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,mesh_smooth_type='FACE',use_mesh_modifiers=False,
        add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False,bake_anim_step=1,bake_anim_simplify_factor=0,
        use_triangles=True,path_mode='STRIP',embed_textures=False)
    print('BOSS_ANIMATION_EXPORT',model.name,'Idle=2s Walk=0.9s Attack=1s Hit=0.366667s Death=1.3s; five NLA takes; fps=30',flush=True)


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
