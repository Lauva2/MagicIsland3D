"""Validate actual reef geometry, original safety envelopes, layout and whole-reef budget."""
from pathlib import Path
import json,math
ROOT=Path(__file__).resolve().parents[1]
def dot(a,b):return sum(x*y for x,y in zip(a,b))
def sub(a,b):return [x-y for x,y in zip(a,b)]
def cross(a,b):return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
def run():
    d=json.loads((ROOT/'Assets/Art/Meshes/Ocean/Reef.meshdata.json').read_text());layout=json.loads((ROOT/'Assets/Art/Meshes/Ocean/ReefLayout.json').read_text())
    assert d['version']==layout['version']==1 and d['coordinateSpace']=='Unity local metres'
    assert [p['name'] for p in d['parts']]==[s['name'] for s in layout['sources'][:8]]
    assert len(layout['groups'])==84 and sum(len(g['members']) for g in layout['groups'])==2744
    for i,p in enumerate(d['parts']):
        v=p['positions'];n=p['normals'];c=p['colors'];idx=p['triangles'];count=len(v)//3;limit=layout['sources'][i]
        assert 2<count<65536 and len(v)==len(n)==count*3 and len(c)==count*4
        assert all(math.isfinite(f) for f in v+n+c) and all(0<=f<=1 for f in c)
        assert len(idx)%3==0 and all(0<=j<count for j in idx)
        assert all(abs(dot(n[j:j+3],n[j:j+3])-1)<.001 for j in range(0,len(n),3))
        for j in range(0,len(idx),3):
            a,b,e=[v[k*3:k*3+3] for k in idx[j:j+3]];normal=cross(sub(b,a),sub(e,a))
            assert dot(normal,normal)>1e-18,(p['name'],'degenerate')
            assert dot(normal,[sum(n[k*3+t] for k in idx[j:j+3]) for t in range(3)])>=-1e-12,(p['name'],'winding')
        assert max(math.hypot(v[j],v[j+2]) for j in range(0,len(v),3))<=limit['radius']+1e-6
        assert min(v[1::3])>=limit['bottom']-1e-6 and max(v[1::3])<=limit['top']+1e-6
        if i==4:assert min(c[3::4])<.001 and max(c[3::4])>.999
        else:assert all(a==0 for a in c[3::4])
        assert not any(p['hinges']) and not any(p['gait'])
    counts=[len(p['triangles'])//3 for p in d['parts']]+[layout['sources'][8]['triangles']]
    visible=[m for g in layout['groups'] for m in g['members'] if not m.get('hidden',False)]
    assert visible and all(any(not m.get('hidden',False) for m in g['members']) for g in layout['groups'])
    before=sum(layout['sources'][m['source']]['triangles'] for m in visible)
    after=sum(counts[m['source']] for m in visible)
    assert after<before*1.25
    print(f'PASS: eight source models, 84 habitats / 2744 recorded members / {len(visible)} visible; geometry, normals, original envelopes and grass weights. Visible combined triangles {before:,} -> {after:,} ({(after/before-1)*100:.2f}%).')
if __name__=='__main__':run()
