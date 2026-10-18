"""Run with Blender MCP; writes only the dedicated beach studio and export.

Existing exports are preserved unless main(replace=True) is explicitly requested.
The user's original scene and objects are retained. Preview the studio, then restore
the scene stored in bpy.app.driver_namespace['BeachCompositionPreviousScene'].
"""
from pathlib import Path
import bpy, json, math
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'SourceArt/BeachComposition'

def main(replace=False):
    if (OUT / 'BeachComposition.blend').exists() and not replace:
        raise RuntimeError('Existing beach source preserved; use explicit replace=True to regenerate.')
    previous = bpy.context.window.scene
    name = 'Beach Composition Studio'
    old = bpy.data.scenes.get(name)
    if old:
        if not replace or old.get('MagicIslandBeachSource') != 1:
            raise RuntimeError('Existing studio preserved; use an owned studio and explicit replace=True.')
        if previous == old:
            previous = bpy.data.scenes[bpy.app.driver_namespace['BeachCompositionPreviousScene']]
            bpy.context.window.scene = previous
        for obj in list(old.objects):bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.scenes.remove(old)
    scene = bpy.data.scenes.new(name)
    scene['MagicIslandBeachSource'] = 1
    bpy.app.driver_namespace['BeachCompositionPreviousScene'] = previous.name
    bpy.context.window.scene = scene
    groups, palette = {}, {}
    def material(label, rgb):
        m = bpy.data.materials.new('BeachComposition.' + label)
        m.diffuse_color = (*rgb, 1)
        m.use_nodes = True
        node = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        node.inputs['Base Color'].default_value = (*rgb, 1)
        node.inputs['Roughness'].default_value = .83
        palette[m.name] = (*rgb, 1)
        return m
    stone = material('Warm grey stone', (.65,.68,.62))
    stone_top = material('Sun bleached stone', (.77,.78,.69))
    wood = material('Weathered driftwood', (.57,.45,.31))
    end = material('Pale wood ends', (.75,.64,.46))
    grain = material('Soft grain', (.63,.51,.35))
    green = material('Sage grass', (.48,.59,.33))
    tips = material('Grass tips', (.66,.69,.42))
    def mesh(kind, label, verts, faces, materials, indices=None, smooth=False):
        data = bpy.data.meshes.new(label)
        data.from_pydata(verts, [], faces);data.update()
        obj = bpy.data.objects.new(label, data);scene.collection.objects.link(obj)
        for m in materials:data.materials.append(m)
        for i,p in enumerate(data.polygons):
            p.material_index=indices[i] if indices else 0;p.use_smooth=smooth
        groups.setdefault(kind, []).append(obj)
        return obj
    def pebble(center, size, phase):
        verts=[(center[0],center[1],center[2]-size[2])];sides=12;rings=7
        for j in range(1,rings):
            a=-math.pi/2+math.pi*j/rings
            for k in range(sides):
                b=k*math.tau/sides;w=1+.035*math.sin(3*b+phase)
                verts.append((center[0]+math.cos(a)*math.cos(b)*size[0]*w,
                              center[1]+math.cos(a)*math.sin(b)*size[1]*w,
                              center[2]+math.sin(a)*size[2]))
        verts.append((center[0],center[1],center[2]+size[2]))
        faces=[(0,1+(k+1)%sides,1+k) for k in range(sides)];indices=[0]*sides
        for j in range(rings-2):
            for k in range(sides):
                faces.append((1+j*sides+k,1+j*sides+(k+1)%sides,1+(j+1)*sides+(k+1)%sides,1+(j+1)*sides+k))
                indices.append(1 if j>=4 else 0)
        faces.extend((len(verts)-1,1+(rings-2)*sides+k,1+(rings-2)*sides+(k+1)%sides) for k in range(sides));indices.extend([1]*sides)
        return mesh('Rocks','Rounded shore stone',verts,faces,[stone,stone_top],indices,True)
    pebble((-.10,.02,.16),(.28,.23,.22),.2)
    pebble((.20,.07,.075),(.16,.14,.12),1.9)
    pebble((-.04,-.25,.04),(.13,.12,.085),3.7)
    def branch(points, radii, label):
        verts=[];sides=10
        for j,point in enumerate(points):
            tangent=Vector(points[min(j+1,len(points)-1)])-Vector(points[max(0,j-1)])
            tangent.normalize();side=tangent.cross(Vector((0,0,1))).normalized();up=tangent.cross(side).normalized()
            for k in range(sides):
                a=k*math.tau/sides
                verts.append(Vector(point)+(side*math.cos(a)+up*math.sin(a))*radii[j]*(1+.06*math.sin(k*3+j)))
        faces=[tuple(reversed(range(sides)))];indices=[1]
        for j in range(len(points)-1):
            for k in range(sides):
                a=j*sides+k;b=j*sides+(k+1)%sides
                faces.append((a,b,b+sides,a+sides));indices.append(2 if k%4==0 else 0)
        faces.append(tuple((len(points)-1)*sides+k for k in range(sides)));indices.append(1)
        return mesh('Driftwood',label,verts,faces,[wood,end,grain],indices,True)
    branch([(-.37,-.07,.075),(-.15,-.03,.09),(.11,.02,.10),(.37,.08,.09)],[.065,.079,.07,.045],'Rounded weathered log')
    branch([(-.06,-.015,.10),(.03,.14,.12),(.10,.26,.14)],[.047,.035,.012],'Short driftwood fork')
    # Thick folded blades retain rounded silhouettes; no runtime wind or individual objects.
    for k in range(11):
        a=k*2.399963;r=.045+(k%3)*.025;h=.16+(k%4)*.033
        base=Vector((math.cos(a)*r,math.sin(a)*r,0))
        direction=Vector((math.cos(a),math.sin(a),0));side=Vector((-math.sin(a),math.cos(a),0))*.022
        mid=base+direction*.05+Vector((0,0,h*.58));tip=base+direction*.12+Vector((0,0,h))
        mesh('Grass','Folded dune blade',[base-side,base+side,mid+side*.8,tip,mid-side*.8,mid+Vector((0,0,.015))],
             [(0,1,5),(1,2,5),(2,3,5),(3,4,5),(4,0,5),(5,1,0),(5,2,1),(5,3,2),(5,4,3),(5,0,4)],
             [green,tips],[0,0,1,1,0,0,0,1,1,0])
    data=[]
    for kind,objects in groups.items():
        positions=[];normals=[];colors=[];triangles=[]
        for obj in objects:
            m=obj.data;m.calc_loop_triangles()
            for t in m.loop_triangles:
                first=len(positions)//3;color=palette[m.materials[t.material_index].name]
                for vi in t.vertices:
                    p=m.vertices[vi].co;n=m.vertices[vi].normal if m.polygons[t.polygon_index].use_smooth else t.normal
                    positions.extend(round(v,6) for v in (p.x,p.z,p.y))
                    normals.extend(round(v,6) for v in (n.x,n.z,n.y));colors.extend(color)
                triangles.extend((first,first+2,first+1))
        data.append(dict(name=kind,positions=positions,normals=normals,colors=colors,triangles=triangles))
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/'BeachComposition.meshdata.json').write_text(json.dumps(dict(version=1,templates=data),separators=(',',':')),encoding='utf8')
    # Library export contains only this studio, preserving the user's open file and original scene.
    bpy.data.libraries.write(str(OUT/'BeachComposition.blend'),{scene},fake_user=True,compress=True)
    # Spread only the live preview; exported native data and blend retain centered templates.
    for i,(kind,objects) in enumerate(groups.items()):
        for obj in objects:obj.location.x+=(i-1)*1.2
    print('Beach templates exported:',[(d['name'],len(d['triangles'])//3) for d in data])

if __name__ == '__main__':main()
