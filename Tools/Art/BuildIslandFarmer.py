"""Bake the first Island Farmer art revision inside Blender (no runtime geometry).

Run explicitly through Blender MCP, or Blender's Python console:
  exec(compile(open('.../Tools/Art/BuildIslandFarmer.py', encoding='utf-8').read(),
               'BuildIslandFarmer.py', 'exec'))
  build('E:/UnityProject/MagicIsland3D', '.../preview-directory')

Only the named art scene is rebuilt. The original scene and the nine Unity
animation clips are not edited. Outputs are Unity-local mesh data and editable
Blender/FBX sources; PlayerCharacterArtSetup imports them explicitly in Unity.
"""
from pathlib import Path
import json
import math
import re
import subprocess
import bpy
import bmesh
from mathutils import Vector, Matrix

ART_SCENE = 'MagicIsland - Island Farmer'
PALETTE = [
    ('Skin', 'E9B891'), ('SkinLight', 'F2C9A7'), ('Cheek', 'D88E7C'),
    ('Hair', '533D30'), ('HairLight', '70503A'), ('Eyes', '2D302B'),
    ('Cream', 'F3E7CB'), ('Shirt', '7CACB8'), ('ShirtShadow', '608B9B'),
    ('Overalls', '667C5A'), ('OverallShadow', '4B634B'), ('OverallLight', '81916A'),
    ('Straw', 'D9B56B'), ('StrawLight', 'E8CA87'), ('StrawShadow', 'BA8E4D'),
    ('Leather', '8E6043'), ('LeatherLight', 'AF7A50'), ('Sole', '514236'),
    ('Brass', 'D2AA59'), ('White', 'FFFAE9'),
    ('Metal', '59665F'),
]
COLOR = {name: i for i, (name, _) in enumerate(PALETTE)}
JOINTS = {'Body': (0, 0, 0), 'Head': (0, 1.35, 0),
          'LeftArm': (-.29, 1.03, 0), 'RightArm': (.29, 1.03, 0),
          'LeftLeg': (-.15, .53, 0), 'RightLeg': (.15, .53, 0)}

def blender_point(v):
    return (v[0], -v[2], v[1])

def unity_point(v):
    return (v[0], v[2], -v[1])

def linear(value):
    return value / 12.92 if value < .04045 else ((value + .055) / 1.055) ** 2.4

def rgb(code):
    return tuple(int(code[i:i+2], 16) / 255 for i in (0, 2, 4))

class Shape:
    def __init__(self):
        self.vertices, self.faces, self.colors = [], [], []

    def add(self, vertices, faces, color, center=(0, 0, 0), rotation=None):
        offset = len(self.vertices)
        transform = rotation if rotation is not None else Matrix.Identity(3)
        for vertex in vertices:
            p = transform @ Vector(vertex) + Vector(center)
            self.vertices.append(blender_point(p))
        for face in faces:
            self.faces.append(tuple(offset + index for index in face))
            self.colors.append(COLOR[color])

    def sphere(self, center, size, color, segments=20, rings=12, rotation=None):
        vertices = [(0, -size[1]/2, 0)]
        for ring in range(1, rings):
            latitude = -math.pi/2 + math.pi * ring / rings
            for segment in range(segments):
                angle = math.tau * segment / segments
                vertices.append((size[0]/2 * math.cos(latitude) * math.cos(angle),
                                 size[1]/2 * math.sin(latitude),
                                 size[2]/2 * math.cos(latitude) * math.sin(angle)))
        top = len(vertices)
        vertices.append((0, size[1]/2, 0))
        faces = []
        for i in range(segments):
            j = (i+1) % segments
            faces.append((0, 1+j, 1+i))
            for r in range(rings-2):
                a, b = 1+r*segments, 1+(r+1)*segments
                faces.append((a+i, a+j, b+j, b+i))
            a = 1+(rings-2)*segments
            faces.append((a+i, a+j, top))
        self.add(vertices, faces, color, center, rotation)

    def box(self, center, size, radius, color, rotation=None, steps=4):
        half = [v/2 for v in size]
        radius = min(radius, min(half) * .95)
        vertices, faces, cache = [], [], {}
        for axis in range(3):
            u, v = (axis+1) % 3, (axis+2) % 3
            for sign in (-1, 1):
                grid = []
                for iy in range(steps+1):
                    row = []
                    for ix in range(steps+1):
                        p = [0., 0., 0.]
                        p[axis] = sign * half[axis]
                        p[u], p[v] = half[u]*(2*ix/steps-1), half[v]*(2*iy/steps-1)
                        inner = [max(-half[k]+radius, min(half[k]-radius, p[k])) for k in range(3)]
                        normal = (Vector(p)-Vector(inner)).normalized()
                        point = tuple(Vector(inner) + normal*radius)
                        key = tuple(round(value, 8) for value in point)
                        if key not in cache:
                            cache[key] = len(vertices)
                            vertices.append(point)
                        row.append(cache[key])
                    grid.append(row)
                for y in range(steps):
                    for x in range(steps):
                        faces.append((grid[y][x], grid[y][x+1], grid[y+1][x+1], grid[y+1][x]))
        self.add(vertices, faces, color, center, rotation)

    def lathe(self, profile, color, center=(0, 0, 0), aspect=1., segments=40, wave=0.):
        vertices, rows, faces = [], [], []
        for radius, height in profile:
            if radius < 1e-7:
                rows.append([len(vertices)])
                vertices.append((0, height, 0))
            else:
                row = []
                for i in range(segments):
                    theta = math.tau * i / segments
                    row.append(len(vertices))
                    vertices.append((radius*math.cos(theta), height+wave*math.sin(theta)*radius/.38,
                                     radius*aspect*math.sin(theta)))
                rows.append(row)
        for a, b in zip(rows, rows[1:]):
            for i in range(segments):
                j = (i+1) % segments
                if len(a) == 1:
                    faces.append((a[0], b[j], b[i]))
                elif len(b) == 1:
                    faces.append((a[i], a[j], b[0]))
                else:
                    faces.append((a[i], a[j], b[j], b[i]))
        self.add(vertices, faces, color, center)

    def tube(self, points, radius, color, sides=6):
        vertices, faces = [], []
        for i, p in enumerate(points):
            tangent = Vector(points[min(i+1, len(points)-1)]) - Vector(points[max(i-1, 0)])
            tangent.normalize()
            normal = tangent.cross(Vector((0, 0, 1)))
            if normal.length < .01:
                normal = tangent.cross(Vector((0, 1, 0)))
            normal.normalize()
            second = tangent.cross(normal).normalized()
            for j in range(sides):
                theta = math.tau*j/sides
                vertices.append(Vector(p)+radius*(normal*math.cos(theta)+second*math.sin(theta)))
        for i in range(len(points)-1):
            for j in range(sides):
                k = (j+1) % sides
                faces.append((i*sides+j, i*sides+k, (i+1)*sides+k, (i+1)*sides+j))
        faces.extend([tuple(reversed(range(sides))), tuple((len(points)-1)*sides+j for j in range(sides))])
        self.add(vertices, faces, color)

    def make(self, name, parent, collection, material):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.update()
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(mesh)
        bm.free()
        uv = mesh.uv_layers.new(name='Palette')
        for polygon, index in zip(mesh.polygons, self.colors):
            polygon.use_smooth = True
            for loop in polygon.loop_indices:
                uv.data[loop].uv = ((index % 8 + .5)/8, (index // 8 + .5)/4)
        mesh.materials.append(material)
        obj = bpy.data.objects.new(name, mesh)
        collection.objects.link(obj)
        obj.parent = parent
        return obj

def palette_material(repo):
    folder = repo/'Assets/Art/Textures/Player'
    folder.mkdir(parents=True, exist_ok=True)
    image = bpy.data.images.new('Island Farmer Palette', width=128, height=64, alpha=False)
    pixels = []
    for y in range(64):
        for x in range(128):
            index = (y//16)*8+x//16
            color = rgb(PALETTE[index][1]) if index < len(PALETTE) else rgb('F3E7CB')
            pixels.extend((*color, 1))
    image.pixels.foreach_set(pixels)
    image.filepath_raw = str(folder/'IslandFarmerPalette.png')
    image.file_format = 'PNG'
    image.save()
    material = bpy.data.materials.new('Island Farmer - Palette')
    material.use_nodes = True
    shader = next(node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
    shader.inputs['Roughness'].default_value = .78
    shader.inputs['Specular IOR Level'].default_value = .2
    texture = material.node_tree.nodes.new('ShaderNodeTexImage')
    texture.image = image
    texture.interpolation = 'Closest'
    material.node_tree.links.new(texture.outputs['Color'], shader.inputs['Base Color'])
    return material, image

def make_body():
    s = Shape()
    s.box((0, .83, 0), (.48, .54, .34), .10, 'Shirt')
    s.box((0, .59, -.005), (.39, .20, .30), .075, 'Overalls')
    s.box((0, .755, .153), (.345, .28, .055), .055, 'Overalls')
    s.box((0, .72, -.157), (.35, .30, .04), .05, 'Overalls')
    s.box((0, .73, .189), (.16, .12, .026), .024, 'OverallLight')
    s.box((0, .783, .204), (.15, .014, .012), .005, 'Cream', steps=4)
    for side in (-1, 1):
        rot = Matrix.Rotation(math.radians(side*6), 3, 'Z')
        s.box((side*.145, .93, .163), (.045, .31, .035), .015, 'Overalls', rot)
        s.box((side*.145, .90, -.162), (.044, .35, .026), .012, 'Overalls', rot)
        s.sphere((side*.143, .864, .192), (.033, .033, .015), 'Brass', 12, 8)
        s.box((side*.093, .69, -.187), (.12, .105, .018), .024, 'OverallLight')
        s.sphere((side*.062, 1.083, .12), (.15, .052, .11), 'Cream', 16, 8,
                 Matrix.Rotation(math.radians(-side*22), 3, 'Z'))
    s.sphere((0, 1.135, .005), (.17, .16, .17), 'Skin', 20, 10)
    s.sphere((0, 1.065, .153), (.05, .048, .04), 'Cream', 12, 8)
    return s

def make_head():
    s = Shape()
    s.sphere((0, -.025, 0), (.535, .50, .47), 'SkinLight', 32, 20)
    for side in (-1, 1):
        s.sphere((side*.263, -.045, -.008), (.085, .128, .085), 'Skin', 16, 10)
        s.sphere((side*.289, -.044, .014), (.025, .065, .04), 'Cheek', 12, 8)
    # A closed shell whose lower edge rises over the face and dips at the nape.
    vertices, faces, segments, rings = [], [], 32, 10
    for r in range(rings):
        for i in range(segments):
            theta = math.tau*i/segments
            front = (math.sin(theta)+1)/2
            lower = -.48 + .91*front**2
            latitude = lower + (math.pi/2-lower)*r/rings
            vertices.append((.283*math.cos(latitude)*math.cos(theta),
                             -.014+.266*math.sin(latitude), .254*math.cos(latitude)*math.sin(theta)-.012))
    top = len(vertices)
    vertices.append((0, .246, -.012))
    for r in range(rings-1):
        for i in range(segments):
            j = (i+1) % segments
            faces.append((r*segments+i, r*segments+j, (r+1)*segments+j, (r+1)*segments+i))
    for i in range(segments):
        faces.append(((rings-1)*segments+i, (rings-1)*segments+(i+1)%segments, top))
    faces.append(tuple(reversed(range(segments))))
    s.add(vertices, faces, 'Hair')
    for x, y, z, tilt in [(-.16, .095, .185, -25), (-.075, .105, .222, -22),
                          (.018, .102, .225, -28), (.12, .092, .198, -32)]:
        s.sphere((x, y, z), (.115, .13, .05), 'HairLight' if x < -.1 else 'Hair', 16, 10,
                 Matrix.Rotation(math.radians(tilt), 3, 'Z'))
    for side in (-1, 1):
        s.sphere((side*.092, -.02, .219), (.038, .050, .021), 'Eyes', 16, 10)
        s.sphere((side*.092-.005, -.009, .230), (.010, .013, .006), 'White', 10, 6)
        s.tube([(side*.126, .025, .211), (side*.099, .034, .219), (side*.073, .031, .224)], .005, 'Hair', 5)
        s.sphere((side*.15, -.078, .184), (.064, .027, .013), 'Cheek', 16, 8,
                 Matrix.Rotation(math.radians(side*30), 3, 'Y'))
    s.sphere((0, -.065, .232), (.058, .051, .044), 'Skin', 20, 10)
    s.tube([(-.039, -.121, .205), (-.02, -.13, .210), (0, -.133, .212),
            (.02, -.13, .210), (.039, -.121, .205)], .004, 'Hair', 6)
    # The curved brim is a closed ring, rather than a flat cylinder.
    brim = [(.225,.180),(.29,.169),(.36,.155),(.383,.159),(.39,.171),(.379,.181),(.35,.183),(.29,.193),(.225,.200),(.225,.180)]
    s.lathe(brim, 'StrawLight', aspect=.86, segments=48, wave=.008)
    crown = [(.225,.18),(.237,.21),(.235,.265),(.219,.314),(.19,.344),(.13,.355),(0,.355)]
    s.lathe(crown, 'Straw', aspect=.88, segments=40)
    band = [(.238,.207),(.24,.212),(.238,.25),(.235,.255),(.232,.25),(.233,.21),(.238,.207)]
    s.lathe(band, 'OverallShadow', aspect=.88, segments=40)
    for radius, height in ((.32,.19),(.365,.181)):
        s.lathe([(radius-.002,height),(radius,height+.002),(radius+.002,height),(radius,height-.002),(radius-.002,height)],
                'StrawShadow', aspect=.86, segments=48, wave=.008)
    s.box((-.173, .232, .15), (.082, .055, .023), .012, 'Overalls', Matrix.Rotation(math.radians(-38),3,'Y'), steps=4)
    s.sphere((-.173, .24, .173), (.031, .054, .012), 'Cream', 12, 8, Matrix.Rotation(math.radians(-32),3,'Z'))
    return s

def make_arm(side):
    s = Shape()
    s.sphere((0, -.10, 0), (.215, .27, .218), 'Shirt', 20, 12)
    s.lathe([(.092,-.187),(.104,-.194),(.102,-.228),(.09,-.235),(.083,-.226),(.084,-.197),(.092,-.187)],
            'Cream', segments=24)
    s.sphere((0, -.279, .012), (.124, .205, .128), 'Skin', 20, 12)
    s.sphere((0, -.391, .025), (.145, .17, .15), 'SkinLight', 20, 12)
    s.sphere((-side*.059, -.372, .061), (.064, .092, .071), 'Skin', 16, 10)
    return s

def make_leg(side):
    s = Shape()
    s.box((0, -.185, -.012), (.187, .39, .211), .06, 'Overalls')
    s.box((0, -.33, -.003), (.195, .058, .22), .018, 'OverallLight')
    s.box((0, -.388, .057), (.213, .185, .324), .057, 'Leather')
    s.box((0, -.463, .057), (.222, .041, .336), .017, 'Sole')
    s.box((0, -.445, .060), (.218, .025, .330), .011, 'LeatherLight')
    s.box((0, -.322, .057), (.155, .035, .20), .015, 'LeatherLight')
    s.box((0, -.322, .143), (.137, .024, .045), .009, 'Cream', steps=4)
    return s

def export_meshes(objects, path):
    payload = {'version': 1, 'name': 'IslandFarmer', 'coordinateSpace': 'Unity local metres', 'parts': []}
    for obj in objects:
        mesh = obj.data
        mesh.calc_loop_triangles()
        vertices, normals, uvs, triangles, indices = [], [], [], [], {}
        for triangle in mesh.loop_triangles:
            for loop_index in triangle.loops:
                loop = mesh.loops[loop_index]
                normal = mesh.corner_normals[loop_index].vector
                uv = mesh.uv_layers.active.data[loop_index].uv
                key = (loop.vertex_index, *(round(v, 7) for v in normal), *uv)
                if key not in indices:
                    indices[key] = len(vertices)
                    vertices.append([round(v, 7) for v in unity_point(mesh.vertices[loop.vertex_index].co)])
                    normals.append([round(v, 7) for v in unity_point(normal)])
                    uvs.append([round(v, 7) for v in uv])
                triangles.append(indices[key])
        payload['parts'].append({'name': obj.name, 'joint': obj.parent['unity_path'],
                                 'positions': [v for row in vertices for v in row],
                                 'normals': [v for row in normals for v in row],
                                 'uv': [v for row in uvs for v in row], 'triangles': triangles})
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, separators=(',', ':')), encoding='utf-8')
    return sum(len(part['triangles'])//3 for part in payload['parts'])

def preview_animations(scene, repo, bob):
    """Sample the existing Unity Euler/position curves, only for art inspection."""
    basis = Matrix(((1,0,0),(0,0,-1),(0,1,0)))
    names = ['Idle','Walk','Hoe','Plant','Water','Axe','Pickaxe','Harvest','Fertilize']
    joints = {obj['unity_path']:obj for obj in [bob, *bob.children] if 'unity_path' in obj}
    ranges = {}
    first = 1
    vector_pattern = r'\{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}'
    def curves(text, heading):
        section = re.search(r'^  '+heading+r':\n(.*?)(?=^  m_\w+:)',text,re.M|re.S)
        result = {}
        if not section:
            return result
        for block in re.split(r'^  - curve:\n',section.group(1),flags=re.M)[1:]:
            path = re.search(r'^    path: (.+)$',block,re.M).group(1)
            keys = []
            for key in re.split(r'^      - serializedVersion: \d+\n',block,flags=re.M)[1:]:
                t = float(re.search(r'^        time: ([^\n]+)',key,re.M).group(1))
                vectors = [tuple(map(float,re.search(r'^        '+field+': '+vector_pattern,key,re.M).groups()))
                           for field in ('value','inSlope','outSlope')]
                keys.append((t,*vectors))
            result[path] = keys
        return result
    def evaluate(keys,t):
        if t <= keys[0][0]:
            return keys[0][1]
        for a,b in zip(keys,keys[1:]):
            if t <= b[0]+1e-7:
                dt = b[0]-a[0]
                u = min(1.,max(0.,(t-a[0])/dt))
                return tuple((2*u**3-3*u*u+1)*a[1][i]+(u**3-2*u*u+u)*dt*a[3][i]
                             +(-2*u**3+3*u*u)*b[1][i]+(u**3-u*u)*dt*b[2][i] for i in range(3))
        return keys[-1][1]
    for name in names:
        text = (repo/'Assets/Art/Animations/Player'/f'{name}.anim').read_text(encoding='utf-8')
        rotations,positions = curves(text,'m_EulerCurves'),curves(text,'m_PositionCurves')
        duration = max(key[0] for keys in [*rotations.values(),*positions.values()] for key in keys)
        count = round(duration*30)
        for frame in range(count+1):
            t = frame/30
            for path,keys in rotations.items():
                angles = [math.radians(v) for v in evaluate(keys,t)]
                unity = Matrix.Rotation(angles[1],3,'Y') @ Matrix.Rotation(angles[0],3,'X') @ Matrix.Rotation(angles[2],3,'Z')
                joint = joints[path]
                joint.rotation_euler = (basis @ unity @ basis.transposed()).to_euler('XYZ')
                joint.keyframe_insert('rotation_euler',frame=first+frame,group=name)
            for path,keys in positions.items():
                joints[path].location = blender_point(evaluate(keys,t))
                joints[path].keyframe_insert('location',frame=first+frame,group=name)
        ranges[name] = [first,first+count]
        scene.timeline_markers.new(name,frame=first)
        first += count+12
    scene.render.fps = 30
    scene.frame_start,scene.frame_end = 1,first-12
    scene['unity_animation_preview_ranges'] = json.dumps(ranges)
    scene.frame_set(1)
    return ranges

def preview_tool(scene, collection, grip, material, ranges):
    """An inspection prop with the current ToolPresentation hoe dimensions."""
    shape = Shape()
    shape.box((0,-.13,0),(.085,.8,.085),.008,'Leather',steps=2)
    shape.box((0,.26,.09),(.45,.09,.26),.008,'Metal',steps=2)
    tool = shape.make('Hoe Grip Check - Preview Only',grip,collection,material)
    for name,(first,last) in ranges.items():
        tool.hide_render = tool.hide_viewport = name != 'Hoe'
        for property_name in ('hide_render','hide_viewport'):
            tool.keyframe_insert(property_name,frame=first,group='Preview visibility')
            tool.keyframe_insert(property_name,frame=last,group='Preview visibility')
        for frame in range(first,last+1):
            progress = (frame-first)/max(1,last-first)
            tool.rotation_euler[0] = math.radians(-110*math.sin(progress*math.pi)) if name == 'Hoe' else 0
            tool.keyframe_insert('rotation_euler',frame=frame,group=name)
    scene.frame_set(1)

def export_current(repository, preview_directory):
    """Explicitly bake artist edits; does not recreate or overwrite scene geometry."""
    repo, preview = Path(repository),Path(preview_directory)
    scene = bpy.data.scenes.get(ART_SCENE)
    if scene is None or not scene.get('magic_island_generated_art'):
        raise RuntimeError('Open the Island Farmer art scene before exporting.')
    meshes = [scene.objects.get('IslandFarmer'+name) for name in JOINTS]
    if any(obj is None or obj.type != 'MESH' for obj in meshes):
        raise RuntimeError('Keep the six named joint meshes; merge new details into their matching mesh.')
    scene.frame_set(1)
    bpy.context.view_layer.update()
    if any(obj.modifiers or any(abs(obj.matrix_local[i][j]-(1 if i == j else 0)) > 1e-6
                                for i in range(4) for j in range(4)) for obj in meshes):
        raise RuntimeError('Apply mesh modifiers and mesh-object transforms before export; keep the joint transforms unchanged.')
    triangles = export_meshes(meshes,repo/'Assets/Art/Meshes/Player/IslandFarmer.meshdata.json')
    material = meshes[0].data.materials[0]
    image = next(node.image for node in material.node_tree.nodes if node.type == 'TEX_IMAGE')
    image.filepath_raw = str(repo/'Assets/Art/Textures/Player/IslandFarmerPalette.png')
    image.save()
    image.pack()
    source = repo/'SourceArt/Characters'
    source.mkdir(parents=True,exist_ok=True)
    bpy.data.libraries.write(str(source/'IslandFarmer.blend'),{scene},fake_user=True,compress=True)
    # Write a standalone main file without including the user's other scenes.
    # The library snapshot first limits the dependency graph to this art scene.
    packed = subprocess.run([bpy.app.binary_path,'--background','--factory-startup',
                             str(source/'IslandFarmer.blend'),'--python-exit-code','1',
                             '--python-expr',
                             'import bpy; bpy.context.preferences.filepaths.save_version=0; '
                             'bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,compress=True)'],
                            capture_output=True,text=True,timeout=60)
    if packed.returncode:
        raise RuntimeError('Could not package the standalone Blender source: '+packed.stderr[-2000:])
    bpy.ops.object.select_all(action='DESELECT')
    art = meshes[0].users_collection[0]
    for obj in art.objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.export_scene.fbx(filepath=str(source/'IslandFarmer.fbx'),use_selection=True,
                             object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',
                             apply_unit_scale=True,bake_space_transform=False,bake_anim=False,
                             path_mode='STRIP',use_custom_props=True)
    (preview/'model-summary.json').write_text(json.dumps({'triangles':triangles,'renderers':len(meshes),
        'paletteColors':len(PALETTE),'joints':JOINTS,
        'animations':json.loads(scene['unity_animation_preview_ranges']),
        'source':str(source/'IslandFarmer.blend')},indent=2),encoding='utf-8')
    print(json.dumps({'scene':scene.name,'triangles':triangles,'renderers':len(meshes),'source':str(source/'IslandFarmer.blend')}))

def stage(scene, collection, preview):
    def mat(name, code):
        material = bpy.data.materials.new(name)
        material.diffuse_color = (*rgb(code),1)
        material.use_nodes = True
        shader = next(node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
        shader.inputs['Base Color'].default_value = (*[linear(v) for v in rgb(code)],1)
        shader.inputs['Roughness'].default_value = .9
        return material
    floor = Shape()
    floor.box((0, .005, 0), (200, .08, 200), .001, 'Cream', steps=1)
    floor.make('Preview Ground', None, collection, mat('Preview Sand', 'EDE7DA'))
    scene.world = bpy.data.worlds.new('Island Farmer Studio')
    scene.world.use_nodes = True
    background = next(node for node in scene.world.node_tree.nodes if node.type == 'BACKGROUND')
    background.inputs['Color'].default_value = (.65,.72,.80,1)
    background.inputs['Strength'].default_value = .35
    for name, position, power, size, color in [
        ('Key Softbox',(-3,-4,5),450,4,(1,.91,.77)),
        ('Fill Softbox',(3,-2,3),230,3,(.78,.88,1)),
        ('Rim Softbox',(1,3,4),450,3,(1,.96,.86))]:
        data = bpy.data.lights.new(name,'AREA')
        data.energy, data.shape, data.size, data.color = power,'DISK',size,color
        light = bpy.data.objects.new(name,data)
        collection.objects.link(light)
        light.location = position
        light.rotation_euler = (Vector((0,0,.85))-light.location).to_track_quat('-Z','Y').to_euler()
    camera = bpy.data.objects.new('Farmer Portrait',bpy.data.cameras.new('Farmer Portrait'))
    collection.objects.link(camera)
    camera.location = (3.2,-6,3.0)
    camera.rotation_euler = (Vector((0,0,.88))-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type, camera.data.ortho_scale = 'ORTHO',2.18
    scene.camera = camera
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y, scene.render.resolution_percentage = 1000,1000,100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = str(preview/'IslandFarmerPortrait.png')
    scene.view_settings.view_transform = 'AgX'
    return camera

def build(repository, preview_directory):
    repo, preview = Path(repository),Path(preview_directory)
    preview.mkdir(parents=True, exist_ok=True)
    old = bpy.data.scenes.get(ART_SCENE)
    if old is not None:
        if not old.get('magic_island_generated_art'):
            raise RuntimeError('An artist-owned scene has this name; refusing to replace it.')
        if any(any(other != old for other in obj.users_scene) for obj in old.objects):
            raise RuntimeError('The art objects are shared with another scene; use export_current to preserve those links.')
        for obj in list(old.objects):
            bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.scenes.remove(old)
    scene = bpy.data.scenes.new(ART_SCENE)
    scene['magic_island_generated_art'] = 1
    bpy.context.window.scene = scene
    art = bpy.data.collections.new('IslandFarmer - Export')
    studio = bpy.data.collections.new('IslandFarmer - Preview Only')
    scene.collection.children.link(art)
    scene.collection.children.link(studio)
    palette, image = palette_material(repo)
    bob = bpy.data.objects.new('Bob',None)
    art.objects.link(bob)
    bob['unity_path'] = 'Bob'
    meshes = []
    for name, position in JOINTS.items():
        joint = bob if name == 'Body' else bpy.data.objects.new(name,None)
        if joint != bob:
            art.objects.link(joint)
            joint.parent, joint.location = bob,blender_point(position)
            joint['unity_path'] = 'Bob/'+name
            if name.endswith('Arm'):
                side = -1 if name.startswith('Left') else 1
                joint.rotation_euler[1] = math.radians(side*10)
        shape = make_body() if name == 'Body' else make_head() if name == 'Head' else make_arm(-1 if name.startswith('Left') else 1) if name.endswith('Arm') else make_leg(-1 if name.startswith('Left') else 1)
        meshes.append(shape.make('IslandFarmer'+name,joint,art,palette))
    grip = bpy.data.objects.new('ToolSocket',None)
    art.objects.link(grip)
    grip.parent = next(obj for obj in art.objects if obj.get('unity_path') == 'Bob/RightArm')
    grip.location = blender_point((0,-.40,.10))
    stage(scene,studio,preview)
    animation_ranges = preview_animations(scene,repo,bob)
    preview_tool(scene,studio,grip,palette,animation_ranges)
    for window in bpy.context.window_manager.windows:
        if window.scene != scene:
            continue
        for area in window.screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.shading.type = 'MATERIAL'
                area.spaces.active.region_3d.view_perspective = 'CAMERA'
    export_current(repository,preview_directory)

