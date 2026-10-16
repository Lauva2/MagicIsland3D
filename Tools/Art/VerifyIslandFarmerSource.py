"""Open the saved .blend in a separate background Blender and round-trip its FBX.

blender --background --factory-startup SourceArt/Characters/IslandFarmer.blend \
  --python-exit-code 1 --python Tools/Art/VerifyIslandFarmerSource.py

This script replaces the verification process's scene, never the live MCP scene.
"""
from pathlib import Path
import json
import bpy

ROOT = Path(__file__).resolve().parents[2]
scene = bpy.context.scene
assert scene.name == 'MagicIsland - Island Farmer'
parts = [obj for obj in scene.objects if obj.type == 'MESH' and obj.name.startswith('IslandFarmer')]
assert len(parts) == 6
assert len({obj.data.materials[0] for obj in parts}) == 1
assert len(json.loads(scene['unity_animation_preview_ranges'])) == 9
scene.frame_set(1)

def bounds(objects):
    points = [obj.matrix_world @ vertex.co for obj in objects for vertex in obj.data.vertices]
    return [min(p[i] for p in points) for i in range(3)] + [max(p[i] for p in points) for i in range(3)]

before = bounds(parts)
socket = next(obj for obj in scene.objects if obj.name == 'ToolSocket')
socket_before = socket.matrix_world.translation.copy()
assert max(abs(a-b) for a,b in zip(socket.location,(0,-.1,-.4))) < 1e-6
triangles = 0
for obj in parts:
    obj.data.calc_loop_triangles()
    triangles += len(obj.data.loop_triangles)
assert triangles == 18618
assert all(obj.data.uv_layers.active is not None for obj in parts)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'SourceArt/Characters/IslandFarmer.fbx'),use_anim=False)
bpy.context.view_layer.update()
imported = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
assert len(imported) == 6
after = bounds(imported)
assert max(abs(a-b) for a,b in zip(before,after)) < 1e-4, (before,after)
socket_after = bpy.data.objects['ToolSocket'].matrix_world.translation
error = (socket_before-socket_after).length
assert error < 1e-4
result = {'result':'PASS','triangles':triangles,'mesh_count':len(imported),
          'source_blend_opened':True,'fbx_roundtrip_bounds':after,
          'socket_roundtrip_error':error,'blender_version':bpy.app.version_string}
output = ROOT/'Logs/PlayerArt'
output.mkdir(parents=True,exist_ok=True)
(output/'SourceVerification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result))
