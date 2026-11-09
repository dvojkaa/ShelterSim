"""Run with Blender, -- <asset scripts...>; validate source geometry without exporting or rendering.
Writes M22_MODEL_REPORT.json and checks clear openings. Does not mutate shared art library or assets.
"""
import json, math, os, runpy, sys
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / 'ArtSource' / 'lib'))
sys.path.insert(0, str(ROOT / 'ArtSource' / 'assets'))
import ss_art
records = []


def capture(model, bounds=None, preview=True):
    objs = ss_art._build_objects(model)
    bpy.context.view_layer.update()
    lo, hi = ss_art._world_bounds(objs)
    failures = []
    if bounds:
        for i, axis in enumerate('xyz'):
            if axis in bounds and (lo[i] < bounds[axis][0] - .00011 or hi[i] > bounds[axis][1] + .00011):
                failures.append('outside ' + axis + ' bounds')
    for obj in objs:
        for vertex in obj.data.vertices:
            if not all(math.isfinite(value) for value in vertex.co): failures.append('nonfinite vertex')
        if not obj.data.uv_layers: failures.append('missing atlas UV')
    hole = {'piece_wall_window': (-.55,.55,1.0,2.1), 'piece_door_frame':(-.8,.8,.02,2.2)}.get(model.name)
    if hole:
        vertices, faces = [], []
        for obj in objs:
            base = len(vertices)
            vertices.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
            faces.extend(tuple(base + i for i in face.vertices) for face in obj.data.polygons)
        tree = BVHTree.FromPolygons(vertices, faces)
        x0,x1,z0,z1 = hole
        blocked = 0
        for xi in range(17):
            for zi in range(23):
                x = x0 + .005 + (x1-x0-.01) * xi/16
                z = z0 + .005 + (z1-z0-.01) * zi/22
                if tree.ray_cast(Vector((x,-1,z)), Vector((0,1,0)), 2)[0] is not None: blocked += 1
        if blocked: failures.append(f'opening blocked on {blocked}/391 sampled rays')
    record = dict(id=model.name, tris=sum(len(face.vertices)-2 for obj in objs for face in obj.data.polygons),
                  bounds={axis:[round(lo[i],5),round(hi[i],5)] for i,axis in enumerate('xyz')},
                  parts=[dict(name=obj.name,pivot=[round(v,5) for v in obj.location]) for obj in objs],
                  failures=failures)
    blend_dir=ROOT/'ArtSource'/'blend'
    blend_dir.mkdir(exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.context.preferences.filepaths.file_preview_type='NONE'
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_dir/(model.name+'.blend')), relative_remap=True)
    records.append(record)
    print('M22_VALIDATION',json.dumps(record),flush=True)


ss_art.finish = capture
for path in sys.argv[sys.argv.index('--')+1:]:
    runpy.run_path(str(ROOT / path), run_name='__main__')
output=ROOT/'ArtSource'/'M22_MODEL_REPORT.json'
output.write_text(json.dumps(records,indent=2)+'\n')
sys.stdout.flush()
sys.stderr.flush()
os._exit(1 if any(record['failures'] for record in records) else 0)
