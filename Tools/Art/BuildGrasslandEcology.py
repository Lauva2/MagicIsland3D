"""Blender MCP source models; preserve the user's scene and existing exports."""
from pathlib import Path
import bpy, json, math
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'SourceArt/GrasslandEcology'

def main():
    if (OUT/'GrasslandEcology.blend').exists() or (OUT/'GrasslandEcology.meshdata.json').exists():
        raise RuntimeError('Existing ecology source preserved.')
    previous=bpy.context.window.scene
    scene=bpy.data.scenes.new('Grassland Ecology Studio');scene['MagicIslandGrasslandEcology']=1
    groups={};palette={};bpy.context.window.scene=scene
    def material(name,rgb):
        m=bpy.data.materials.new('Meadow.'+name);m.diffuse_color=(*rgb,1);m.use_nodes=True
        bsdf=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bsdf.inputs['Base Color'].default_value=(*rgb,1);bsdf.inputs['Roughness'].default_value=.8
        palette[m.name]=(*rgb,1);return m
    lemon=material('Lemon wings',(.98,.86,.43));cream=material('Cream wing tips',(1,.97,.77))
    lilac=material('Lilac wing edges',(.70,.61,.87));body=material('Soft umber body',(.29,.26,.19));glow=material('Warm firefly abdomen',(.99,.92,.44))
    def mesh(kind,label,v,faces,mats,indices=None,wing=False,smooth=False):
        m=bpy.data.meshes.new(label);m.from_pydata(v,[],faces);m.update()
        o=bpy.data.objects.new(label,m);scene.collection.objects.link(o);o['wing']=int(wing)
        for mat in mats:m.materials.append(mat)
        for i,p in enumerate(m.polygons):p.material_index=indices[i] if indices else 0;p.use_smooth=smooth
        groups.setdefault(kind,[]).append(o);return o
    def ball(kind,label,p,scale,mat):
        v=[Vector(p)+Vector((0,0,-scale[2]))];n=8;rings=4
        for j in range(1,rings):
            a=-math.pi/2+j*math.pi/rings
            for i in range(n):
                b=i*math.tau/n;v.append(Vector(p)+Vector((math.cos(a)*math.cos(b)*scale[0],math.cos(a)*math.sin(b)*scale[1],math.sin(a)*scale[2])))
        v.append(Vector(p)+Vector((0,0,scale[2])))
        f=[(0,1+(i+1)%n,1+i) for i in range(n)]
        for j in range(rings-2):
            for i in range(n):f.append((1+j*n+i,1+j*n+(i+1)%n,1+(j+1)*n+(i+1)%n,1+(j+1)*n+i))
        f.extend((len(v)-1,1+(rings-2)*n+i,1+(rings-2)*n+(i+1)%n) for i in range(n))
        return mesh(kind,label,v,f,[mat],smooth=True)
    for kind in ['Butterfly','Firefly']:
        if kind=='Butterfly':
            ball(kind,'Rounded thorax',(0,0,.015),(.035,.105,.035),body)
            ball(kind,'Head',(0,.12,.025),(.042,.04,.035),body)
            for sign in [-1,1]:
                # Broad forewings and smaller rounded hindwings, creased slightly above the body.
                for back in [False,True]:
                    center=Vector((sign*(.135 if not back else .12),.055 if not back else -.085,.018))
                    v=[center];n=10
                    for k in range(n):
                        a=k*math.tau/n;v.append(center+Vector((sign*math.cos(a)*(.13 if not back else .105),math.sin(a)*(.125 if not back else .095),.008*math.cos(a))))
                    faces=[(0,1+k,1+(k+1)%n) for k in range(n)]
                    if sign<0:faces=[tuple(reversed(f)) for f in faces]
                    mesh(kind,'Rounded butterfly wing',v,faces,[lemon,cream,lilac],[1 if k in (0,1,9) else 2 if back and k in (4,5) else 0 for k in range(n)],wing=True)
        else:
            ball(kind,'Firefly thorax',(0,.025,.015),(.03,.045,.027),body)
            ball(kind,'Firefly luminous tail',(0,-.05,.015),(.052,.073,.047),glow)
    templates=[]
    for kind,objects in groups.items():
        positions=[];normals=[];colors=[];wings=[];triangles=[]
        for o in objects:
            m=o.data;m.calc_loop_triangles()
            for t in m.loop_triangles:
                first=len(positions)//3;color=palette[m.materials[t.material_index].name]
                for vi in t.vertices:
                    p=m.vertices[vi].co;n=m.vertices[vi].normal if m.polygons[t.polygon_index].use_smooth else t.normal
                    positions.extend(round(f,6) for f in (p.x,p.z,p.y));normals.extend(round(f,6) for f in (n.x,n.z,n.y));colors.extend(color);wings.append(o['wing'])
                triangles.extend((first,first+2,first+1))
        templates.append(dict(name=kind,positions=positions,normals=normals,colors=colors,wings=wings,triangles=triangles))
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/'GrasslandEcology.meshdata.json').write_text(json.dumps(dict(version=1,templates=templates),separators=(',',':')),encoding='utf8')
    bpy.data.libraries.write(str(OUT/'GrasslandEcology.blend'),{scene},fake_user=True,compress=True)
    bpy.context.window.scene=previous
    print('Grassland ecology exported:',[(t['name'],len(t['triangles'])//3) for t in templates],'; original scene restored:',previous.name)

if __name__=='__main__':main()
