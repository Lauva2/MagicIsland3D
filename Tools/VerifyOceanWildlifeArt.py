"""Check authored ocean animal geometry, colour data, safety envelopes and crab joint metadata."""
from pathlib import Path
import json,math
ROOT=Path(__file__).resolve().parents[1]
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def sub(a,b):return [x-y for x,y in zip(a,b)]
def cross(a,b):return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
def run():
    data=json.loads((ROOT/'Assets/Art/Meshes/Ocean/Wildlife.meshdata.json').read_text())
    limits=json.loads((ROOT/'SourceArt/Ocean/ReferenceEnvelopes.json').read_text())['parts']
    names=['SilverMinnow','Clownfish','Butterflyfish','SpottedRay','Seagull Body','Seagull Left Wing','Seagull Right Wing','Shore Crab']
    assert data['version']==1 and data['coordinateSpace']=='Unity local metres'
    assert [p['name'] for p in data['parts']]==names
    for i,p in enumerate(data['parts']):
        v=p['positions'];n=p['normals'];c=p['colors'];h=p['hinges'];g=p['gait'];idx=p['triangles'];count=len(v)//3
        assert 2<count<65536 and len(v)==len(n)==count*3 and len(c)==len(h)==len(g)==count*4
        assert all(math.isfinite(f) for f in v+n+c+h+g) and all(0<=f<=1 for f in c)
        assert all(c[j]==0 for j in range(3,len(c),4))
        assert len(idx)%3==0 and all(0<=j<count for j in idx)
        assert all(abs(dot(n[j:j+3],n[j:j+3])-1)<.001 for j in range(0,len(n),3))
        for j in range(0,len(idx),3):
            a,b,d=[v[k*3:k*3+3] for k in idx[j:j+3]];normal=cross(sub(b,a),sub(d,a))
            assert dot(normal,normal)>1e-18,(p['name'],'degenerate')
            assert dot(normal,[sum(n[k*3+t] for k in idx[j:j+3]) for t in range(3)])>=-1e-12,(p['name'],'winding')
        if i<4:
            limit=limits[i]
            assert max(math.hypot(v[j],v[j+2]) for j in range(0,len(v),3))<=limit['radius']+1e-6
            assert min(v[1::3])>=limit['bottom']-1e-6 and max(v[1::3])<=limit['top']+1e-6
        if i==7:
            assert set(h[3::4])=={0,100,101,102,103,110,111}
            for j in range(count):
                if h[j*4+3]>=100:assert abs(h[j*4])<=.121 and .099<=h[j*4+1]<=.111
    assert (ROOT/'SourceArt/Ocean/OceanWildlife.blend').stat().st_size>10000
    assert (ROOT/'SourceArt/Ocean/OceanWildlife.fbx').stat().st_size>10000
    print('PASS: eight animal meshes; valid geometry, normals, colours, original fish envelopes and crab leg/claw metadata.')
if __name__=='__main__':run()
