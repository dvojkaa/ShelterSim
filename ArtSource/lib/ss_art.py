"""SheltreSim shared Blender art library. Run asset scripts via ArtSource/build.sh <script.py>.

Coordinates: metres. Blender X = Unity X, Blender Y = Unity Z, Blender Z = Unity Y (up).
The exporter handles the conversion, so author directly in those terms.
All colour comes from the shared palette atlas: one texture, one material for every model.
"""
import colorsys, math, os, random, sys
import bpy, bmesh
import numpy as np
from mathutils import Matrix, Vector, Euler

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
MODELS_DIR = os.path.join(ROOT, 'Assets', 'Art', 'Models')
ATLAS_PATH = os.path.join(ROOT, 'Assets', 'Art', 'Textures', 'SS_Atlas.png')
PREVIEW_DIR = os.path.join(ROOT, 'ArtSource', 'previews')

GRID = 16
CELL = 32
# Muted, sun-bleached post-apocalyptic palette (sRGB 0-255). Order is the atlas layout: never reorder.
PALETTE = {
    'concrete_light': (150,146,136), 'concrete': (116,113,106), 'concrete_dark': (78,76,72), 'asphalt': (52,51,50),
    'brick': (122,70,54), 'brick_dark': (86,50,42), 'plaster': (168,156,132), 'plaster_dirty': (128,116,96),
    'wood_light': (158,120,78), 'wood': (118,84,52), 'wood_dark': (76,54,36), 'wood_grey': (112,104,92),
    'bark': (70,54,42), 'bark_light': (98,80,62), 'bark_dead': (92,86,78), 'rope': (150,130,92),
    'steel': (128,132,136), 'steel_dark': (72,75,79), 'gunmetal': (44,46,50), 'black': (24,24,26),
    'rust': (138,76,42), 'rust_dark': (92,52,32), 'rust_light': (170,104,58), 'chrome': (178,180,180),
    'army_green': (84,92,58), 'army_green_dark': (58,64,42), 'mustard': (166,136,50), 'mustard_dark': (120,98,40),
    'hazard_yellow': (214,172,36), 'warning_red': (182,40,32), 'red_bright': (226,44,36), 'orange': (198,108,44),
    'leaf_green': (84,108,52), 'leaf_dark': (52,76,42), 'leaf_dry': (126,122,60), 'leaf_brown': (116,86,48),
    'spruce': (40,72,52), 'spruce_light': (62,96,66), 'berry_blue': (48,58,140), 'berry_light': (82,96,180),
    'stone_light': (146,144,138), 'stone': (112,110,106), 'stone_dark': (78,77,75), 'dirt': (102,84,58),
    'ore_rust': (130,70,48), 'ore_dark': (82,50,42), 'ore_metal': (180,150,110), 'sand': (156,136,98),
    'car_red': (136,60,44), 'car_blue': (66,90,112), 'car_green': (78,96,70), 'car_grey': (118,118,114),
    'glass': (58,74,78), 'glass_dirty': (92,100,88), 'rubber': (32,32,33), 'lamp_warm': (255,220,150),
    'fabric_grey': (124,126,132), 'fabric_blue': (70,82,110), 'fabric_white': (190,186,172), 'fabric_stain': (150,138,108),
    'leather': (84,60,42), 'leather_dark': (52,40,32), 'cloth_red': (112,44,36), 'cloth_dark': (58,52,50),
    'skin': (156,116,92), 'skin_dark': (118,84,66), 'hair': (40,32,28), 'bone': (196,186,160),
    'water': (70,110,120), 'plastic_blue': (58,92,122), 'paint_white': (180,178,168), 'paint_green': (72,104,84),
}
_names = list(PALETTE.keys())
assert len(_names) <= GRID * GRID


def ensure_atlas():
    """Writes SS_Atlas.png deterministically (subtle per-cell grain gives faces tonal variation)."""
    size = GRID * CELL
    rng = np.random.default_rng(20261108)
    img = np.zeros((size, size, 4), dtype=np.float32)
    img[..., 3] = 1.0
    for i in range(GRID * GRID):
        col = PALETTE[_names[i]] if i < len(_names) else (255, 0, 255)
        x0, y0 = (i % GRID) * CELL, (i // GRID) * CELL
        base = np.array(col, dtype=np.float32) / 255.0
        coarse = rng.normal(0, 1, (4, 4))
        kron = np.kron(coarse, np.ones((CELL // 4, CELL // 4)))
        grain = rng.normal(0, 0.012, (CELL, CELL)) + kron * 0.014
        if _names[i] in ('lamp_warm', 'red_bright', 'hazard_yellow') if i < len(_names) else False:
            grain *= 0.3
        for c in range(3):
            img[y0:y0 + CELL, x0:x0 + CELL, c] = np.clip(base[c] * (1.0 + grain), 0, 1)
    os.makedirs(os.path.dirname(ATLAS_PATH), exist_ok=True)
    if os.path.exists(ATLAS_PATH):
        return
    bi = bpy.data.images.new('SS_Atlas_gen', size, size, alpha=True, float_buffer=False)
    bi.colorspace_settings.name = 'Non-Color'  # values above are already sRGB-encoded
    bi.pixels.foreach_set(img.ravel())
    bi.filepath_raw = ATLAS_PATH
    bi.file_format = 'PNG'
    bi.save()
    bpy.data.images.remove(bi)


def _cell_uv(name, rng):
    if name not in PALETTE:
        raise ValueError(f"Unknown palette colour '{name}'. Available: {_names}")
    i = _names.index(name)
    u = ((i % GRID) + 0.5) / GRID
    v = ((i // GRID) + 0.5) / GRID
    half = 0.32 / GRID
    return Vector((u + rng.uniform(-half, half), v + rng.uniform(-half, half)))


class Part:
    def __init__(self, model, name, pivot):
        self.model, self.name, self.pivot = model, name, Vector(pivot)
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UVMap')

    def _paint(self, bm, faces, color):
        uv = bm.loops.layers.uv.verify()
        for f in faces:
            co = _cell_uv(color, self.model.rng)
            for l in f.loops:
                l[uv].uv = co

    def _commit(self, tmp, matrix):
        bmesh.ops.transform(tmp, matrix=matrix, verts=tmp.verts)
        me = bpy.data.meshes.new('_tmp')
        tmp.to_mesh(me)
        tmp.free()
        self.bm.from_mesh(me)
        bpy.data.meshes.remove(me)

    @staticmethod
    def _xf(loc, rot):
        return Matrix.Translation(loc) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4()

    def box(self, size, loc=(0, 0, 0), color='concrete', rot=(0, 0, 0), bevel=0.0, taper=None):
        """Axis-aligned box centred at loc. taper=(sx,sy) scales the top face. bevel in metres."""
        t = bmesh.new(); t.loops.layers.uv.new('UVMap')
        bmesh.ops.create_cube(t, size=1.0)
        bmesh.ops.scale(t, vec=size, verts=t.verts)
        if taper:
            for v in t.verts:
                if v.co.z > 0:
                    v.co.x *= taper[0]; v.co.y *= taper[1]
        if bevel > 0:
            bmesh.ops.bevel(t, geom=list(t.verts) + list(t.edges), offset=bevel, segments=1, profile=0.5, affect='EDGES')
        self._paint(t, t.faces, color)
        self._commit(t, self._xf(loc, rot))
        return self

    def cyl(self, r, h, loc=(0, 0, 0), color='steel', rot=(0, 0, 0), seg=10, r2=None, caps=True):
        """Cylinder/cone along local Z centred at loc. r2 = top radius (cone/frustum)."""
        t = bmesh.new(); t.loops.layers.uv.new('UVMap')
        bmesh.ops.create_cone(t, cap_ends=caps, cap_tris=False, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=h)
        self._paint(t, t.faces, color)
        self._commit(t, self._xf(loc, rot))
        return self

    def tube(self, points, r, color='steel', seg=8):
        """Pipe through a list of 3D points (mitred per segment; caps at both ends)."""
        for a, b in zip(points[:-1], points[1:]):
            a, b = Vector(a), Vector(b)
            d = b - a
            q = d.to_track_quat('Z', 'Y')
            t = bmesh.new(); t.loops.layers.uv.new('UVMap')
            bmesh.ops.create_cone(t, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r, depth=d.length)
            self._paint(t, t.faces, color)
            self._commit(t, Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4())
        return self

    def blob(self, size, loc=(0, 0, 0), color='stone', rot=(0, 0, 0), rough=0.18, subdiv=1, seed=None, colors=None, flat_bottom=None):
        """Rough faceted lump (displaced icosphere) of overall size. colors = optional list: random colour per face."""
        rng = random.Random(self.model.rng.random() if seed is None else seed)
        t = bmesh.new(); t.loops.layers.uv.new('UVMap')
        bmesh.ops.create_icosphere(t, subdivisions=subdiv, radius=0.5)
        for v in t.verts:
            v.co *= 1.0 + rng.uniform(-rough, rough)
        bmesh.ops.scale(t, vec=size, verts=t.verts)
        if flat_bottom is not None:
            for v in t.verts:
                if v.co.z < flat_bottom:
                    v.co.z = flat_bottom
        if colors:
            for f in t.faces:
                self._paint(t, [f], rng.choice(colors))
        else:
            self._paint(t, t.faces, color)
        self._commit(t, self._xf(loc, rot))
        return self

    def poly(self, profile, thickness, loc=(0, 0, 0), color='concrete', rot=(0, 0, 0)):
        """Extruded polygon: profile = [(x,z),...] CCW in the XZ plane, extruded symmetrically along Y.
        Use for jagged broken edges, car silhouettes, etc."""
        t = bmesh.new(); t.loops.layers.uv.new('UVMap')
        vs = [t.verts.new((p[0], -thickness / 2, p[1])) for p in profile]
        f = t.faces.new(vs)
        bmesh.ops.triangulate if False else None
        r = bmesh.ops.extrude_face_region(t, geom=[f])
        for g in r['geom']:
            if isinstance(g, bmesh.types.BMVert):
                g.co.y += thickness
        bmesh.ops.recalc_face_normals(t, faces=t.faces)
        self._paint(t, t.faces, color)
        self._commit(t, self._xf(loc, rot))
        return self


class Model:
    """One exported asset. Use m.part() for a separate transform (only when the game must move it)."""
    def __init__(self, name, seed=1):
        self.name, self.parts = name, {}
        self.rng = random.Random(seed)
        self.default = Part(self, name, (0, 0, 0))
        self.parts[name] = self.default

    def part(self, name, pivot=(0, 0, 0)):
        self.parts[name] = Part(self, name, pivot)
        return self.parts[name]

    def __getattr__(self, attr):
        if attr in ('box', 'cyl', 'tube', 'blob', 'poly'):
            return getattr(self.default, attr)
        raise AttributeError(attr)


def _clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)


def _atlas_material():
    mat = bpy.data.materials.get('SS_Atlas')
    if mat:
        return mat
    mat = bpy.data.materials.new('SS_Atlas')
    mat.use_nodes = True
    n = mat.node_tree
    bsdf = n.nodes.get('Principled BSDF')
    bsdf.inputs['Roughness'].default_value = 0.85
    tex = n.nodes.new('ShaderNodeTexImage')
    tex.image = bpy.data.images.load(ATLAS_PATH, check_existing=True)
    tex.interpolation = 'Closest'
    n.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    return mat


def _build_objects(model):
    _clear_scene()
    mat = _atlas_material()
    objs = []
    for part in model.parts.values():
        if not part.bm.verts:
            continue
        bmesh.ops.remove_doubles(part.bm, verts=part.bm.verts, dist=0.0001)
        bmesh.ops.translate(part.bm, vec=-part.pivot, verts=part.bm.verts)
        me = bpy.data.meshes.new(part.name)
        part.bm.to_mesh(me)
        for p in me.polygons:
            p.use_smooth = False
        me.materials.append(mat)
        ob = bpy.data.objects.new(part.name, me)
        ob.location = part.pivot
        bpy.context.collection.objects.link(ob)
        objs.append(ob)
    return objs


def _world_bounds(objs):
    lo = Vector((1e9, 1e9, 1e9)); hi = -lo
    for o in objs:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            for i in range(3):
                lo[i] = min(lo[i], w[i]); hi[i] = max(hi[i], w[i])
    return lo, hi


def _export_fbx(objs, name):
    os.makedirs(MODELS_DIR, exist_ok=True)
    path = os.path.join(MODELS_DIR, name + '.fbx')
    bpy.ops.object.select_all(action='DESELECT')
    # Conversion is verified against Unity by Assets/Editor/ArtImportProbe: Blender (x,y,z) -> Unity (x,z,y).
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'},
        axis_forward=_AXIS_FORWARD, axis_up='Y', global_scale=1.0, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
        mesh_smooth_type='FACE', use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False, use_triangles=True,
        path_mode='STRIP', embed_textures=False)
    return path

_AXIS_FORWARD = 'Z'


def _render_preview(objs, name, lo, hi):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 24
    scene.cycles.use_denoising = False
    scene.render.threads = 4
    scene.render.threads_mode = 'FIXED'
    res = 512
    scene.render.resolution_x = scene.render.resolution_y = res
    scene.view_settings.view_transform = 'Standard'
    world = bpy.data.worlds.new('w'); world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (0.62, 0.66, 0.70, 1)
    world.node_tree.nodes['Background'].inputs[1].default_value = 0.75
    scene.world = world
    sun_d = bpy.data.lights.new('s', 'SUN'); sun_d.energy = 2.6
    sun = bpy.data.objects.new('s', sun_d); bpy.context.collection.objects.link(sun)
    sun.rotation_euler = Euler((math.radians(50), 0, math.radians(35)))
    center = (lo + hi) / 2
    extent = hi - lo
    span = max(extent.length, 0.2)
    # Ground plane + 1m-scale reference post (grey/white, 0.5m stripes).
    bpy.ops.mesh.primitive_plane_add(size=span * 12, location=(center.x, center.y, min(lo.z, 0) - 0.001))
    gr = bpy.context.object
    gm = bpy.data.materials.new('g'); gm.use_nodes = True
    gm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.25, 0.23, 0.2, 1)
    gr.data.materials.append(gm)
    views = [('front 3/4', (-1, -1.25, 0.65)), ('back 3/4', (1, 1.25, 0.65)), ('side', (1, -0.02, 0.15)), ('top', (0.02, -0.25, 1))]
    cam_d = bpy.data.cameras.new('c'); cam_d.type = 'ORTHO'; cam_d.ortho_scale = span * 1.18; cam_d.clip_end = span * 40
    cam = bpy.data.objects.new('c', cam_d); bpy.context.collection.objects.link(cam)
    scene.camera = cam
    sheet = np.zeros((res * 2, res * 2, 4), dtype=np.float32)
    tmp = os.path.join(PREVIEW_DIR, f'_{name}_tmp.png')
    for i, (_, d) in enumerate(views):
        v = Vector(d).normalized()
        cam.location = center + v * span * 8
        cam.rotation_euler = (-v).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = tmp
        bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(tmp)
        px = np.array(im.pixels[:], dtype=np.float32).reshape(res, res, im.channels)
        bpy.data.images.remove(im)
        row, colm = i // 2, i % 2
        y0 = (1 - row) * res  # image buffer is bottom-up
        sheet[y0:y0 + res, colm * res:colm * res + res, :px.shape[2]] = px
    sheet[..., 3] = 1
    out = bpy.data.images.new('sheet', res * 2, res * 2, alpha=True)
    out.colorspace_settings.name = 'Non-Color'
    out.pixels.foreach_set(sheet.ravel())
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    out.filepath_raw = os.path.join(PREVIEW_DIR, name + '.png'); out.file_format = 'PNG'; out.save()
    os.remove(tmp)
    return out.filepath_raw


def finish(model, bounds=None, preview=True):
    """Export + validate + preview. bounds = {'x':(min,max),'y':(min,max),'z':(min,max)} hard limits from the spec.
    Prints a line 'ASSET_OK <name> ...' or raises SystemExit on violation."""
    ensure_atlas()
    objs = _build_objects(model)
    bpy.context.view_layer.update()
    lo, hi = _world_bounds(objs)
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs)
    problems = []
    if bounds:
        for i, ax in enumerate('xyz'):
            if ax in bounds:
                mn, mx = bounds[ax]
                if lo[i] < mn - 1e-4 or hi[i] > mx + 1e-4:
                    problems.append(f"{ax}: model {lo[i]:.3f}..{hi[i]:.3f} outside allowed {mn:.3f}..{mx:.3f}")
    path = _export_fbx(objs, model.name)
    pv = _render_preview(objs, model.name, lo, hi) if preview else None
    print(f"ASSET_INFO {model.name} tris={tris} parts={[o.name for o in objs]} x={lo.x:.3f}..{hi.x:.3f} y={lo.y:.3f}..{hi.y:.3f} z={lo.z:.3f}..{hi.z:.3f} fbx={path} preview={pv}")
    if problems:
        print(f"ASSET_FAIL {model.name}: " + '; '.join(problems))
        sys.stdout.flush()
        os._exit(1)
    print(f"ASSET_OK {model.name}")
    sys.stdout.flush()
