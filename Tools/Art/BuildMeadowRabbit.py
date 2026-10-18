"""Run through Blender MCP. Create a library scene without replacing the user's scene."""
from pathlib import Path
import bpy, json, math
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'SourceArt/MeadowRabbit'

def main():
    if (OUT/'MeadowRabbit.blend').exists() or (OUT/'MeadowRabbit.meshdata.json').exists():
        raise RuntimeError('Existing rabbit source preserved; use a new explicit source revision.')
    previous=bpy.context.window.scene
    scene=bpy.data.scenes.new('Meadow Rabbit Studio');scene['MagicIslandMeadowRabbit']=1
    bpy.context.window.scene=scene
    palette={};objects=[]
    def material(name,rgb):
        m=bpy.data.materials.new('MeadowRabbit.'+name);m.diffuse_color=(*rgb,1);m.use_nodes=True
        bsdf=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        bsdf.inputs['Base Color'].default_value=(*rgb,1);bsdf.inputs['Roughness'].default_value=.85
        palette[m.name]=(*rgb,1);return m
    fur=material('Warm cream',(.87,.80,.65));light=material('Cream muzzle',(1,.95,.83))
    pink=material('Soft rose',(.80,.49,.46));eye=material('Umber eyes',(.18,.14,.11));glint=material('Eye glints',(1,.98,.91))
    head=(0,.17,.40)
    def ball(label,p,scale,mat,part=0,pivot=(0,0,0),tilt=0,n=12,rings=6):
        v=[Vector((0,0,-1))]
        for j in range(1,rings):
            a=-math.pi/2+j*math.pi/rings
            for i in range(n):
                b=i*math.tau/n;v.append(Vector((math.cos(a)*math.cos(b),math.cos(a)*math.sin(b),math.sin(a))))
        v.append(Vector((0,0,1)))
        from mathutils import Quaternion
        rotate=Quaternion((0,1,0),tilt)
        v=[Vector(p)+rotate@Vector((q.x*scale[0],q.y*scale[1],q.z*scale[2])) for q in v]
        faces=[(0,1+(i+1)%n,1+i) for i in range(n)]
        for j in range(rings-2):
            for i in range(n):faces.append((1+j*n+i,1+j*n+(i+1)%n,1+(j+1)*n+(i+1)%n,1+(j+1)*n+i))
        faces.extend((len(v)-1,1+(rings-2)*n+i,1+(rings-2)*n+(i+1)%n) for i in range(n))
        mesh=bpy.data.meshes.new(label);mesh.from_pydata(v,[],faces);mesh.update();mesh.materials.append(mat)
        o=bpy.data.objects.new(label,mesh);scene.collection.objects.link(o);o['part']=part;o['pivot']=pivot
        for face in mesh.polygons:face.use_smooth=True
        objects.append(o)
    try:
        # Blender +Y is the face direction, +Z is up. The export converts this to Unity +Z/+Y.
        ball('Pear shaped body',(0,-.065,.255),(.205,.245,.215),fur)
        ball('Soft chest',(0,.075,.29),(.15,.16,.175),light)
        ball('Fluffy tail',(0,-.285,.26),(.085,.075,.08),light,n=10,rings=5)
        ball('Rounded head',(0,.185,.445),(.15,.145,.14),fur,1,head)
        ball('Muzzle left',(-.041,.305,.414),(.065,.045,.047),light,1,head,n=10,rings=5)
        ball('Muzzle right',(.041,.305,.414),(.065,.045,.047),light,1,head,n=10,rings=5)
        ball('Little nose',(0,.345,.432),(.025,.018,.018),pink,1,head,n=8,rings=4)
        for side in [-1,1]:
            ball('Round eye', (side*.125,.265,.485),(.026,.027,.032),eye,1,head,n=10,rings=5)
            ball('Eye glint',(side*.137,.285,.497),(.008,.009,.010),glint,1,head,n=8,rings=4)
            ear=(side*.083,.16,.535);part=2 if side<0 else 3
            ball('Long soft ear',(side*.096,.155,.665),(.048,.044,.155),fur,part,ear,side*.10)
            ball('Rose inner ear',(side*.097,.193,.672),(.027,.012,.119),pink,part,ear,side*.10,n=10,rings=5)
            ball('Front paw',(side*.102,.16,.065),(.050,.10,.060),light,4 if side<0 else 5,(side*.102,.10,.19),n=10,rings=5)
            ball('Hind haunch',(side*.14,-.15,.16),(.090,.113,.12),fur,6 if side<0 else 7,(side*.14,-.14,.21),n=10,rings=5)
            ball('Hind paw',(side*.145,-.09,.052),(.065,.135,.047),light,6 if side<0 else 7,(side*.14,-.14,.21),n=10,rings=5)
        positions=[];normals=[];colors=[];parts=[];pivots=[];triangles=[]
        for o in objects:
            mesh=o.data;mesh.calc_loop_triangles()
            for tri in mesh.loop_triangles:
                first=len(parts);color=palette[mesh.materials[tri.material_index].name]
                for vi in tri.vertices:
                    q=mesh.vertices[vi].co;n=mesh.vertices[vi].normal;p=o['pivot']
                    positions.extend(round(f,6) for f in (q.x,q.z,q.y));normals.extend(round(f,6) for f in (n.x,n.z,n.y))
                    colors.extend(color);parts.append(o['part']);pivots.extend((p[0],p[2],p[1]))
                triangles.extend((first,first+2,first+1))
        OUT.mkdir(parents=True,exist_ok=True)
        (OUT/'MeadowRabbit.meshdata.json').write_text(json.dumps(dict(version=1,positions=positions,normals=normals,colors=colors,parts=parts,pivots=pivots,triangles=triangles),separators=(',',':')),encoding='utf8')
        bpy.data.libraries.write(str(OUT/'MeadowRabbit.blend'),{scene},fake_user=True,compress=True)
        print('Meadow rabbit:',len(triangles)//3,'triangles;',len(objects),'authored parts; library saved. Original scene:',previous.name)
    finally:bpy.context.window.scene=previous

if __name__=='__main__':main()
