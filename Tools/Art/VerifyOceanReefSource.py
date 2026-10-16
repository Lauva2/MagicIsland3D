"""Run in a separate background Blender opened with OceanReef.blend."""
from pathlib import Path
import json
import bpy
ROOT=Path(__file__).resolve().parents[2]
data=json.loads((ROOT/'Assets/Art/Meshes/Ocean/Reef.meshdata.json').read_text())
assert bpy.context.scene.name=='MagicIsland - Reef Models' and len(bpy.data.scenes)==1
objects=[bpy.context.scene.objects[p['name']] for p in data['parts']]
assert len(objects)==8 and len({o.data.materials[0] for o in objects})==1
def bounds(obj):
    points=[obj.matrix_world@v.co for v in obj.data.vertices]
    return [min(p[i] for p in points) for i in range(3)]+[max(p[i] for p in points) for i in range(3)]
before={o.name:bounds(o) for o in objects}
for obj,p in zip(objects,data['parts']):
    mesh=obj.data;mesh.calc_loop_triangles();count=0
    for tri in mesh.loop_triangles:
        a,b,c=[mesh.vertices[i].co for i in tri.vertices]
        if (b-a).cross(c-a).length_squared>=1e-18:count+=1
    assert count==len(p['triangles'])//3,obj.name
    colors=mesh.color_attributes['OceanColor'];alpha=[c.color_srgb[3] for c in colors.data]
    if obj.name=='Seagrass Blades':assert min(alpha)<.001 and max(alpha)>.999
    else:assert max(alpha)==0
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'SourceArt/OceanReef/OceanReef.fbx'),use_anim=False)
bpy.context.view_layer.update();after=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(after)==8
for obj in after:
    assert max(abs(a-b) for a,b in zip(before[obj.name],bounds(obj)))<1e-4,obj.name
    assert len(obj.data.color_attributes)>0,obj.name
    if obj.name=='Seagrass Blades':
        alpha=[c.color_srgb[3] for c in obj.data.color_attributes[0].data];assert min(alpha)<.001 and max(alpha)>.999
result={'result':'PASS','parts':8,'triangles':sum(len(p['triangles'])//3 for p in data['parts']),
        'standalone_source_and_vertex_colors':True,'fbx_world_bounds_and_grass_weights':True,'blender_version':bpy.app.version_string}
p=ROOT/'Logs/ReefArt/SourceVerification.json';p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
