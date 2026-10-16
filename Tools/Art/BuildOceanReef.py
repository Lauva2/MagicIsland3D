"""Explicit Blender authoring/export for eight reef plants and small decorations."""
from pathlib import Path
import json,math,subprocess
import bpy
from mathutils import Vector
SCENE='MagicIsland - Reef Models'
NAMES=['Branch Coral','Fan Coral','Table Coral','Boulder Coral','Seagrass Blades','Tube Sponge','Sea Star','Clam Shell']
PALETTE=[('White','FFFFFF'),('Mid','D0D0D0'),('Dark','707070'),('Light','ECECEC')]

def support(repo):
    ns={'__name__':'reef_helpers'};p=repo/'Tools/Art/BuildOceanWildlife.py'
    exec(compile(p.read_text(encoding='utf-8'),str(p),'exec'),ns);ns['PALETTE']=PALETTE
    shapes=ns['support'](repo)
    return ns,shapes

def branch(ns):
    s=ns['Shape']();s.tube([(0,0,0),(.014,.20,0),(-.015,.43,.01),(0,.65,.025)],.037,'Light',8)
    for i in range(5):
        a=i*2.3;y=.17+i*.055;x,z=math.cos(a),math.sin(a)
        mid=(x*.21,y+.11,z*.21);tip=(x*.24,y+.29,z*.24)
        s.tube([(0,y,0),mid,tip],.025,'White',6)
        other=(mid[0]+math.cos(a+1)*.115,tip[1]-.045,mid[2]+math.sin(a+1)*.115)
        s.tube([mid,other],.017,'Light',6)
        for p in [tip,other]:s.sphere(p,(.045,.05,.045),'White',8,4)
    return s

def fan(ns):
    s=ns['Shape']();s.tube([(0,0,0),(0,.20,0)],.026,'Light',8);tips=[];mids=[]
    for i in range(7):
        a=-1.08+i*.36;mid=(math.sin(a)*.23,.21+math.cos(a)*.18,.012*math.sin(i));tip=(math.sin(a)*.46,.25+math.cos(a)*.39,.025*math.sin(i))
        mids.append(mid);tips.append(tip);s.tube([(0,.15,0),mid,tip],.014,'White',6)
        s.sphere(tip,(.032,.036,.032),'Light',8,4)
        if i:s.tube([mids[i-1],((tips[i-1][0]+tip[0])*.5,(tips[i-1][1]+tip[1])*.5,0),mid],.006,'Light',5)
    return s

def table(ns):
    s=ns['Shape']();s.tube([(0,0,0),(0,.28,0)],.045,'Mid',8)
    for center,size in [((0,.31,0),(.94,.09,.78)),((.15,.16,.11),(.47,.065,.43))]:
        start=len(s.vertices);s.sphere(center,size,'White',20,6)
        for j in range(start,len(s.vertices)):
            x,by,y=s.vertices[j];z=-by;angle=math.atan2(z-center[2],x-center[0]);ripple=1+.035*math.sin(angle*9)
            s.vertices[j]=(center[0]+(x-center[0])*ripple,-(center[2]+(z-center[2])*ripple),y)
    for i in range(9):
        a=i*2.399963;r=.16+.12*(i%3)/2;x=math.cos(a)*r;z=math.sin(a)*r*.82
        y=.31+.045*math.sqrt(max(0,1-(x/.47)**2-(z/.39)**2))+.003
        s.sphere((x,y,z),(.024,.013,.024),'Light',6,3)
    return s

def boulder(ns):
    s=ns['Shape']();s.sphere((0,.22,0),(.68,.44,.62),'White',24,12)
    for i,(x,by,y) in enumerate(s.vertices):
        z=-by;a=math.atan2(z,x);height=(y-.22)/.22
        fold=1+.075*math.sin(a*7+height*4)+.035*math.sin(a*13-height*7)
        s.vertices[i]=(x*fold,-z*fold,y)
    return s

def grass(ns):
    s=ns['Shape']()
    for i in range(7):
        angle=math.tau*i/7;d=Vector((math.cos(angle),0,math.sin(angle)));side=Vector((-d.z,0,d.x));v=[];faces=[]
        for back in [False,True]:
            for j in range(7):
                t=j/6;width=max(.00002,.060*math.sin(math.pi*t)**.7);mid=d*(.025+.25*t*t)+Vector((0,t*.78*(1+.09*math.sin(i*2.7)),0))
                for k in [-1,0,1]:v.append(mid+side*width*k+Vector((0,(.008*(1-abs(k))-.002 if back else .008*(1-abs(k))+.002),0)))
        for j in range(6):
            for k in range(2):
                a=j*3+k;faces.extend([(a,a+3,a+4,a+1),(21+a,21+a+1,21+a+4,21+a+3)])
            for k in [0,2]:
                a=j*3+k;faces.append((a,21+a,21+a+3,a+3))
        faces.extend([(0,1,2,23,22,21),(18,39,40,41,20,19)])
        s.add(v,faces,'White')
    return s

def sponge(ns):
    s=ns['Shape']()
    for j in range(3):
        h=.26+j*.09;r=.073+j*.009;p=((j-1)*.12,0,.07*(j%2));segments=12
        profile=[(.72*r,0),(.91*r,.04),(r,h-.06),(1.06*r,h-.018),(r,h),(.69*r,h-.005),(.60*r,h-.07),(.42*r,h-.14),(0,h-.16)]
        vertices=[];faces=[];colors=[]
        for radius,y in profile:
            for k in range(segments):
                a=math.tau*k/segments;vertices.append((p[0]+radius*math.cos(a),y,p[2]+radius*math.sin(a)))
        for row in range(len(profile)-1):
            for k in range(segments):
                a=row*segments+k;b=row*segments+(k+1)%segments;faces.append((a,b,b+segments,a+segments));colors.append(ns['COLOR']['Dark' if row>=5 else 'Mid' if row==4 else 'White'])
        s.add(vertices,faces,'White');s.colors[-len(colors):]=colors
    return s

def star(ns):
    s=ns['Shape']();v=[];faces=[];segments=40
    for ring in range(3):
        for i in range(segments):
            a=math.tau*i/segments;r=(.128+.062*math.cos(5*a))*(1 if ring==1 else .68 if ring==0 else .92)
            v.append((math.cos(a)*r,.020 if ring==1 else .050 if ring==0 else .009,math.sin(a)*r))
    v.append((0,.044,0));v.append((0,.008,0))
    for i in range(segments):
        j=(i+1)%segments;faces.extend([(120,i,j),(i,i+40,j+40,j),(i+40,i+80,j+80,j+40),(121,j+80,i+80)])
    s.add(v,faces,'White')
    for i in range(5):
        a=math.tau*i/5
        s.sphere((math.cos(a)*.105,.041,math.sin(a)*.105),(.015,.009,.015),'Light',6,4)
    return s

def shell(ns):
    s=ns['Shape']();v=[];faces=[];segments=20;rows=5
    for bottom in [False,True]:
        for row in range(rows):
            t=.0001+(1-.0001)*row/(rows-1)
            for i in range(segments+1):
                a=math.pi*i/segments;r=t*(.97+.03*math.cos(a*10));x=math.cos(a)*r;z=-.25*(1-t)+math.sin(a)*r*.9
                y=.015+math.sin(a)*math.sin(t*math.pi*.72)*.105+.006*math.cos(a*10)*math.sin(t*math.pi)
                v.append((x,.010 if bottom else y,z))
    n=rows*(segments+1)
    for row in range(rows-1):
        for i in range(segments):
            a=row*(segments+1)+i;b=a+segments+1;faces.extend([(a,b,b+1,a+1),(n+a,n+a+1,n+b+1,n+b)])
    for i in range(segments):
        a=(rows-1)*(segments+1)+i;faces.append((a,n+a,n+a+1,a+1))
    for k in [0,segments]:
        for row in range(rows-1):
            a=row*(segments+1)+k;b=a+segments+1;faces.append((a,b,n+b,n+a))
    s.add(v,faces,'White');return s

def export_current(repository,preview_directory):
    repo=Path(repository);preview=Path(preview_directory);preview.mkdir(parents=True,exist_ok=True);scene=bpy.data.scenes.get(SCENE)
    if scene is None or not scene.get('magic_island_reef_models'):raise RuntimeError('Open the marked Reef Models scene.')
    parts=[];objects=[]
    for name in NAMES:
        obj=scene.objects.get(name)
        if obj is None or obj.modifiers or any(abs(obj.matrix_local[i][j]-(1 if i==j else 0))>1e-6 for i in range(4) for j in range(4)):raise RuntimeError('Keep original local origin and apply transforms: '+name)
        mesh=obj.data;mesh.calc_loop_triangles();color=mesh.color_attributes['OceanColor'];p={k:[] for k in ['positions','normals','colors','hinges','gait','triangles']};p['name']=name;unique={}
        for tri in mesh.loop_triangles:
            a,b,c=[mesh.vertices[i].co for i in tri.vertices]
            if (b-a).cross(c-a).length_squared<1e-18:continue
            for loop in tri.loops:
                idx=mesh.loops[loop].vertex_index;v=mesh.vertices[idx].co;n=mesh.corner_normals[loop].vector
                col=color.data[loop if color.domain=='CORNER' else idx].color_srgb
                key=(idx,*n,*col)
                if key not in unique:
                    unique[key]=len(p['positions'])//3
                    p['positions'] += [v.x,v.z,-v.y];p['normals'] += [n.x,n.z,-n.y]
                    p['colors'] += [col[0],col[1],col[2],col[3]];p['hinges'] += [0,0,0,0];p['gait'] += [0,0,0,0]
                p['triangles'].append(unique[key])
        parts.append(p);objects.append(obj)
    output=repo/'Assets/Art/Meshes/Ocean/Reef.meshdata.json';output.write_text(json.dumps({'version':1,'coordinateSpace':'Unity local metres','parts':parts},separators=(',',':')),encoding='utf-8')
    source=repo/'SourceArt/OceanReef';source.mkdir(parents=True,exist_ok=True)
    bpy.data.libraries.write(str(source/'OceanReef.blend'),{scene},fake_user=True,compress=True)
    job=subprocess.run([bpy.app.binary_path,'--background','--factory-startup',str(source/'OceanReef.blend'),'--python-exit-code','1','--python-expr','import bpy; bpy.context.preferences.filepaths.save_version=0; bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,compress=True)'],capture_output=True,text=True,timeout=60)
    if job.returncode:raise RuntimeError(job.stderr[-1500:])
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True);obj.parent.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(source/'OceanReef.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,bake_anim=False,use_custom_props=True)
    summary={p['name']:len(p['triangles'])//3 for p in parts};(preview/'ModelSummary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8');print(json.dumps(summary))

def build(repository,preview_directory):
    repo=Path(repository);preview=Path(preview_directory);preview.mkdir(parents=True,exist_ok=True);helpers,ns=support(repo)
    limits=json.loads((repo/'Assets/Art/Meshes/Ocean/ReefLayout.json').read_text())['sources'];old=bpy.data.scenes.get(SCENE)
    if old:
        if not old.get('magic_island_reef_models'):raise RuntimeError('Unrelated scene uses the reef source name.')
        for obj in list(old.objects):bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.scenes.remove(old)
    scene=bpy.data.scenes.new(SCENE);scene['magic_island_reef_models']=1;bpy.context.window.scene=scene
    collection=bpy.data.collections.new('Reef Models - Export');scene.collection.children.link(collection)
    mat=bpy.data.materials.new('Reef Models - Vertex Colours');mat.use_nodes=True;nodes=mat.node_tree.nodes;shader=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');shader.inputs['Roughness'].default_value=.77
    attr=nodes.new('ShaderNodeVertexColor');attr.layer_name='OceanColor';info=nodes.new('ShaderNodeObjectInfo');mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
    mat.node_tree.links.new(attr.outputs['Color'],mix.inputs[1]);mat.node_tree.links.new(info.outputs['Color'],mix.inputs[2]);mat.node_tree.links.new(mix.outputs[0],shader.inputs['Base Color'])
    colors=[(.80,.48,.50,1),(.61,.48,.70,1),(.76,.60,.44,1),(.47,.66,.61,1),(.36,.57,.45,1),(.80,.65,.41,1),(.84,.56,.43,1),(.87,.79,.63,1)]
    for i,(name,func) in enumerate(zip(NAMES,[branch,fan,table,boulder,grass,sponge,star,shell])):
        s=func(ns);limit=limits[i];radius=max(math.hypot(v[0],v[1]) for v in s.vertices);horizontal=min(1,limit['radius']/radius*.997);bottom=min(v[2] for v in s.vertices);top=max(v[2] for v in s.vertices)
        s.vertices=[(x*horizontal,y*horizontal,limit['bottom']+(z-bottom)/(top-bottom)*(limit['top']-limit['bottom'])) for x,y,z in s.vertices]
        obj=helpers['make'](s,name,collection,mat);obj.color=colors[i]
        if i==4:
            attrcolor=obj.data.color_attributes['OceanColor'];height=limit['top']-limit['bottom']
            for loop in obj.data.loops:
                col=attrcolor.data[loop.index].color_srgb;alpha=(obj.data.vertices[loop.vertex_index].co.z-limit['bottom'])/height;attrcolor.data[loop.index].color_srgb=(col[0],col[1],col[2],alpha)
        else:
            for col in obj.data.color_attributes['OceanColor'].data:
                c=col.color_srgb;col.color_srgb=(c[0],c[1],c[2],0)
        root=bpy.data.objects.new(name+' Display',None);collection.objects.link(root);root.location=((i%4-1.5)*1.18,.70 if i<4 else -.60,.053-limit['bottom']);obj.parent=root;root['unity_mesh']=name
        if name=='Clam Shell':root.scale=(.55,.55,.55)
        if name=='Sea Star':root.scale=(1.8,1.8,1.8);root.location.z=.053-limit['bottom']*1.8
    studio=bpy.data.collections.new('Reef Models - Preview Only');scene.collection.children.link(studio);ns['stage'](scene,studio,preview)
    scene.camera.location=(2.6,-6.4,4.8);scene.camera.rotation_euler=(Vector((0,.05,.27))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.ortho_scale=5.6
    scene.render.resolution_x=1700;scene.render.resolution_y=1100;scene.render.filepath=str(preview/'OceanReefStudio.png')
    export_current(str(repo),str(preview));bpy.ops.render.render(write_still=True)
