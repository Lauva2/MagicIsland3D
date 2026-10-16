"""Explicit Blender source for four fish species, gull body/wings and the articulated crab.

Only the marked Ocean Wildlife scene is replaced. Existing scenes are retained.
Unity colours are sRGB vertex colours; UV1/UV2 carry the original crab joint contract.
"""
from pathlib import Path
import json, math, subprocess
import bpy
from mathutils import Vector, Matrix

SCENE = 'MagicIsland - Ocean Wildlife'
NAMES = ['SilverMinnow', 'Clownfish', 'Butterflyfish', 'SpottedRay',
         'Seagull Body', 'Seagull Left Wing', 'Seagull Right Wing', 'Shore Crab']
PALETTE = [('Ivory','EFEAD7'), ('White','FAF6E8'), ('Ink','263D45'), ('Gold','DFA64C'),
           ('Silver','91B9BD'), ('Blue','5E919F'), ('Orange','D98B53'), ('OrangeDark','AC6841'),
           ('Yellow','D9BC65'), ('YellowLight','F0DB91'), ('Ray','647F87'), ('RayLight','B4C9C4'),
           ('Grey','A2B6B9'), ('Feather','718D98'), ('Red','C97453'), ('Shell','D99069'),
           ('ShellLight','E8AD7E'), ('Sand','E8D5AF')]

def support(repo):
    ns={'__name__':'ocean_shapes'}
    path=repo/'Tools/Art/BuildIslandFarmer.py'
    exec(compile(path.read_text(encoding='utf-8'),str(path),'exec'),ns)
    ns['COLOR']={name:i for i,(name,_) in enumerate(PALETTE)}
    ns['COLOR']['Cream']=0
    return ns

def prism(s, points, width, color, axis=0):
    # Closed thin fins, wings and beak: the broad profile is in the other two axes.
    axes=[i for i in range(3) if i!=axis]; vertices=[]
    for sign in [-1,1]:
        for a,b in points:
            p=[0.,0.,0.];p[axis]=sign*width/2;p[axes[0]]=a;p[axes[1]]=b;vertices.append(p)
    n=len(points);faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    s.add(vertices,faces,color)

def eyes(s, positions, size=.028):
    for p in positions:
        s.sphere(p,(size,size,size),'Ink',10,6)
        s.sphere((p[0]+size*.17,p[1]+size*.20,p[2]+size*.30),
                 (size*.22,size*.22,size*.22),'White',8,4)

def fish(ns, species):
    s=ns['Shape']();length=.25 if species==0 else .31
    width=.075 if species==2 else .065;height=[.055,.10,.16][species]
    base=['Silver','Orange','Yellow'][species]
    # Rounded spindle with real geometric bands, rather than a texture or extra renderer.
    rings=[-1,-.88,-.70,-.59,-.40,-.20,0,.24,.35,.50,.65,.82,1]
    vertices=[];faces=[];colors=[];segments=12
    for u in rings:
        for k in range(segments):
            angle=math.tau*k/segments;r=math.sqrt(max(0,1-u*u))
            vertices.append((width*r*math.cos(angle),height*r*math.sin(angle),length*u))
    for j in range(len(rings)-1):
        u=(rings[j]+rings[j+1])/2
        for k in range(segments):
            a=j*segments+k;b=j*segments+(k+1)%segments
            if j==0: f=(a,b,b+segments)
            elif j==len(rings)-2: f=(a,b,b+segments)
            else: f=(a,b,b+segments,a+segments)
            # Remove collapsed pole edges, retaining a single triangle per segment.
            if j==0: f=(a,b+segments,a+segments)
            color=base
            if species==1:
                if -.70<u<-.40 or .35<u<.65:color='Ivory'
                elif -.88<u<-.70 or -.40<u<-.20 or .24<u<.35 or .65<u<.82:color='OrangeDark'
            if species==2 and .35<u<.65:color='Ink'
            if species==0 and k<6:color='Blue'
            faces.append(f);colors.append(ns['COLOR'][color])
    s.add(vertices,faces,base)
    s.colors[-len(colors):]=colors
    # Tail tips keep the original full fish radius; body and dorsal keep the original height.
    tail=[(-width*1.7,-length*1.6),(-width*.35,-length*1.23),(0,-length*.81),
          (width*.35,-length*1.23),(width*1.7,-length*1.6),(0,-length*1.40)]
    prism(s,tail,.012,'Blue' if species==0 else 'OrangeDark' if species==1 else 'Yellow',axis=1)
    prism(s,[(height*.60,.12),(height*1.7,-.02),(height*.65,-.18)],.012,
          'Ivory' if species==0 else 'OrangeDark' if species==1 else 'YellowLight')
    for side in [-1,1]:
        # Pectorals are slightly swept back, inside the previous lateral envelope.
        s.add([(side*width*.7,0,.04),(side*width*2.05,-.02,-.12),
               (side*width*.7,-.025,-.07)],[(0,1,2)],'Ivory' if species==0 else base)
    eyes(s,[(side*width*.75,height*.30,length*.66) for side in [-1,1]],.027)
    return s

def ray(ns, reference):
    s=ns['Shape']()
    s.sphere((0,0,0),(.38,.11,.60),'Ray',16,8)
    # Two sculpted pectoral discs with a raised central ridge and rounded leading edge.
    for side in [-1,1]:
        outline=[(.085,.25),(.24,.22),(.40,.12),(.53,-.02),(.41,-.13),(.25,-.23),(.10,-.28)]
        ridge=(side*.20,.038,-.005)
        for i in range(len(outline)-1):
            a=(side*outline[i][0],.012,outline[i][1]);b=(side*outline[i+1][0],.012,outline[i+1][1])
            s.add([ridge,a,b],[(0,1,2)],'Ray')
            s.add([(ridge[0],-.018,ridge[2]),a,b],[(0,2,1)],'RayLight')
        for j in range(5):
            x=.14+j*.061;z=.10-j*.048
            s.sphere((side*x,.028,z),(.034,.008,.039),'Ivory',8,4)
        s.sphere((side*.083,.047,.15),(.040,.026,.045),'RayLight',10,6)
    eyes(s,[(side*.083,reference['parts'][3]['top']-.0115,.15) for side in [-1,1]],.023)
    # Original tapered tail endpoint retains the exact baked horizontal safety envelope.
    tail=reference['parts'][3];v=tail['positions'];idx=tail['triangles'];start=len(v)-54
    for i in range(0,len(idx),3):
        face=idx[i:i+3]
        if min(face)>=start:
            s.add([(v[j]['x'],v[j]['y'],v[j]['z']) for j in face],[(0,1,2)],'Ray')
    return s

def gull(ns):
    s=ns['Shape']()
    s.sphere((0,.25,0),(.216,.25,.52),'Ivory',20,10)
    s.sphere((0,.265,-.06),(.208,.19,.38),'Grey',16,8)
    s.sphere((0,.35,.175),(.138,.18,.18),'White',16,8)
    s.sphere((0,.40,.23),(.18,.19,.22),'White',16,8)
    prism(s,[(.373,.307),(.38,.476),(.404,.434),(.418,.305)],.069,'Gold')
    s.tube([(-.029,.381,.32),(0,.382,.463),(.029,.381,.32)],.002,'OrangeDark',5)
    eyes(s,[(side*.078,.42,.27) for side in [-1,1]],.029)
    for side in [-1,1]:
        s.tube([(side*.05,.17,-.03),(side*.05,.027,-.02)],.010,'Gold',8)
        for toe in [-1,0,1]:
            s.tube([(side*.05,.025,-.02),(side*.05+toe*.025,.015,.066)],.008,'Gold',7)
        # Tail feather tips extend back from the same body anchor.
        for i in range(3):
            x=side*(.023+i*.032)
            s.sphere((x,.246,-.255-i*.007),(.046,.026,.245),'Ivory',10,6)
    return s

def wing(ns, side):
    s=ns['Shape']()
    # Existing wing roots remain at +/-0.07, 0.27, 0 in Unity.
    points=[(0,.11),(.25,.105),(.43,.027),(.66,-.13),(.78,-.21),(.40,-.19),(0,-.16)]
    prism(s,[(side*x,z) for x,z in points],.016,'Grey',axis=1)
    for i in range(5):
        x=.36+i*.073;z=-.09-i*.023
        s.sphere((side*x,-.006,z),(.13,.022,.17),'Feather' if i<2 else 'Ink',10,6,
                 Matrix.Rotation(math.radians(side*-33),3,'Y'))
    for i in range(3):
        s.sphere((side*(.16+i*.065),.011,-.06),(.09,.018,.18),'Ivory',10,6)
    return s

def crab(ns):
    s=ns['Shape']();spans=[]
    def marked(start,hinge,phase=0):spans.append((start,len(s.vertices),hinge,phase))
    s.sphere((0,.103,0),(.34,.15,.24),'Shell',20,10)
    s.sphere((0,.118,-.006),(.278,.131,.19),'Red',16,8)
    for side in [-1,1]:
        for i in range(4):
            x=side*(.055+i*.024);z=-.025+(i%2)*.038
            s.sphere((x,.165-abs(x)*.13,z),(.023,.012,.023),'ShellLight',8,4)
        for i in range(4):
            a=(side*.12,.10,.075-i*.045);b=(side*(.24+i*.013),.08,.12-i*.085);c=(side*.29,.018,.14-i*.09)
            start=len(s.vertices)
            s.tube([a,b],.014,'Red',8);s.tube([b,c],.011,'ShellLight',8)
            s.sphere(b,(.035,.029,.036),'Shell',10,6)
            marked(start,(*a,100+i),(i+(1 if side>0 else 0))*math.pi)
        pivot=(side*.12,.11,.07);start=len(s.vertices)
        p=(side*.217,.145,.175)
        s.tube([pivot,(side*.19,.12,.12),p],.021,'Red',8)
        s.sphere(p,(.079,.061,.078),'ShellLight',12,6)
        # Rounded pincer palm with two separated hooked fingers.
        s.sphere((side*.232,.154,.213),(.091,.064,.075),'Shell',12,6)
        s.tube([(side*.258,.151,.224),(side*.271,.166,.258),(side*.25,.167,.274)],.014,'ShellLight',8)
        s.tube([(side*.212,.157,.228),(side*.217,.169,.258),(side*.237,.169,.273)],.013,'Ivory',8)
        marked(start,(*pivot,110 if side<0 else 111))
        s.tube([(side*.065,.15,.06),(side*.065,.215,.085)],.010,'Red',8)
    eyes(s,[(side*.065,.22,.085) for side in [-1,1]],.039)
    return s,spans

def make(s,name,collection,material,spans=()):
    obj=s.make(name,None,collection,material);mesh=obj.data
    colors=mesh.color_attributes.new(name='OceanColor',type='FLOAT_COLOR',domain='CORNER')
    for polygon in mesh.polygons:
        uv=mesh.uv_layers[0].data[polygon.loop_start].uv
        index=min(len(PALETTE)-1,int(uv.y*4)*8+int(uv.x*8));rgb=tuple(int(PALETTE[index][1][i:i+2],16)/255 for i in [0,2,4])
        for loop in polygon.loop_indices:colors.data[loop].color_srgb=(*rgb,1)
    mesh.uv_layers.remove(mesh.uv_layers[0])
    hinge=mesh.attributes.new(name='hinge',type='FLOAT_VECTOR',domain='POINT')
    tag=mesh.attributes.new(name='joint_tag',type='FLOAT',domain='POINT')
    gait=mesh.attributes.new(name='leg_phase',type='FLOAT',domain='POINT')
    for start,end,pivot,phase in spans:
        for i in range(start,end):hinge.data[i].vector=pivot[:3];tag.data[i].value=pivot[3];gait.data[i].value=phase
    mesh.set_sharp_from_angle(angle=math.radians(52))
    return obj

def export_current(repository,preview_directory):
    repo=Path(repository);preview=Path(preview_directory);preview.mkdir(parents=True,exist_ok=True)
    scene=bpy.data.scenes.get(SCENE)
    if scene is None or not scene.get('magic_island_ocean_wildlife'):raise RuntimeError('Open the marked Ocean Wildlife scene.')
    parts=[];objects=[]
    for name in NAMES:
        obj=scene.objects.get(name)
        if obj is None or obj.modifiers or any(abs(obj.matrix_local[i][j]-(1 if i==j else 0))>1e-6 for i in range(4) for j in range(4)):
            raise RuntimeError('Keep export meshes at their original local origin and apply modifiers: '+name)
        mesh=obj.data;mesh.calc_loop_triangles();colors=mesh.color_attributes['OceanColor']
        part={'name':name,'positions':[],'normals':[],'colors':[],'hinges':[],'gait':[],'triangles':[]}
        for triangle in mesh.loop_triangles:
            a,b,c=[mesh.vertices[i].co for i in triangle.vertices]
            if (b-a).cross(c-a).length_squared<1e-18:continue
            for loop in triangle.loops:
                idx=mesh.loops[loop].vertex_index;v=mesh.vertices[idx].co;n=mesh.corner_normals[loop].vector
                part['positions'] += [v.x,v.z,-v.y];part['normals'] += [n.x,n.z,-n.y]
                col=colors.data[loop if colors.domain=='CORNER' else idx].color_srgb
                part['colors'] += [col[0],col[1],col[2],0]
                h=mesh.attributes['hinge'].data[idx].vector
                part['hinges'] += [*h,mesh.attributes['joint_tag'].data[idx].value]
                part['gait'] += [mesh.attributes['leg_phase'].data[idx].value,0,0,0]
                part['triangles'].append(len(part['triangles']))
        parts.append(part);objects.append(obj)
    output=repo/'Assets/Art/Meshes/Ocean/Wildlife.meshdata.json'
    output.parent.mkdir(parents=True,exist_ok=True)
    output.write_text(json.dumps({'version':1,'coordinateSpace':'Unity local metres','parts':parts},separators=(',',':')),encoding='utf-8')
    source=repo/'SourceArt/Ocean';source.mkdir(parents=True,exist_ok=True)
    bpy.data.libraries.write(str(source/'OceanWildlife.blend'),{scene},fake_user=True,compress=True)
    result=subprocess.run([bpy.app.binary_path,'--background','--factory-startup',str(source/'OceanWildlife.blend'),
        '--python-exit-code','1','--python-expr','import bpy; bpy.context.preferences.filepaths.save_version=0; bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,compress=True)'],capture_output=True,text=True,timeout=60)
    if result.returncode:raise RuntimeError(result.stderr[-1500:])
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True);obj.parent.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(source/'OceanWildlife.fbx'),use_selection=True,object_types={'MESH','EMPTY'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,bake_anim=False,use_custom_props=True)
    summary={p['name']:len(p['triangles'])//3 for p in parts}
    (preview/'ModelSummary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8');print(json.dumps(summary))

def build(repository,preview_directory):
    repo=Path(repository);preview=Path(preview_directory);preview.mkdir(parents=True,exist_ok=True)
    reference=json.loads((repo/'SourceArt/Ocean/ReferenceEnvelopes.json').read_text(encoding='utf-8'))
    ns=support(repo);old=bpy.data.scenes.get(SCENE)
    if old:
        if not old.get('magic_island_ocean_wildlife'):raise RuntimeError('Unrelated scene has the art scene name.')
        for obj in list(old.objects):bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.scenes.remove(old)
    scene=bpy.data.scenes.new(SCENE);scene['magic_island_ocean_wildlife']=1;bpy.context.window.scene=scene
    collection=bpy.data.collections.new('Ocean Wildlife - Export');scene.collection.children.link(collection)
    mat=bpy.data.materials.new('Ocean Wildlife - Vertex Colours');mat.use_nodes=True
    shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');shader.inputs['Roughness'].default_value=.72
    attribute=mat.node_tree.nodes.new('ShaderNodeVertexColor');attribute.layer_name='OceanColor'
    mat.node_tree.links.new(attribute.outputs['Color'],shader.inputs['Base Color'])
    shapes=[fish(ns,i) for i in range(3)]+[ray(ns,reference),gull(ns),wing(ns,-1),wing(ns,1)]
    crab_shape,spans=crab(ns);shapes.append(crab_shape)
    positions=[(-1.20,-.40,.55),(-.48,-.40,.55),(.22,-.40,.55),(1.10,-.25,.50),(-.65,1.05,.06),(-.65,1.05,.06),(-.65,1.05,.06),(.92,1.15,.06)]
    for i,(name,s,location) in enumerate(zip(NAMES,shapes,positions)):
        if i<4:
            limit=reference['parts'][i]
            s.vertices=[(x,y,min(limit['top'],max(limit['bottom'],z))) for x,y,z in s.vertices]
        obj=make(s,name,collection,mat,spans if name=='Shore Crab' else ())
        root=bpy.data.objects.new(name+' Display',None);collection.objects.link(root);root.location=location
        obj.parent=root;root['unity_mesh']=name
        if 'Wing' in name:root.location+=Vector((-.07 if 'Left' in name else .07,0,.27))
    preview_only=bpy.data.collections.new('Ocean Wildlife - Preview Only');scene.collection.children.link(preview_only)
    ns['stage'](scene,preview_only,preview)
    scene.camera.location=(2,-5,3.3);scene.camera.rotation_euler=(Vector((0,.1,.4))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
    scene.camera.data.ortho_scale=3.8;scene.render.resolution_x=1600;scene.render.resolution_y=1100
    scene.render.filepath=str(preview/'OceanWildlifeStudio.png')
    export_current(str(repo),str(preview));bpy.ops.render.render(write_still=True)
