"""Explicit Blender authoring/export for four farm tools; preserves other scenes."""
from pathlib import Path
import json
import math
import subprocess
import bpy
import bmesh
from mathutils import Vector, Matrix

SCENE = 'MagicIsland - Farm Tools'
TOOLS = [('IslandHoe', 'Hoe'), ('IslandWateringCan', 'Water'), ('IslandAxe', 'Axe'), ('IslandPickaxe', 'Pickaxe')]


def support(repo):
    namespace = {'__name__': 'island_farmer_shapes'}
    path = repo/'Tools/Art/BuildIslandFarmer.py'
    exec(compile(path.read_text(encoding='utf-8'), str(path), 'exec'), namespace)
    return namespace


def oriented_lathe(shape, profile, center, direction, color, segments=24):
    rotation = Vector((0, 1, 0)).rotation_difference(Vector(direction).normalized()).to_matrix()
    vertices, faces = [], []
    for radius, height in profile:
        for i in range(segments):
            a = math.tau*i/segments
            vertices.append(rotation @ Vector((radius*math.cos(a), height, radius*math.sin(a))))
    for ring in range(len(profile)-1):
        for i in range(segments):
            a, b = ring*segments+i, ring*segments+(i+1)%segments
            faces.append((a, b, b+segments, a+segments))
    # All profiles used here have nonzero end radii; cap with single polygons.
    faces += [tuple(reversed(range(segments))), tuple((len(profile)-1)*segments+i for i in range(segments))]
    shape.add(vertices, faces, color, center)


def hoe(Shape):
    shape = Shape()
    shape.lathe([(0, -.54), (.026, -.54), (.039, -.52), (.041, -.40), (.036, -.08),
                 (.037, .18), (.031, .255), (0, .255)], 'LeatherLight', segments=20)
    # Subtle longitudinal grain; all details are baked into the same mesh.
    for x, z in [(.018, .033), (-.024, .027), (.033, -.015)]:
        shape.tube([(x, -.48, z), (x*.85, -.28, z*1.05), (x*.9, -.07, z),
                    (x, .17, z*.9)], .0018, 'Leather', sides=5)
    shape.lathe([(.038, -.075), (.041, -.07), (.041, .055), (.038, .06),
                 (.038, -.075)], 'Leather', segments=20)
    for y in [-.063, -.035, -.007, .021, .049]:
        shape.lathe([(.041, y-.002), (.042, y), (.041, y+.002)], 'LeatherLight', segments=20)
    shape.lathe([(.032, .16), (.046, .17), (.047, .24), (.033, .265)], 'Metal', segments=24)
    shape.lathe([(.047, .177), (.049, .18), (.049, .19), (.047, .193)], 'Brass', segments=24)
    shape.box((0, .255, .055), (.092, .095, .125), .022, 'Metal', steps=3)
    shape.box((0, .273, .151), (.46, .067, .235), .022, 'Metal', steps=3)
    shape.box((0, .276, .252), (.435, .048, .037), .014, 'Cream', steps=3)
    for x in [-.09, .09]:
        shape.sphere((x, .31, .10), (.022, .012, .022), 'Brass', segments=12, rings=6)
    return shape


def watering_can(Shape):
    shape = Shape()
    # Hollow, softly bulging enamel body. The hand origin is at the carry handle.
    shape.lathe([(0, -.435), (.105, -.435), (.15, -.418), (.173, -.34),
                 (.172, -.18), (.158, -.12), (.143, -.115), (.136, -.125),
                 (.148, -.18), (.150, -.33), (.128, -.405), (0, -.405)],
                'Shirt', aspect=.86, segments=32)
    shape.lathe([(.146, -.125), (.158, -.126), (.162, -.118), (.158, -.109),
                 (.145, -.108), (.138, -.116), (.146, -.125)], 'Cream', aspect=.86, segments=32)
    shape.lathe([(.147, -.402), (.154, -.396), (.157, -.382), (.155, -.374)],
                'ShirtShadow', aspect=.86, segments=32)
    # Carry the body in front of the knee while keeping the handle at the original hand origin.
    forward = .22
    shape.vertices = [(x, y-forward, z) for x, y, z in shape.vertices]
    shape.tube([(-.16, -.18, forward), (-.19, -.08, .12), (-.18, -.005, .025),
                (-.12, .025, 0), (-.06, .026, 0), (.06, .026, 0),
                (.12, .025, 0), (.18, -.005, .025), (.19, -.08, .12), (.16, -.18, forward)],
               .017, 'Cream', sides=10)
    shape.tube([(-.065, .026, 0), (.065, .026, 0)], .021, 'LeatherLight', sides=12)
    for side in [-1, 1]:
        shape.sphere((side*.165, -.18, forward), (.025, .036, .04), 'Brass', segments=12, rings=8)
    shape.tube([(0, -.30, .125+forward), (0, -.26, .23+forward), (0, -.18, .32+forward), (0, -.12, .405+forward)],
               .032, 'Shirt', sides=16)
    direction = Vector((0, .06, .10)).normalized()
    center = Vector((0, -.108, .425+forward))
    oriented_lathe(shape, [(.035, -.025), (.070, .012), (.078, .034), (.071, .047)],
                   center, direction, 'Brass')
    oriented_lathe(shape, [(.070, .047), (.072, .052), (.068, .057)],
                   center, direction, 'Cream')
    across = Vector((1, 0, 0)); up = direction.cross(across)
    rotation = Vector((0, 1, 0)).rotation_difference(direction).to_matrix()
    for i in range(9):
        a = math.tau*i/8 if i else 0
        offset = .042*(math.cos(a)*across+math.sin(a)*up) if i else Vector((0, 0, 0))
        p = center+direction*.058+offset
        shape.sphere(p, (.009, .003, .009), 'Metal', segments=10, rings=6, rotation=rotation)
    # Small cream maker's leaf rather than text or a decal texture.
    shape.sphere((0, -.275, -.149+forward), (.043, .072, .005), 'Cream', segments=16, rings=8)
    return shape


def beveled_prism(shape, outline, thickness, color, bevel=.008):
    """A rounded forged head, built as one closed mesh before palette assignment."""
    bm = bmesh.new()
    count = len(outline)
    vertices = [bm.verts.new((x, y, z)) for z in [-thickness/2, thickness/2] for x, y in outline]
    bm.faces.new(list(reversed(vertices[:count]))); bm.faces.new(vertices[count:])
    for i in range(count):
        j = (i+1) % count
        bm.faces.new([vertices[i], vertices[j], vertices[j+count], vertices[i+count]])
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=3, affect='EDGES')
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.verts.index_update()
    shape.add([tuple(v.co) for v in bm.verts], [tuple(v.index for v in face.verts) for face in bm.faces], color)
    bm.free()


def gathering_handle(Shape):
    shape = Shape()
    shape.lathe([(0, -.55), (.024, -.55), (.036, -.53), (.040, -.39), (.036, -.07),
                 (.037, .19), (.032, .29), (0, .29)], 'LeatherLight', segments=20)
    for x, z in [(.018, .032), (-.024, .027), (.032, -.014)]:
        shape.tube([(x, -.49, z), (x*.9, -.30, z*1.06), (x*.8, -.08, z), (x, .18, z*.93)],
                   .0018, 'Leather', sides=5)
    shape.lathe([(.038, -.07), (.042, -.065), (.042, .065), (.038, .07), (.038, -.07)],
                'Leather', segments=20)
    for y in [-.055, -.028, 0, .028, .055]:
        shape.lathe([(.042, y-.002), (.043, y), (.042, y+.002)], 'LeatherLight', segments=20)
    shape.lathe([(.038, .195), (.053, .20), (.054, .30), (.037, .31)], 'Metal', segments=24)
    shape.lathe([(.053, .202), (.055, .205), (.055, .215), (.053, .218)], 'Brass', segments=24)
    return shape


def axe(Shape):
    shape = gathering_handle(Shape)
    beveled_prism(shape, [(-.055, .31), (.08, .328), (.275, .39), (.32, .37),
                         (.35, .18), (.30, .15), (.08, .205), (-.055, .21)], .075, 'Metal', .010)
    beveled_prism(shape, [(.292, .38), (.32, .37), (.35, .18), (.32, .163),
                         (.308, .20), (.296, .32)], .079, 'Cream', .005)
    for z in [-.044, .044]:
        shape.sphere((.015, .26, z), (.024, .024, .009), 'Brass', segments=12, rings=6)
    return shape


def pickaxe(Shape):
    shape = gathering_handle(Shape)
    beveled_prism(shape, [(-.36, .13), (-.28, .22), (-.15, .29), (-.055, .32),
                         (.055, .32), (.16, .29), (.30, .20), (.36, .12),
                         (.27, .16), (.13, .22), (.04, .235), (-.04, .235), (-.16, .215), (-.28, .16)],
                  .066, 'Metal', .007)
    beveled_prism(shape, [(.295, .203), (.36, .12), (.27, .16), (.265, .171)], .069, 'Cream', .003)
    beveled_prism(shape, [(-.36, .13), (-.28, .22), (-.266, .225), (-.28, .16)], .069, 'Cream', .003)
    for z in [-.040, .040]:
        shape.sphere((0, .273, z), (.026, .026, .009), 'Brass', segments=12, rings=6)
    return shape


def finish_forged_mesh(obj):
    mesh = obj.data
    mesh.set_sharp_from_angle(angle=math.radians(40))
    # Tiny concave bevel patches need a face normal rather than averaging across the pointed tip.
    for _ in range(8):
        mesh.update(); mesh.calc_loop_triangles(); patches = set()
        for triangle in mesh.loop_triangles:
            a, b, c = [mesh.vertices[i].co for i in triangle.vertices]
            normal = sum((mesh.corner_normals[i].vector for i in triangle.loops), Vector())
            if (b-a).cross(c-a).dot(normal) < -1e-12: patches.add(triangle.polygon_index)
        if not patches: return
        for index in patches: mesh.polygons[index].use_smooth = False
    raise RuntimeError('Forged tool has inconsistent corner normals.')


def extend_gathering_tools(repository, preview_directory):
    """Add only the two missing meshes; preserve the existing hoe, can and palette edits."""
    repo, preview = Path(repository), Path(preview_directory)
    ns = support(repo); scene = bpy.data.scenes.get(SCENE)
    if scene is None or not scene.get('magic_island_farm_tools'):
        raise RuntimeError('Open the existing Farm Tools scene before extending it.')
    bpy.context.window.scene = scene
    base = scene.objects.get('IslandHoe')
    if base is None or scene.objects.get('IslandWateringCan') is None:
        raise RuntimeError('Preserve the existing hoe and watering can meshes.')
    if any(scene.objects.get(name) is not None for name in ['IslandAxe', 'IslandPickaxe']):
        raise RuntimeError('Gathering tools already exist; edit/export them rather than recreating them.')
    collection = base.users_collection[0]; mat = base.data.materials[0]
    for name, joint, location, shape in [('IslandAxe', 'Axe', (-.32, 0, .58), axe(ns['Shape'])),
                                         ('IslandPickaxe', 'Pickaxe', (.38, 0, .58), pickaxe(ns['Shape']))]:
        root = bpy.data.objects.new(joint+' Grip Origin', None); collection.objects.link(root)
        root['unity_path'] = joint; root.location = location
        finish_forged_mesh(shape.make(name, root, collection, mat))
    base.parent.location = (-1.03, 0, .56)
    scene.objects['IslandWateringCan'].parent.location = (1.06, 0, .46)
    scene.camera.location = (1.4, -5.0, 2.6)
    scene.camera.rotation_euler = (Vector((0, 0, .48))-scene.camera.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera.data.ortho_scale = 3.25
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    scene.render.filepath = str(preview/'FarmToolsStudio.png')
    export_current(str(repo), str(preview))


def material(repo, ns):
    image = bpy.data.images.load(str(repo/'Assets/Art/Textures/Player/IslandFarmerPalette.png'), check_existing=False)
    image.name = 'Farm Tools Palette'
    image.pack()
    image.filepath_raw = str(repo/'Assets/Art/Textures/Tools/FarmToolsPalette.png')
    Path(image.filepath_raw).parent.mkdir(parents=True, exist_ok=True)
    image.save(); image.pack()
    mat = bpy.data.materials.new('Farm Tools - Shared Palette')
    mat.use_nodes = True
    shader = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    shader.inputs['Roughness'].default_value = .65
    node = mat.node_tree.nodes.new('ShaderNodeTexImage'); node.image = image
    mat.node_tree.links.new(node.outputs['Color'], shader.inputs['Base Color'])
    return mat


def export_current(repository, preview_directory):
    repo, preview = Path(repository), Path(preview_directory)
    preview.mkdir(parents=True, exist_ok=True)
    ns = support(repo); scene = bpy.data.scenes.get(SCENE)
    if scene is None or not scene.get('magic_island_farm_tools'):
        raise RuntimeError('Open the Farm Tools source scene before export.')
    meshes = [scene.objects.get(name) for name, _ in TOOLS]
    if any(obj is None or obj.type != 'MESH' or obj.modifiers or
           any(abs(obj.matrix_local[i][j]-(1 if i == j else 0)) > 1e-6 for i in range(4) for j in range(4))
           for obj in meshes):
        raise RuntimeError('Keep the four named meshes and apply mesh transforms/modifiers; keep their grip origins.')
    data_path = repo/'Assets/Art/Meshes/Tools/FarmTools.meshdata.json'
    triangles = ns['export_meshes'](meshes, data_path)
    data = json.loads(data_path.read_text()); data['name'] = 'FarmTools'
    data_path.write_text(json.dumps(data, separators=(',', ':')), encoding='utf-8')
    mat = meshes[0].data.materials[0]
    image = next(node.image for node in mat.node_tree.nodes if node.type == 'TEX_IMAGE')
    image.filepath_raw = str(repo/'Assets/Art/Textures/Tools/FarmToolsPalette.png')
    image.save(); image.pack()
    source = repo/'SourceArt/Tools'; source.mkdir(parents=True, exist_ok=True)
    bpy.data.libraries.write(str(source/'FarmTools.blend'), {scene}, fake_user=True, compress=True)
    result = subprocess.run([bpy.app.binary_path, '--background', '--factory-startup', str(source/'FarmTools.blend'),
        '--python-exit-code', '1', '--python-expr', 'import bpy; bpy.context.preferences.filepaths.save_version=0; '
        'bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,compress=True)'], capture_output=True, text=True, timeout=60)
    if result.returncode: raise RuntimeError('Could not package FarmTools.blend: '+result.stderr[-1500:])
    bpy.ops.object.select_all(action='DESELECT')
    for obj in meshes:
        obj.select_set(True); obj.parent.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.export_scene.fbx(filepath=str(source/'FarmTools.fbx'), use_selection=True,
        object_types={'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
        bake_space_transform=False, bake_anim=False, path_mode='STRIP', use_custom_props=True)
    summary = {'triangles': triangles, 'parts': {p['name']:len(p['triangles'])//3 for p in data['parts']}}
    (preview/'model-summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
    print(json.dumps(summary))


def build(repository, preview_directory):
    repo, preview = Path(repository), Path(preview_directory)
    preview.mkdir(parents=True, exist_ok=True)
    ns = support(repo)
    old = bpy.data.scenes.get(SCENE)
    if old is not None:
        if not old.get('magic_island_farm_tools'): raise RuntimeError('Refusing to replace an unrelated scene.')
        for obj in list(old.objects): bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.scenes.remove(old)
    scene = bpy.data.scenes.new(SCENE); scene['magic_island_farm_tools'] = 1
    bpy.context.window.scene = scene
    art = bpy.data.collections.new('Farm Tools - Export'); scene.collection.children.link(art)
    mat = material(repo, ns)
    for name, joint, location, shape in [('IslandHoe', 'Hoe', (-.52, 0, .56), hoe(ns['Shape'])),
                                         ('IslandWateringCan', 'Water', (.40, 0, .46), watering_can(ns['Shape']))]:
        root = bpy.data.objects.new(joint+' Grip Origin', None); art.objects.link(root)
        root['unity_path'] = joint; root.location = location
        shape.make(name, root, art, mat)
    preview_only = bpy.data.collections.new('Farm Tools - Preview Only'); scene.collection.children.link(preview_only)
    camera = ns['stage'](scene, preview_only, preview)
    camera.location = (1.9, -3.8, 2.2)
    camera.rotation_euler = (Vector((0, 0, .48))-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.ortho_scale = 1.9
    scene.render.resolution_x, scene.render.resolution_y = 1200, 900
    scene.render.filepath = str(preview/'FarmToolsStudio.png')
    scene['grip_contract'] = 'Unity local metres; +Y up, +Z tool front; mesh origin is ToolSocket grip'
    extend_gathering_tools(str(repo), str(preview))


if __name__ == '__main__':
    raise SystemExit('Call build(repository, preview_directory) or export_current(...) explicitly through Blender MCP.')
