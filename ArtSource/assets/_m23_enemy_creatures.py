"""Three original wasteland creatures. Metres, feet at z=0, forward +Y.
Named mesh pivots are optional animation attachment points, not a rig.
"""
import os
import math
from mathutils import Matrix, Vector
import bpy
from ss_art import Model, finish, ROOT


def limb(p, points, radius, color='bark'):
    p.tube(points, radius, color, seg=6)
    for q in points[1:-1]:
        p.blob((radius*2.1,)*3,q,color,rough=.08)


def rootwalker(m):
    torso=m.part('Torso',(0,0,1.02))
    torso.blob((.43,.33,.62),(0,-.03,1.34),'army_green_dark',rough=.1,subdiv=2)
    torso.blob((.34,.28,.31),(0,0,1.01),'cloth_dark',rough=.1)
    # Torn vest leaves the exposed, branch-bound rib cage visible.
    torso.poly([(-.21,-.26),(-.19,.23),(-.06,.3),(.02,.08),(.03,-.23)],.22,(-.03,-.02,1.34),'cloth_dark')
    for z in (1.2,1.32,1.44):
        torso.tube([(-.14,.11,z),(-.05,.19,z-.025),(.14,.14,z+.025)],.025,'bone',seg=5)
    for s in (-1,1):
        limb(torso,[(s*.08,.03,.9),(s*.18,.16,1.24),(s*.12,.12,1.56),(s*.22,-.04,1.7)],.036,'bark_light')
    # Branch crown is a part of the head and turns with it.
    head=m.part('Head',(0,.01,1.68))
    head.blob((.29,.27,.36),(0,.05,1.82),'bark_dead',rough=.06,subdiv=2)
    head.box((.19,.055,.12),(0,.176,1.82),'wood_dark',rot=(8,0,0))
    for s in (-1,1):
        head.box((.055,.028,.035),(s*.061,.208,1.85),'mustard',rot=(0,s*12,s*8))
        limb(head,[(s*.1,-.03,1.91),(s*.18,-.08,2.1),(s*.3,-.035,2.18)],.025)
        limb(head,[(s*.18,-.08,2.09),(s*.16,-.16,2.19)],.019,'bark_light')
    head.box((.125,.07,.075),(0,.151,1.68),'bone',rot=(10,0,0),bevel=.012)
    for x in (-.041,0,.041):
        head.box((.014,.01,.04),(x,.191,1.691),'wood_dark')
    for s,side in ((-1,'L'),(1,'R')):
        arm=m.part('Arm_'+side,(s*.23,0,1.52))
        limb(arm,[(s*.19,0,1.52),(s*.3,.015,1.19),(s*.32,.15,.9)],.075,'army_green_dark')
        limb(arm,[(s*.23,-.035,1.56),(s*.34,.035,1.23),(s*.34,.16,.88)],.035,'bark_light')
        for off in (-.06,0,.06):
            limb(arm,[(s*.32,.14,.95),(s*(.32+off),.2,.79),(s*(.33+off),.25,.72)],.019,'bark')
        limb(arm,[(s*.28,-.005,1.4),(s*.39,-.06,1.58),(s*.4,-.02,1.72)],.025)
        leg=m.part('Leg_'+side,(s*.11,0,1.02))
        limb(leg,[(s*.11,0,1.03),(s*.14,-.035,.56),(s*.12,.025,.13)],.083,'bark')
        limb(leg,[(s*.15,-.06,.8),(s*.2,.04,.39),(s*.16,.14,.08)],.03,'bark_light')
        leg.box((.19,.31,.105),(s*.12,.085,.0525),'wood_dark',bevel=.025)
        for off in (-.07,.06):
            limb(leg,[(s*.12,.03,.14),(s*(.12+off),.23,.04)],.027,'bark_light')


def mirelurker(m):
    torso=m.part('Torso',(0,-.1,.48))
    torso.blob((.8,1.03,.65),(0,-.12,.55),'spruce',rough=.08,subdiv=2,colors=['spruce','spruce','army_green_dark'])
    torso.blob((.62,.85,.23),(0,-.06,.34),'leaf_brown',rough=.05,subdiv=2)
    # Raised overlapping back scutes stay visible above shallow marsh water.
    for i,(y,z) in enumerate(((-.52,.68),(-.31,.82),(-.08,.87),(.12,.82))):
        torso.blob((.54,.29,.21),(0,y,z),'army_green',rough=.06)
        torso.poly([(-.075,0),(0,.17),(.075,0)],.11,(0,y,z+.06),'leaf_dry')
    limb(torso,[(0,-.47,.48),(.07,-.59,.37),(0,-.65,.24)],.075,'spruce')
    head=m.part('Head',(0,.32,.51))
    head.blob((.81,.59,.37),(0,.37,.5),'spruce_light',rough=.035,subdiv=2)
    head.box((.63,.27,.075),(0,.48,.39),'wood_dark',bevel=.025)
    head.box((.61,.29,.075),(0,.475,.337),'army_green',bevel=.025)
    for x in (-.24,-.14,-.045,.055,.15,.24):
        head.cyl(.025,.075,(x,.615,.391),'bone',seg=5,r2=0)
    for s in (-1,1):
        head.blob((.19,.22,.17),(s*.26,.37,.67),'spruce',rough=.03)
        head.box((.09,.042,.052),(s*.26,.46,.685),'hazard_yellow',bevel=.01)
        head.box((.021,.009,.05),(s*.26,.485,.685),'black')
        for y in (.18,.32):
            head.tube([(s*.31,y,.51),(s*.41,y-.07,.42)],.025,'leaf_dry',seg=5)
    for s,side in ((-1,'L'),(1,'R')):
        for front,y in ((True,.24),(False,-.4)):
            p=m.part(('Foreleg_' if front else 'Hindleg_')+side,(s*.28,y,.5))
            limb(p,[(s*.27,y,.49),(s*.46,y-.1,.28),(s*.43,y+.1,.09)],.09,'spruce')
            p.blob((.21,.3,.13),(s*.43,y+.14,.08),'army_green_dark',rough=.03)
            p.box((.16,.18,.035),(s*.43,y+.15,.0175),'army_green_dark',bevel=.008)
            for off in (-.075,0,.075):
                limb(p,[(s*.43+off,y+.18,.065),(s*.43+off,y+.3,.035)],.025,'bone')


def iron_boar(m):
    torso=m.part('Torso',(0,-.12,.7))
    torso.blob((.87,1.1,.78),(0,-.12,.71),'leather_dark',rough=.05,subdiv=2)
    torso.blob((.88,.65,.74),(0,.13,.76),'rust_dark',rough=.04,subdiv=2)
    # Layered scrap armour rides the shoulders rather than replacing the animal.
    for y,z in ((-.4,1.0),(-.16,1.09),(.08,1.12)):
        torso.box((.7,.28,.11),(0,y,z),'steel_dark',rot=(7,0,0),bevel=.035,taper=(.85,.85))
        for s in (-1,1):
            torso.box((.12,.25,.29),(s*.365,y,z-.105),'gunmetal',rot=(0,s*24,0),bevel=.018)
            torso.cyl(.025,.018,(s*.24,y,z+.063),'rust_light',seg=6)
    for y in (-.43,-.26,-.08,.1):
        torso.poly([(-.05,0),(0,.14),(.05,0)],.035,(0,y,1.12),'hair')
    limb(torso,[(0,-.59,.71),(.1,-.76,.8),(.16,-.8,.72)],.027,'leather')
    head=m.part('Head',(0,.35,.76))
    head.blob((.6,.57,.59),(0,.43,.72),'leather',rough=.04,subdiv=2)
    head.blob((.39,.47,.33),(0,.66,.56),'leather_dark',rough=.03,subdiv=2)
    head.box((.32,.12,.22),(0,.837,.55),'skin_dark',rot=(7,0,0),bevel=.035)
    for s in (-1,1):
        head.box((.06,.018,.05),(s*.086,.901,.57),'black',bevel=.008)
        head.poly([(-.09,0),(0,.3),(.1,.04)],.095,(s*.23,.32,.89),'leather_dark',rot=(0,s*25,s*-12))
        head.box((.062,.045,.042),(s*.25,.596,.785),'warning_red',rot=(0,0,s*-15))
        head.box((.33,.26,.065),(s*.13,.43,.955),'steel_dark',rot=(14,s*14,0),bevel=.022)
        limb(head,[(s*.2,.72,.45),(s*.31,.76,.56),(s*.32,.78,.73)],.038,'bone')
        head.cyl(.039,.12,(s*.32,.78,.77),'bone',seg=6,r2=0)
    for s,side in ((-1,'L'),(1,'R')):
        for front,y in ((True,.23),(False,-.44)):
            p=m.part(('Foreleg_' if front else 'Hindleg_')+side,(s*.27,y,.65))
            limb(p,[(s*.27,y,.65),(s*.31,y-.04,.3),(s*.29,y+.025,.09)],.095,'leather_dark')
            p.box((.18,.23,.13),(s*.29,y+.04,.065),'black',bevel=.02)
            p.box((.012,.024,.08),(s*.29,y+.163,.07),'wood_grey')
            if front:
                p.box((.16,.16,.24),(s*.3,y+.025,.34),'steel_dark',rot=(8,0,0),bevel=.02)


BUILD={'enemy_rootwalker':rootwalker,'enemy_mirelurker':mirelurker,'enemy_iron_boar':iron_boar}
LIMITS={'enemy_rootwalker':(.9,.85,2.23),'enemy_mirelurker':(1.15,1.5,1.1),'enemy_iron_boar':(1.2,1.85,1.34)}


def build(name):
    m=Model(name,seed=23091)
    BUILD[name](m)
    x,y,z=LIMITS[name]
    finish(m,bounds={'x':(-x/2,x/2),'y':(-y/2,y/2),'z':(0,z)})
    # Keep the native file focused on exported meshes; preview lights/floor are staging.
    for ob in list(bpy.data.objects):
        if ob.name not in m.parts:
            bpy.data.objects.remove(ob,do_unlink=True)
    bpy.context.scene.camera=None
    animate_and_export(name,list(bpy.data.objects))
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.context.scene.unit_settings.scale_length=1
    bpy.context.preferences.filepaths.save_version=0
    bpy.context.preferences.filepaths.file_preview_type='NONE'
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'ArtSource','blend',name+'.blend'))


CLIPS={'Idle':60,'Walk':24,'Attack':23,'Hit':9,'Death':30}


def animate_and_export(name,objects):
    """Five multi-slot actions, baked together into named FBX takes.

    Blender's stock per-NLA export mutes every strip except one, which splits
    object animation. The scoped adapter below calls the stock global sampler
    once per multi-object action. No exporter file or shared library is edited.
    Unity must set Loop Time for Idle/Walk; FBX takes do not encode Unity flags.
    """
    from io_scene_fbx import export_fbx_bin as exporter
    scene=bpy.context.scene
    scene.render.fps=30
    scene.frame_start=0
    scene.frame_end=60
    # Actions from earlier batch assets must not acquire .001 suffixes.
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    rest={o.name:(o.location.copy(),o.rotation_euler.copy(),o.scale.copy()) for o in objects}
    actions={}
    for clip,end in CLIPS.items():
        action=bpy.data.actions.new(clip)
        action.use_fake_user=True
        action['loop']=clip in ('Idle','Walk')
        action['duration_seconds']=end/30
        slots={}
        for ob in objects:
            ob.animation_data_create()
            ob.animation_data.action=action
            slot=action.slots.new(id_type='OBJECT',name=ob.name)
            ob.animation_data.action_slot=slot
            slots[ob.name]=slot
        for frame in range(end+1):
            t=frame/end
            for ob in objects:
                loc,rot,scale=rest[ob.name]
                ob.location=loc
                ob.rotation_euler=rot
                ob.scale=scale
                part=ob.name
                if clip=='Idle':
                    breath=(1-math.cos(t*math.tau))*.5
                    if part=='Torso':
                        ob.scale.z=1+.018*breath
                        ob.rotation_euler.y=math.radians(1.5*math.sin(t*math.tau))
                    if part=='Head':
                        ob.rotation_euler.x=math.radians(2*math.sin(t*math.tau))
                    if part.startswith('Arm_'):
                        ob.rotation_euler.x=math.radians(3*math.sin(t*math.tau))
                elif clip=='Walk':
                    sign=-1 if part.endswith('_L') else 1
                    if part.startswith('Hindleg_'): sign*=-1
                    if part.startswith(('Leg_','Foreleg_','Hindleg_')):
                        ob.rotation_euler.x=math.radians(13*sign*math.sin(t*math.tau))
                    elif part.startswith('Arm_'):
                        ob.rotation_euler.x=math.radians(-18*sign*math.sin(t*math.tau))
                    elif part=='Torso':
                        ob.rotation_euler.y=math.radians(2*math.sin(t*math.tau))
                    elif part=='Head':
                        ob.rotation_euler.x=math.radians(3*math.sin(t*math.tau))
                elif clip=='Attack':
                    # Deliberate windup, impact exactly at frame 15 (0.5 s), recovery.
                    if frame<=9: a=-.35*frame/9
                    elif frame<=15: a=-.35+1.35*(frame-9)/6
                    else: a=1-(frame-15)/(end-15)
                    if part=='Torso':
                        ob.rotation_euler.x=math.radians(-8*a)
                        ob.location.y+=.07*a
                    elif part=='Head':
                        ob.rotation_euler.x=math.radians((-25 if name!='enemy_rootwalker' else -12)*a)
                        ob.location.y+=.07*a
                    elif part.startswith('Arm_'):
                        ob.rotation_euler.x=math.radians(60*a)
                        ob.rotation_euler.y=math.radians((18 if part.endswith('_L') else -18)*a)
                elif clip=='Hit':
                    a=math.sin(math.pi*t)
                    if part in ('Torso','Head'):
                        ob.rotation_euler.x=math.radians(13*a)
                        ob.location.y-=.045*a
                    elif part.startswith('Arm_'):
                        ob.rotation_euler.x=math.radians(-12*a)
                else:
                    # Whole-body fall, with the imported root held fixed at the origin.
                    a=t*t*(3-2*t)
                    turn=Matrix.Rotation(math.radians(86*a),4,'Y')
                    ob.location=turn@loc
                    ob.rotation_euler=(turn.to_3x3()@rot.to_matrix()).to_euler()
            bpy.context.view_layer.update()
            # Foot contact per sampled frame; no foot or tusk penetrates the floor.
            floor=min((ob.matrix_world@v.co).z for ob in objects for v in ob.data.vertices)
            for ob in objects:
                ob.location.z-=floor
                for channel in ('location','rotation_euler','scale'):
                    ob.keyframe_insert(channel,frame=frame,group=ob.name)
        actions[clip]=(action,slots,end)
    def bind(action,slots):
        for ob in objects:
            ob.animation_data.action=action
            ob.animation_data.action_slot=slots[ob.name]
    def together(scene_data):
        takes=[]
        animated=set()
        for clip,(action,slots,end) in actions.items():
            bind(action,slots)
            scene.frame_set(0)
            take=exporter.fbx_animations_do(scene_data,action,0,end,True,force_keep=True)
            assert take is not None,clip
            takes.append(take)
            for elem_key,(_,curvenodes) in take[1].items():
                for prop in curvenodes: animated.add((elem_key,prop))
            print('CREATURE_TAKE',name,clip,'seconds=',end/30,'parts=',len(take[1]),'loop=',clip in ('Idle','Walk'),flush=True)
        return takes,animated,0,60
    original=exporter.fbx_animations
    exporter.fbx_animations=together
    try:
        bpy.ops.object.select_all(action='DESELECT')
        for ob in objects: ob.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT,'Assets','Art','Models',name+'.fbx'),
            use_selection=True,object_types={'MESH'},axis_forward='Z',axis_up='Y',
            global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
            bake_space_transform=True,mesh_smooth_type='FACE',use_mesh_modifiers=False,
            add_leaf_bones=False,use_triangles=True,path_mode='STRIP',embed_textures=False,
            bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,
            bake_anim_step=1,bake_anim_simplify_factor=0)
    finally:
        exporter.fbx_animations=original
        action,slots,_=actions['Idle']
        bind(action,slots)
        scene.frame_set(0)
