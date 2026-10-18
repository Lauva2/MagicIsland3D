"""Run through Blender MCP. Rebuild only the dedicated ancient-tree source scene."""
from pathlib import Path
import bpy, math, json, random
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'SourceArt/WorldPlanning/Models'
scene = bpy.data.scenes.get('Starter Ancient Tree Studio')
if scene is None:
    scene = bpy.data.scenes.new('Starter Ancient Tree Studio')
else:
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
bpy.context.window.scene = scene
objects, palette, metadata = [], {}, {}
rng = random.Random(821807)

def material(name, rgb, weight=0):
    m = bpy.data.materials.get('AncientTree.'+name) or bpy.data.materials.new('AncientTree.'+name)
    m.use_nodes = True
    node = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    node.inputs['Base Color'].default_value = (*rgb, 1)
    node.inputs['Roughness'].default_value = .78
    m.diffuse_color = (*rgb, 1)
    palette[m.name] = (*rgb, weight)
    return m

bark = material('Silver warm bark', (.46,.48,.39))
barklight = material('Bark ridges', (.66,.65,.51))
leaf = material('Mint foliage', (.48,.73,.60), .65)
leaflight = material('Silver foliage', (.69,.85,.71), .9)
leafdark = material('Leaf underside', (.34,.57,.48), .45)
flower = material('Lavender lanterns', (.59,.55,.95), .2)
heart = material('Flower light', (.77,.87,1), .12)
rune = material('Mint inlay', (.61,.94,.87))

def mesh(name, vertices, faces, mats, indices=None, mote=False):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces); data.update()
    obj = bpy.data.objects.new(name,data); scene.collection.objects.link(obj)
    for m in mats: data.materials.append(m)
    if indices:
        for p, i in zip(data.polygons,indices): p.material_index=i
    objects.append(obj); metadata[obj.name]=mote
    return obj

def tube(name, points, radii, mats, sides=9):
    vertices=[]
    for j, point in enumerate(points):
        tangent=Vector(points[min(j+1,len(points)-1)])-Vector(points[max(0,j-1)])
        tangent.normalize(); right=tangent.cross(Vector((0,1,0))).normalized()
        if right.length<.1: right=Vector((1,0,0))
        up=tangent.cross(right).normalized()
        for i in range(sides):
            a=i*math.tau/sides
            vertices.append(Vector(point)+(right*math.cos(a)+up*math.sin(a))*radii[j])
    faces=[tuple(reversed(range(sides)))]; indices=[0]
    for j in range(len(points)-1):
        for i in range(sides):
            a=j*sides+i; b=j*sides+(i+1)%sides
            faces.append((a,b,b+sides,a+sides));indices.append((i+j)%len(mats))
    faces.append(tuple((len(points)-1)*sides+i for i in range(sides)));indices.append(0)
    return mesh(name,vertices,faces,mats,indices)

def ellipsoid(name, center, scale, mats, rings=5, sides=10):
    vertices=[(center[0],center[1],center[2]-scale[2])]
    for j in range(1,rings):
        a=-math.pi/2+j*math.pi/rings
        for i in range(sides):
            b=i*math.tau/sides; wobble=1+.06*math.sin(b*3+j)
            vertices.append((center[0]+math.cos(a)*math.cos(b)*scale[0]*wobble,center[1]+math.cos(a)*math.sin(b)*scale[1]*wobble,center[2]+math.sin(a)*scale[2]))
    vertices.append((center[0],center[1],center[2]+scale[2]));faces=[]
    for i in range(sides): faces.append((0,1+(i+1)%sides,1+i))
    for j in range(rings-2):
        for i in range(sides):
            a=1+j*sides+i;b=1+j*sides+(i+1)%sides;faces.append((a,b,b+sides,a+sides))
    for i in range(sides): faces.append((len(vertices)-1,1+(rings-2)*sides+i,1+(rings-2)*sides+(i+1)%sides))
    return mesh(name,vertices,faces,mats,[0 for _ in faces])

tube('Twisted elder trunk',[(0,0,-.25),(-.6,.2,2),(0,.7,5),(1.3,.6,8.4),(1.9,0,11.4),(1.1,-.5,14.4),(.1,-.4,17.2)], [2.35,2,1.65,1.4,1.1,.75,.18],[bark,barklight,bark],12)
for i in range(9):
    a=i*math.tau/9+.17
    end=Vector((math.cos(a)*(6.2+i%3*.65),math.sin(a)*(6.2+i%3*.65),.05))
    tube('Buttress root %02d'%i,[(0,0,2.1),end*.28+Vector((0,0,.5)),end*.68+Vector((0,0,.25)),end], [1.05,.7,.3,.045],[bark,barklight,bark],7)

for i in range(7):
    a=i*math.tau/7+.22
    r=8.3+(i%3)*.85
    tip=Vector((math.cos(a)*r,math.sin(a)*r,13.8+(i%3)*1.45))
    start=Vector((.6,.3,7.1+(i%2)*1.4))
    middle=tip*.57+Vector((.1,0,2.8))
    tube('Sweeping bough %02d'%i,[start,middle,tip],[.95,.62,.17],[bark,barklight,bark],8)
    for j in range(3):
        offset=Vector((math.cos(a+.8)*((j-1)*2.2),math.sin(a+.8)*((j-1)*2.2),.9+(j%2)*1.3))
        c=tip+offset
        ellipsoid('Layered foliage %02d %d'%(i,j),c,(4.7-j*.35,3.7,2.6),[leaf,leaflight,leafdark])
    for j in range(6):
        b=a+(j-2.5)*.16
        top=tip+Vector((math.cos(b)*2,math.sin(b)*2,-.1))
        length=3.4+rng.random()*3.7
        bottom=top+Vector((math.cos(b)*1.3,math.sin(b)*1.3,-length))
        tube('Willow strand %02d %d'%(i,j),[top,(top+bottom)*.5+Vector((.2,0,.2)),bottom],[.07,.045,.02],[leafdark],5)
        for k in range(6):
            t=(k+1)/7; p=top.lerp(bottom,t)
            side=Vector((math.cos(b+math.pi/2),math.sin(b+math.pi/2),0))*(.5+(.1 if k%2 else 0))
            for sign in [-1,1]:
                q=p+side*sign+Vector((0,0,-.45))
                mesh('Drooping leaf',[p,p+side*sign*.75+Vector((0,0,.1)),q,p+Vector((0,0,-.2))],[(0,1,2,3),(3,2,1,0)],[leaflight if k%3==0 else leaf])
        if j%2==0:
            ellipsoid('Lantern flower',bottom,(.34,.34,.62),[flower,heart],4,6)
            ellipsoid('Lantern calyx',bottom+Vector((0,0,.48)),(.38,.38,.18),[leaflight],3,6)

ellipsoid('Highest crown',(0,0,18.3),(5.7,5,3.6),[leaflight,leaf,leafdark],6,12)
# Bark inlays follow the front of the bent trunk, keeping the ground entrance clear.
for i in range(4):
    h=2.8+i*1.55; x=.15+i*.22
    tube('Luminous bark vein',[(x,-1.72,h),(x+.55,-1.63,h+.55),(x+.2,-1.5,h+1.2)],[.045,.055,.035],[rune],5)
for i in range(36):
    a=i*2.399963; r=4+(i%8)*.9; z=1.1+(i%7)*.64
    p=Vector((math.cos(a)*r,math.sin(a)*r,z));s=.06+(i%3)*.025
    mesh('Drifting light %02d'%i,[p+Vector((s,0,0)),p+Vector((0,s,0)),p+Vector((-s,0,0)),p+Vector((0,-s,0)),p+Vector((0,0,s)),p-Vector((0,0,s))],[(0,1,4),(1,2,4),(2,3,4),(3,0,4),(1,0,5),(2,1,5),(3,2,5),(0,3,5)],[heart],mote=True)

positions,colors,triangles,uv=[],[],[],[]
depsgraph=bpy.context.evaluated_depsgraph_get()
for obj in objects:
    evaluated=obj.evaluated_get(depsgraph); data=evaluated.to_mesh();data.calc_loop_triangles()
    for t in data.loop_triangles:
        color=palette[data.materials[t.material_index].name];first=len(positions)//3
        for vi in t.vertices:
            p=obj.matrix_world@data.vertices[vi].co
            positions.extend(round(v,6) for v in (p.x,p.z,p.y));colors.extend(color)
            uv.extend((round((sum(ord(c) for c in obj.name)%101)*.061,5),2 if metadata[obj.name] else 0))
        triangles.extend((first,first+2,first+1))
    evaluated.to_mesh_clear()
OUT.mkdir(parents=True,exist_ok=True)
(OUT/'StarterAncientTree.meshdata.json').write_text(json.dumps(dict(version=1,positions=positions,colors=colors,triangles=triangles,uv=uv),separators=(',',':')),encoding='utf8')
for obj in scene.objects: obj.select_set(False)
objects[0].select_set(True);bpy.context.view_layer.objects.active=objects[0]
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'StarterAncientTree.blend'),copy=True)
print('Ancient tree exported:',len(triangles)//3,'triangles;',len(objects),'source parts; one Unity runtime mesh.')
