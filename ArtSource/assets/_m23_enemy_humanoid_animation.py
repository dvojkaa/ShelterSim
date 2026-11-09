"""Bake five coordinated Generic mesh-transform takes without changing ss_art.
A stationary root's NLA custom channels drive existing separately pivoted meshes.
FBX NLA export evaluates and bakes all six meshes together for each root strip.
"""
import bpy, math, os
from mathutils import Vector, Matrix, Euler

PARTS=('Torso','Head','Arm_L','Arm_R','Leg_L','Leg_R')
CLIPS=(('Idle',1,81),('Walk',101,133),('Attack',153,183),('Hit',203,215),('Death',235,275))

def animate_export(objs,name,kind,path):
    for old in list(bpy.data.actions):
        if old.users==0:bpy.data.actions.remove(old)
    scene=bpy.context.scene
    scene.render.fps=40
    scene.frame_start=0; scene.frame_end=275
    root=bpy.data.objects.new(name+'_AnimationControl',None)
    bpy.context.collection.objects.link(root)
    root['animation_notes']='Idle/Walk loop; Attack impact at 0.5 s; Hit one-shot; Death holds final pose. No root motion.'
    pivots={o.name:o.location.copy() for o in objs}
    rear=max(-(o.matrix_world@v.co).y for o in objs for v in o.data.vertices)
    death_raise=max(.04,rear-.12)
    # Exact final ground contact, including outward arm relaxation and backpack.
    fall_matrix=Matrix.Rotation(math.radians(88),3,'X')
    floor_pivot=Vector((0,0,.16))
    resting=[]
    for ob in objs:
        angle=-.22 if ob.name=='Arm_L' else .18 if ob.name=='Arm_R' else 0.
        local=Euler((math.radians(88),0,angle)).to_matrix()
        final_pivot=floor_pivot+fall_matrix@(pivots[ob.name]-floor_pivot)
        resting.extend((final_pivot+local@v.co).z for v in ob.data.vertices)
    death_raise=.012-min(resting)
    # Mesh drivers are a Blender authoring mechanism; exported FBX contains baked transforms.
    for ob in objs:
        for prop in ('rotation_euler','location'):
            for axis in range(3):
                channel=f'{ob.name}_{prop}_{axis}'
                root[channel]=0.
                fc=ob.driver_add(prop,axis)
                var=fc.driver.variables.new(); var.name='v'; var.type='SINGLE_PROP'
                var.targets[0].id=root; var.targets[0].data_path=f'["{channel}"]'
                fc.driver.expression='v' if prop=='rotation_euler' else f'{pivots[ob.name][axis]:.9f}+v'
    root.animation_data_create()
    for clip,start,end in CLIPS:
        act=bpy.data.actions.new(clip)
        root.animation_data.action=act
        act['clip_name']=clip
        act['loop']=clip in ('Idle','Walk')
        act['duration_seconds']=(end-start)/40
        if clip=='Attack':act['impact_seconds']=.5
        for frame in range(start,end+1):
            u=(frame-start)/(end-start)
            pose={p:[Vector((0,0,0)),Vector((0,0,0))] for p in PARTS}
            def rot(p,x=0,y=0,z=0):pose[p][0]=Vector(tuple(math.radians(a) for a in (x,y,z)))
            def loc(p,x=0,y=0,z=0):pose[p][1]=Vector((x,y,z))
            corpse=kind in ('wanderer','runner','screamer','plated')
            if clip=='Idle':
                wave=math.sin(u*math.tau)
                lean=-5 if corpse else -1
                if kind=='runner':lean=-9
                rot('Torso',lean+wave*1.2,0,wave*.8)
                rot('Head',3+wave*1.5 if corpse else wave*.7,0,-wave*1.4)
                rot('Arm_L',-6+wave*2.2 if corpse else wave,0,-1)
                rot('Arm_R',-3-wave*2,0,1)
                # Feet remain planted; subtle chest breath comes from its own scale-free pivot.
            elif clip=='Walk':
                wave=math.sin(u*math.tau); sway=math.sin(u*math.tau*2)
                stride=34 if kind=='runner' else 23
                rot('Leg_L',wave*stride); rot('Leg_R',-wave*stride)
                rot('Arm_L',-wave*(26 if kind=='runner' else 19)-5)
                rot('Arm_R',wave*19-4)
                rot('Torso',-12 if kind=='runner' else -4,0,wave*2)
                rot('Head',3,0,-wave*2)
                loc('Torso',z=abs(sway)*.012)
            elif clip=='Attack':
                # Piecewise readable windup -> strike at .5s -> return to authored stance.
                keys=((0,0),(.42,1),(.6666667,-1),(.83,-.55),(1,0))
                swing=0
                for (a,va),(b,vb) in zip(keys,keys[1:]):
                    if a<=u<=b:
                        t=(u-a)/(b-a); swing=va+(vb-va)*t; break
                if kind=='screamer':
                    pulse=math.sin(math.pi*u)
                    rot('Head',-18*pulse)
                    rot('Torso',-7*pulse)
                    rot('Arm_L',-48*pulse,0,-18*pulse)
                    rot('Arm_R',-48*pulse,0,18*pulse)
                elif kind=='runner':
                    rot('Torso',swing*12-5); rot('Head',-swing*6)
                    rot('Arm_L',swing*65-22,0,-swing*8)
                    rot('Arm_R',swing*65-22,0,swing*8)
                elif kind=='breacher':
                    rot('Arm_R',-swing*75-15,0,-swing*15)
                    rot('Arm_L',swing*18,0,-8*math.sin(math.pi*u))
                    rot('Torso',swing*9,0,swing*10)
                    rot('Head',-swing*5,0,-swing*6)
                else:
                    rot('Arm_R',-swing*(80 if kind=='plated' else 68)-12,0,-swing*9)
                    rot('Arm_L',swing*22-10,0,-swing*6)
                    rot('Torso',swing*9,0,swing*7)
                    rot('Head',-swing*6,0,-swing*5)
                # Blend toward neutral at ends: every one-shot begins/ends at the authored pose.
                fade=min(1,u*12,(1-u)*12)
                for p in PARTS:pose[p][0]*=fade
            elif clip=='Hit':
                amount=math.sin(math.pi*min(1,u*1.7)) if u<1/1.7 else 0
                rot('Torso',amount*15,0,-amount*5)
                rot('Head',amount*23,0,amount*8)
                rot('Arm_L',amount*14); rot('Arm_R',amount*18)
            elif clip=='Death':
                # Whole-character fall around ground reference, keeping stationary root.
                # Each existing pivot moves on the same arc; final pose is held, no recovery.
                fall=min(1,max(0,(u-.08)/.77)); fall=fall*fall*(3-2*fall)
                angle=math.radians(88)*fall
                mat=Matrix.Rotation(angle,3,'X')
                ground=Vector((0,0,.16))
                for p in PARTS:
                    new=ground+mat@(pivots[p]-ground)+Vector((0,0,death_raise*fall))
                    pose[p][0].x=angle
                    pose[p][1]=new-pivots[p]
                # Relax arms outward on the ground without changing the fall's overall arc.
                pose['Arm_L'][0].z=-.22*fall
                pose['Arm_R'][0].z=.18*fall
            if clip!='Death':
                # Upper-body pivots follow the chest motion, keeping neck/shoulders joined.
                chest=Euler(pose['Torso'][0]).to_matrix()
                for p in ('Head','Arm_L','Arm_R'):
                    offset=pivots[p]-pivots['Torso']
                    pose[p][1] += chest@offset-offset+pose['Torso'][1]
            for p in PARTS:
                for prop,values in (('rotation_euler',pose[p][0]),('location',pose[p][1])):
                    for axis,value in enumerate(values):
                        channel=f'{p}_{prop}_{axis}'
                        root[channel]=value
                        root.keyframe_insert(data_path=f'["{channel}"]',frame=frame,group=p)
        for layer in act.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for fc in bag.fcurves:
                        for key in fc.keyframe_points:key.interpolation='LINEAR'
        root.animation_data.action=None
        track=root.animation_data.nla_tracks.new(); track.name=clip
        strip=track.strips.new(clip,start,act)
        strip.extrapolation='NOTHING'; strip.blend_type='REPLACE'
    # Zero custom-property base values; all clips begin beyond frame zero.
    for p in PARTS:
        for prop in ('rotation_euler','location'):
            for axis in range(3):root[f'{p}_{prop}_{axis}']=0.
    scene.frame_set(0); bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    for ob in [root]+objs:ob.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'MESH','EMPTY'},
        axis_forward='Z',axis_up='Y',global_scale=1.,apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,
        mesh_smooth_type='FACE',use_mesh_modifiers=False,add_leaf_bones=False,
        bake_anim=True,bake_anim_use_nla_strips=True,bake_anim_use_all_actions=False,
        bake_anim_step=1.,bake_anim_simplify_factor=0.,use_triangles=True,
        path_mode='STRIP',embed_textures=False)
    scene.frame_set(0)
    print('ANIMATION_EXPORTED',name,'Idle=2.0 Walk=0.8 Attack=0.75 Hit=0.3 Death=1.0 fps=40',flush=True)
    return root
