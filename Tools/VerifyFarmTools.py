"""Validate the actual Blender-exported farm-tool geometry and source artifacts."""
from pathlib import Path
import json
import math
import struct
import re

ROOT = Path(__file__).resolve().parents[1]


def dot(a, b): return sum(x*y for x, y in zip(a, b))
def sub(a, b): return [x-y for x, y in zip(a, b)]
def cross(a, b): return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]


def run():
    data = json.loads((ROOT/'Assets/Art/Meshes/Tools/FarmTools.meshdata.json').read_text(encoding='utf-8'))
    assert data['version'] == 1 and data['coordinateSpace'] == 'Unity local metres'
    assert [(p['name'], p['joint']) for p in data['parts']] == [('IslandHoe', 'Hoe'), ('IslandWateringCan', 'Water'), ('IslandAxe', 'Axe'), ('IslandPickaxe', 'Pickaxe')]
    total = 0
    for part in data['parts']:
        positions, normals, uv, indices = (part[k] for k in ['positions', 'normals', 'uv', 'triangles'])
        count = len(positions)//3
        assert 2 < count < 65536 and len(positions) == len(normals) == count*3 and len(uv) == count*2
        assert all(math.isfinite(v) for v in positions+normals+uv) and all(0 <= v <= 1 for v in uv)
        assert len(indices) % 3 == 0 and all(0 <= i < count for i in indices)
        assert all(abs(dot(normals[i:i+3], normals[i:i+3])-1) < .001 for i in range(0, len(normals), 3))
        for i in range(0, len(indices), 3):
            a, b, c = [positions[index*3:index*3+3] for index in indices[i:i+3]]
            face = cross(sub(b, a), sub(c, a))
            normal = [sum(normals[index*3+j] for index in indices[i:i+3]) for j in range(3)]
            assert dot(face, face) > 1e-18, (part['name'], 'degenerate triangle', i)
            assert dot(face, normal) >= -1e-12, (part['name'], 'backwards triangle', i)
            assert len({tuple(uv[index*2:index*2+2]) for index in indices[i:i+3]}) == 1, 'Triangle crosses palette swatches'
        total += len(indices)//3
        assert max(abs(v) for v in positions) < .8, 'Tool no longer fits its reviewed hand envelope'
    assert total == 9396
    png = (ROOT/'Assets/Art/Textures/Tools/FarmToolsPalette.png').read_bytes()
    assert png.startswith(b'\x89PNG\r\n\x1a\n') and struct.unpack('>II', png[16:24]) == (128, 64)
    assert (ROOT/'SourceArt/Tools/FarmTools.blend').stat().st_size > 10000
    assert (ROOT/'SourceArt/Tools/FarmTools.fbx').read_bytes().startswith(b'Kaydara FBX Binary')
    for relative in ['Assets/Scripts/Tools/FarmToolModels.cs', 'Assets/Editor/FarmToolArtSetup.cs',
                     'Assets/Editor/FarmToolArtVerification.cs', 'Assets/Art/Meshes/Tools/FarmTools.meshdata.json',
                     'Assets/Art/Textures/Tools/FarmToolsPalette.png']:
        meta = ROOT/(relative+'.meta')
        assert meta.exists() and re.search(r'^guid: [0-9a-f]{32}$', meta.read_text(), re.M), ('Invalid asset GUID', relative)
    print(f'PASS: {total:,} triangles in four tools; finite geometry, unit normals, winding, nondegenerate triangles, palette UVs, grip bounds and editable source files.')


if __name__ == '__main__': run()
