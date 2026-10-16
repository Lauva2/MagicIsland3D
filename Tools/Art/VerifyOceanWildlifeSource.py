"""Run in a separate background Blender opened with OceanWildlife.blend."""
from pathlib import Path
import json
import bpy
ROOT=Path(__file__).resolve().parents[2]
data=json.loads((ROOT/'Assets/Art/Meshes/Ocean/Wildlife.meshdata.json').read_text())
assert bpy.context.scene.name=='MagicIsland - Ocean Wildlife'
objects=[bpy.context.scene.objects[p['name']] for p in data['parts']]
assert len(objects)==8 and len({o.data.materials[0] for o in objects})==1
assert len(bpy.data.scenes)==1
def bounds(obj):
    points=[obj.matrix_world@v.co for v in obj.data.vertices]
    return [min(p[i] for p in points) for i in range(3)]+[max(p[i] for p in points) for i in range(3)]
before={o.name:bounds(o) for o in objects}
for obj,p in zip(objects,data['parts']):
    mesh=obj.data;mesh.calc_loop_triangles()
    assert all(mesh.attributes.get(name) for name in ['hinge','joint_tag','leg_phase'])
    assert mesh.color_attributes.get('OceanColor') is not None
    assert len(mesh.loop_triangles)==len(p['triangles'])//3
    if obj.name=='Shore Crab':assert {round(v.value) for v in mesh.attributes['joint_tag'].data}=={0,100,101,102,103,110,111}
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'SourceArt/Ocean/OceanWildlife.fbx'),use_anim=False)
bpy.context.view_layer.update()
after=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(after)==8
for obj in after:
    assert max(abs(a-b) for a,b in zip(before[obj.name],bounds(obj)))<1e-4,obj.name
    assert len(obj.data.color_attributes)>0,obj.name
result={'result':'PASS','parts':8,'triangles':sum(len(p['triangles'])//3 for p in data['parts']),
        'standalone_source_and_joint_attributes':True,'fbx_world_bounds_and_vertex_colors':True,'blender_version':bpy.app.version_string}
path=ROOT/'Logs/OceanModels/SourceVerification.json';path.parent.mkdir(parents=True,exist_ok=True)
path.write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
