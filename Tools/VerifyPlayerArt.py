"""Check baked art geometry and its binding contract without launching Unity."""
from pathlib import Path
import json
import math
import re
import struct

ROOT = Path(__file__).resolve().parent.parent
JOINTS = {'Bob', 'Bob/Head', 'Bob/LeftArm', 'Bob/RightArm', 'Bob/LeftLeg', 'Bob/RightLeg'}

def require(condition, message):
    if not condition:
        raise AssertionError(message)

def vector(values, index):
    return values[index*3:index*3+3]

def subtract(a,b):
    return [a[i]-b[i] for i in range(3)]

def cross(a,b):
    return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]

def dot(a,b):
    return sum(x*y for x,y in zip(a,b))

def run():
    data = json.loads((ROOT/'Assets/Art/Meshes/Player/IslandFarmer.meshdata.json').read_text(encoding='utf-8'))
    require(data['version'] == 1 and data['coordinateSpace'] == 'Unity local metres', 'Wrong coordinate/version contract')
    require(len(data['parts']) == 6 and {p['joint'] for p in data['parts']} == JOINTS, 'Missing or duplicated joint mesh')
    triangles, vertices = 0,0
    for part in data['parts']:
        p,n,uv,index = (part[k] for k in ('positions','normals','uv','triangles'))
        count = len(p)//3
        require(0 < count < 65536 and len(p) == count*3 and len(n) == count*3 and len(uv) == count*2, 'Invalid stream sizes')
        require(all(math.isfinite(v) for v in [*p,*n,*uv]), 'Non-finite geometry')
        require(all(abs(dot(vector(n,i),vector(n,i))-1) < .001 for i in range(count)), 'Non-unit normal')
        require(len(index)%3 == 0 and all(0 <= i < count for i in index), 'Invalid triangle indices')
        require(all(0 <= u <= 1 for u in uv), 'Palette UV outside atlas')
        backwards, degenerate = 0,0
        for i in range(0,len(index),3):
            a,b,c = (vector(p,index[i+j]) for j in range(3))
            normal = cross(subtract(b,a),subtract(c,a))
            degenerate += dot(normal,normal) < 1e-18
            shading = [sum(vector(n,index[i+j])[k] for j in range(3)) for k in range(3)]
            backwards += dot(normal,shading) < -1e-12
            require(uv[index[i]*2:index[i]*2+2] == uv[index[i+1]*2:index[i+1]*2+2]
                    == uv[index[i+2]*2:index[i+2]*2+2], 'A triangle crosses palette swatches')
        require(degenerate == 0, part['name']+': degenerate triangles')
        require(backwards == 0, part['name']+': winding disagrees with shading normals')
        triangles += len(index)//3
        vertices += count
    require(triangles < 20000, 'First art revision exceeded its reviewed geometry budget')
    source = ROOT/'SourceArt/Characters'
    require((source/'IslandFarmer.blend').stat().st_size > 10000, 'Missing editable Blender source')
    require((source/'IslandFarmer.fbx').read_bytes().startswith(b'Kaydara FBX Binary'), 'Invalid FBX output')
    palette = (ROOT/'Assets/Art/Textures/Player/IslandFarmerPalette.png').read_bytes()
    require(palette.startswith(b'\x89PNG\r\n\x1a\n') and struct.unpack('>II',palette[16:24]) == (128,64), 'Wrong palette atlas')
    prefab = (ROOT/'Assets/Art/Prefabs/Characters/PlayerCharacter.prefab').read_text(encoding='utf-8')
    require('m_Name: ToolSocket' in prefab, 'Missing original socket')
    require(len(re.findall(r'^CharacterController:$',prefab,re.M)) == 1
            and len(re.findall(r'^Animator:$',prefab,re.M)) == 1,
            'Unexpected original controller/animator serialization')
    for state in ('Idle','Walk','Hoe','Plant','Water','Axe','Pickaxe','Harvest','Fertilize'):
        clip = (ROOT/'Assets/Art/Animations/Player'/f'{state}.anim').read_text(encoding='utf-8')
        require('m_Events: []' in clip, state+': animation must not perform gameplay settlement')
        require(all('path: '+p in clip for p in JOINTS), state+': source animation does not bind the expected joints')
    # Every newly introduced Unity asset must retain its checked-in GUID.
    for path in [ROOT/'Assets/Editor/PlayerCharacterArtSetup.cs',
                 ROOT/'Assets/Art/Meshes/Player/IslandFarmer.meshdata.json',
                 ROOT/'Assets/Art/Textures/Player/IslandFarmerPalette.png']:
        meta = Path(str(path)+'.meta')
        require(meta.exists() and re.search(r'^guid: [0-9a-f]{32}$',meta.read_text(),re.M), 'Missing asset GUID: '+str(path))
    print(f'PASS: {triangles:,} triangles, {vertices:,} vertices, six joint meshes; finite/unit geometry, '
          'winding, nondegenerate triangles, palette UVs, source artifacts, animation paths and Unity GUIDs.')

if __name__ == '__main__':
    run()
