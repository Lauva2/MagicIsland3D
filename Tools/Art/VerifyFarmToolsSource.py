"""Run only in a separate background Blender opened with FarmTools.blend."""
from pathlib import Path
import json
import bpy

ROOT = Path(__file__).resolve().parents[2]
assert bpy.context.scene.name == 'MagicIsland - Farm Tools'
parts = [o for o in bpy.context.scene.objects if o.name in ['IslandHoe', 'IslandWateringCan', 'IslandAxe', 'IslandPickaxe']]
assert len(parts) == 4 and all(o.type == 'MESH' for o in parts)
assert len({o.data.materials[0] for o in parts}) == 1
image = next(n.image for n in parts[0].data.materials[0].node_tree.nodes if n.type == 'TEX_IMAGE')
assert image.packed_file is not None


def bounds(obj):
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    return [min(p[i] for p in points) for i in range(3)] + [max(p[i] for p in points) for i in range(3)]


before = {o.name: bounds(o) for o in parts}
origins = {o.parent.name: tuple(o.parent.matrix_world.translation) for o in parts}
triangles = 0
for obj in parts:
    obj.data.calc_loop_triangles(); triangles += len(obj.data.loop_triangles)
assert triangles == 9396
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT/'SourceArt/Tools/FarmTools.fbx'), use_anim=False)
bpy.context.view_layer.update()
after = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(after) == 4
for obj in after: assert max(abs(a-b) for a, b in zip(before[obj.name], bounds(obj))) < 1e-4
for name, position in origins.items():
    assert max(abs(a-b) for a, b in zip(position, bpy.data.objects[name].matrix_world.translation)) < 1e-4
result = {'result': 'PASS', 'triangles': triangles, 'tools': 4, 'packed_palette': True,
          'fbx_bounds_and_grip_origins_preserved': True, 'blender_version': bpy.app.version_string}
path = ROOT/'Logs/FarmTools/SourceVerification.json'; path.parent.mkdir(parents=True, exist_ok=True)
path.write_text(json.dumps(result, indent=2), encoding='utf-8'); print(json.dumps(result))
