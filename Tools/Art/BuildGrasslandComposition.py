"""Blender MCP authoring for the grassland route; never replaces an existing export.

The dedicated studio is exported as a library, preserving the user's open scene.
Coordinates and winding are converted to Unity at export. No runtime generation.
"""
from pathlib import Path
import bpy, json, math
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'SourceArt/GrasslandComposition'

def main(replace=False):
    name = 'Grassland Composition Studio'
    if (OUT/'GrasslandComposition.blend').exists() and not replace:
        raise RuntimeError('Existing grassland source/studio preserved.')
    previous = bpy.context.window.scene
    old = bpy.data.scenes.get(name)
    if old:
        if not replace or old.get('MagicIslandGrasslandSource') != 1:
            raise RuntimeError('Existing studio preserved; explicit replace requires an owned studio.')
        if previous == old:
            previous = bpy.data.scenes[bpy.app.driver_namespace['GrasslandCompositionPreviousScene']]
            bpy.context.window.scene = previous
        for obj in list(old.objects):bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.scenes.remove(old)
    scene = bpy.data.scenes.new(name)
    scene['MagicIslandGrasslandSource'] = 1
    bpy.app.driver_namespace['GrasslandCompositionPreviousScene'] = previous.name
    bpy.context.window.scene = scene
    groups, palette = {}, {}
    def material(label, rgb):
        m = bpy.data.materials.new('GrasslandComposition.'+label)
        m.diffuse_color = (*rgb,1);m.use_nodes = True
        node = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        node.inputs['Base Color'].default_value = (*rgb,1)
        node.inputs['Roughness'].default_value = .85
        palette[m.name] = (*rgb,1)
        return m
    stone = material('Warm limestone',(.66,.69,.56))
    moss = material('Moss caps',(.48,.61,.32))
    leaf = material('Meadow leaves',(.34,.53,.27))
    tip = material('Young sage tips',(.60,.72,.36))
    cream = material('Cream petals',(.99,.91,.65))
    lilac = material('Soft lilac petals',(.68,.60,.86))
    gold = material('Golden pollen',(.90,.59,.23))
    def mesh(kind,label,verts,faces,mats,indices=None,smooth=False):
        data=bpy.data.meshes.new(label);data.from_pydata(verts,[],faces);data.update()
        obj=bpy.data.objects.new(label,data);scene.collection.objects.link(obj)
        for m in mats:data.materials.append(m)
        for i,p in enumerate(data.polygons):p.material_index=indices[i] if indices else 0;p.use_smooth=smooth
        groups.setdefault(kind,[]).append(obj)
        return obj
    def ball(kind,label,center,size,mats,sides=10,rings=6):
        verts=[Vector(center)+Vector((0,0,-size[2]))]
        for j in range(1,rings):
            a=-math.pi/2+math.pi*j/rings
            for k in range(sides):
                b=k*math.tau/sides
                verts.append(Vector(center)+Vector((math.cos(a)*math.cos(b)*size[0],math.cos(a)*math.sin(b)*size[1],math.sin(a)*size[2])))
        verts.append(Vector(center)+Vector((0,0,size[2])))
        faces=[(0,1+(k+1)%sides,1+k) for k in range(sides)];indices=[0]*sides
        for j in range(rings-2):
            for k in range(sides):
                faces.append((1+j*sides+k,1+j*sides+(k+1)%sides,1+(j+1)*sides+(k+1)%sides,1+(j+1)*sides+k))
                indices.append(1 if len(mats)>1 and j>=rings-3 else 0)
        faces.extend((len(verts)-1,1+(rings-2)*sides+k,1+(rings-2)*sides+(k+1)%sides) for k in range(sides));indices.extend([len(mats)-1]*sides)
        return mesh(kind,label,verts,faces,mats,indices,True)
    ball('Rocks','Mossy rounded boulder',(-.13,.04,.27),(.55,.43,.33),[stone,moss])
    ball('Rocks','Companion pebble',(.40,-.18,.12),(.27,.23,.17),[stone,moss])
    def blade(kind,base,a,h,width,lean):
        base=Vector(base);side=Vector((-math.sin(a),math.cos(a),0))*width
        d=Vector((math.cos(a),math.sin(a),0));mid=base+d*lean*.45+Vector((0,0,h*.55));top=base+d*lean+Vector((0,0,h))
        verts=[base-side,base+side,mid+side*.7,top,mid-side*.7,mid+Vector((0,0,.025))]
        faces=[(0,1,5),(1,2,5),(2,3,5),(3,4,5),(4,0,5)]
        faces+= [tuple(reversed(f)) for f in faces]
        mesh(kind,'Folded meadow leaf',verts,faces,[leaf,tip],[0,0,1,1,0]*2)
    for k in range(19):
        a=k*2.399963;r=.05+(k%4)*.07
        blade('Grass',(math.cos(a)*r,math.sin(a)*r,0),a,.35+(k%5)*.10,.035,.19)
    for k in range(7):
        a=k*2.399963;r=.06+(k%3)*.15;h=.56+(k%4)*.13
        p=Vector((math.cos(a)*r,math.sin(a)*r,h))
        # Thin triangular stem prism, with folded leaves at different heights.
        verts=[]
        for z in (0,h):
            verts += [(p.x+.015*math.cos(q*math.tau/3),p.y+.015*math.sin(q*math.tau/3),z) for q in range(3)]
        mesh('Flowers','Flower stem',verts,[(0,1,4,3),(1,2,5,4),(2,0,3,5),(3,4,5)],[leaf])
        blade('Flowers',(p.x,p.y,h*.30),a+.8,.22,.035,.16)
        for petal in range(5):
            angle=a+petal*math.tau/5;direction=Vector((math.cos(angle),math.sin(angle),0));side=Vector((-math.sin(angle),math.cos(angle),0))
            c=p+direction*.125
            verts=[p+Vector((0,0,-.012)),c+side*.08,c+direction*.14+Vector((0,0,.035)),c-side*.08,c+Vector((0,0,.047))]
            faces=[(0,1,4),(1,2,4),(2,3,4),(3,0,4),(0,3,2),(0,2,1)]
            faces=[tuple(reversed(f)) for f in faces]
            mesh('Flowers','Rounded flower petal',verts,faces,[cream if k%3 else lilac],smooth=True)
        ball('Flowers','Pollen center',p+Vector((0,0,.036)),(.055,.055,.045),[gold],8,4)
    templates=[]
    for kind,objects in groups.items():
        positions=[];normals=[];colors=[];triangles=[]
        for obj in objects:
            m=obj.data;m.calc_loop_triangles()
            for t in m.loop_triangles:
                first=len(positions)//3;color=palette[m.materials[t.material_index].name]
                for vi in t.vertices:
                    p=m.vertices[vi].co;n=m.vertices[vi].normal if m.polygons[t.polygon_index].use_smooth else t.normal
                    positions.extend(round(v,6) for v in (p.x,p.z,p.y));normals.extend(round(v,6) for v in (n.x,n.z,n.y));colors.extend(color)
                triangles.extend((first,first+2,first+1))
        templates.append(dict(name=kind,positions=positions,normals=normals,colors=colors,triangles=triangles))
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/'GrasslandComposition.meshdata.json').write_text(json.dumps(dict(version=1,templates=templates),separators=(',',':')),encoding='utf8')
    bpy.data.libraries.write(str(OUT/'GrasslandComposition.blend'),{scene},fake_user=True,compress=True)
    for i,objects in enumerate(groups.values()):
        for obj in objects:obj.location.x+=(i-1)*2.0
    print('Grassland templates:',[(t['name'],len(t['triangles'])//3) for t in templates])

if __name__=='__main__':main()
