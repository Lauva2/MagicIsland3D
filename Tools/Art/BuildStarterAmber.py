"""Run in Blender (including MCP). Rebuild only the dedicated amber source scene.

Exports a deterministic, flat-shaded vertex-colour mesh in Unity coordinates.
Unity's explicit island rebuild imports this data into the existing native mesh GUID.
"""
from pathlib import Path
import bpy
import json
import math
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'SourceArt/WorldPlanning/Models'
OUT.mkdir(parents=True, exist_ok=True)
SCENE = 'Starter Amber Studio'
scene = bpy.data.scenes.get(SCENE)
if scene is None:
    scene = bpy.data.scenes.new(SCENE)
else:
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
bpy.context.window.scene = scene
objects = []
palette = {}


def material(name, color):
    mat = bpy.data.materials.get('StarterAmber.' + name)
    if mat is None:
        mat = bpy.data.materials.new('StarterAmber.' + name)
    mat.use_nodes = True
    node = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    node.inputs['Base Color'].default_value = (*color, 1)
    node.inputs['Roughness'].default_value = .34
    mat.diffuse_color = (*color, 1)
    palette[mat.name] = (*color, 1)
    return mat


stone = material('Warm stone', (.46, .38, .30))
rim = material('Stone bevel', (.66, .55, .39))
gold = material('Amber', (1, .57, .095))
honey = material('Honey facets', (1, .77, .25))
deep = material('Dark amber facets', (.76, .31, .055))
rune = material('Rune inlay', (1, .89, .48))


def mesh(name, verts, faces, mats, indices=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    for mat in mats:
        data.materials.append(mat)
    if indices:
        for poly, index in zip(data.polygons, indices):
            poly.material_index = index
    objects.append(obj)
    return obj


def ring(name, levels, n, mats):
    verts = [(r*math.cos(i*math.tau/n), r*math.sin(i*math.tau/n), z)
             for r, z in levels for i in range(n)]
    faces = [tuple(reversed(range(n))), tuple((len(levels)-1)*n+i for i in range(n))]
    indices = [0, 1 % len(mats)]
    for j in range(len(levels)-1):
        for i in range(n):
            a = j*n+i
            b = j*n+(i+1) % n
            faces.append((a, b, b+n, a+n))
            indices.append(j % len(mats))
    return mesh(name, verts, faces, mats, indices)


ring('Weathered lower step', [(10.8, -.25), (10.8, .15), (10.3, .48), (10.3, .65)], 16, [stone, rim])
ring('Upper carved dais', [(9.5, .65), (9.5, 1.1), (9.1, 1.42)], 16, [stone, rim])


def crystal(name, pos, radius, height, tilt, angle):
    n = 7
    levels = [(radius*.64, 0), (radius, height*.15), (radius*.91, height*.68), (radius*.50, height*.88)]
    verts = []
    for r, z in levels:
        for i in range(n):
            a = angle+i*math.tau/n
            verts.append((r*math.cos(a)+tilt[0]*z/height, r*math.sin(a)+tilt[1]*z/height, z))
    verts.append((tilt[0]*1.08, tilt[1]*1.08, height))
    faces = [tuple(reversed(range(n)))]
    indices = [2]
    for j in range(3):
        for i in range(n):
            a = j*n+i
            b = j*n+(i+1) % n
            faces.append((a, b, b+n, a+n))
            indices.append((i+j//2) % 3)
    for i in range(n):
        faces.append((3*n+i, 3*n+(i+1) % n, len(verts)-1))
        indices.append(i % 2)
    obj = mesh(name, verts, faces, [gold, honey, deep], indices)
    obj.location = pos
    return obj


crystal('Giant amber heart', (-.9, .5, 1.4), 3.9, 18.5, (1.7, -.8), .2)
for i, (r, h) in enumerate([(7.1, 7.2), (7.8, 4.1), (6.4, 5.7), (7.7, 3.2), (7.3, 6.4), (6.8, 4.4)]):
    a = i*math.tau/6+.23
    crystal('Satellite crystal %02d' % i, (r*math.cos(a), r*math.sin(a), 1.4),
            1.0+(i % 2)*.3, h, (math.cos(a)*.8, math.sin(a)*.8), a)


def bar(name, a, b, width=.09):
    a, b = Vector(a), Vector(b)
    side = Vector((-(b-a).y, (b-a).x, 0)).normalized()*width
    mesh(name, [a-side, b-side, b+side, a+side], [(0, 1, 2, 3)], [rune])


for i in range(12):
    a = i*math.tau/12
    radial = Vector((math.cos(a), math.sin(a), 0))
    tangent = Vector((-math.sin(a), math.cos(a), 0))
    center = radial*9.85+Vector((0, 0, .69))
    # Angular glyphs and inset ring segments are real geometry, not point lights.
    points = [center+radial*.35, center+tangent*.26,
              center-radial*.35, center-tangent*.26, center+radial*.35]
    for j in range(4):
        bar('Rune %02d stroke %d' % (i, j), points[j], points[j+1], .055)
    for j in range(4):
        t = a+.10+j*.08
        u = t+.07
        bar('Inlay arc', (8.6*math.cos(t), 8.6*math.sin(t), 1.44),
            (8.6*math.cos(u), 8.6*math.sin(u), 1.44), .045)

# Export split triangle vertices: stable hard edges and material colours without dependencies.
positions, colors, triangles = [], [], []
depsgraph = bpy.context.evaluated_depsgraph_get()
for obj in objects:
    evaluated = obj.evaluated_get(depsgraph)
    data = evaluated.to_mesh()
    data.calc_loop_triangles()
    for triangle in data.loop_triangles:
        color = palette[data.materials[triangle.material_index].name]
        first = len(positions)//3
        for index in triangle.vertices:
            p = obj.matrix_world @ data.vertices[index].co
            positions.extend(round(v, 6) for v in (p.x, p.z, p.y))
            colors.extend(color)
        triangles.extend((first, first+2, first+1))
    evaluated.to_mesh_clear()
result = {'version': 1, 'positions': positions, 'colors': colors, 'triangles': triangles}
(OUT / 'StarterAmber.meshdata.json').write_text(json.dumps(result, separators=(',', ':'))+'\n', encoding='utf-8')
for obj in objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = objects[2]
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'StarterAmber.blend'))
print('Amber source exported:', len(triangles)//3, 'triangles;', len(objects), 'source objects; single Unity mesh.')
