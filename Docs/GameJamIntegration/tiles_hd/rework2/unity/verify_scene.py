"""Independent serialized-scene comparison; no Unity builder predicates or YAML mutation."""
import json,re,hashlib
from pathlib import Path
root=Path(__file__).resolve().parents[5]
out=Path(__file__).parent
base=out/'baseline'
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def blocks(p):
 s=p.read_text(encoding='utf-8-sig')
 return {m.group(2):(int(m.group(1)),m.group(0)) for m in re.finditer(r'^--- !u!(\d+) &(\d+).*?(?=^--- !u!|\Z)',s,re.M|re.S)}
b=blocks(base/'BiomechanicalCombatScene.unity');a=blocks(root/'Assets/Scenes/BiomechanicalCombatScene.unity')
def decor_ids(data):
 gos={i for i,(t,s) in data.items() if t==1 and re.search(r'^  m_Name: (?:TilesHD_Decor|PathLamp_|DoorLamp_|EdgePipe_)',s,re.M)}
 return gos|{i for i,(t,s) in data.items() if (m:=re.search(r'm_GameObject: \{fileID: (\d+)\}',s)) and m[1] in gos}
allowed_decor=decor_ids(b)|decor_ids(a)
issues=[];changes=[]
for i,(t,s) in b.items():
 if i in allowed_decor:continue
 if i not in a:issues.append('deleted protected '+i);continue
 ns=a[i][1]
 if s==ns:continue
 if t==1839735485:changes.append({'id':i,'type':t,'authorized':'Tilemap cells'});continue
 issues.append('changed protected block '+i+' type '+str(t))
for i in a.keys()-b.keys():
 if i not in allowed_decor:issues.append('unexpected new record '+i)
old=json.loads((out/'tilemap-before.json').read_text());new=json.loads((out/'tilemap-after.json').read_text())
def positions(m):return {tuple(c['position'][k] for k in ('x','y','z')) for c in m['cells']}
geometry=[]
for bm in old['maps']:
 am=next(m for m in new['maps'] if m['path']==bm['path']);bp=positions(bm);ap=positions(am)
 added=ap-bp;removed=bp-ap;expected=set()
 passed=added==expected and not removed and bm['origin']==am['origin'] and bm['size']==am['size']
 # Existing per-cell transforms, color and flags must not change.
 ac={tuple(c['position'][k] for k in ('x','y','z')):c for c in am['cells']}
 for c in bm['cells']:
  n=ac[tuple(c['position'][k] for k in ('x','y','z'))]
  if any(c[k]!=n[k] for k in ('color','matrix','flags')):passed=False
 geometry.append(dict(path=bm['path'],before=len(bp),after=len(ap),added=sorted(added),removed=sorted(removed),origin_unchanged=bm['origin']==am['origin'],size_unchanged=bm['size']==am['size'],passed=passed))
 if not passed:issues.append('geometry contract failed '+bm['path'])
result=dict(passed=not issues,baseline_scene_sha256=digest(base/'BiomechanicalCombatScene.unity'),final_scene_sha256=digest(root/'Assets/Scenes/BiomechanicalCombatScene.unity'),baseline_records=len(b),final_records=len(a),protected_baseline_records=len(b.keys()-allowed_decor),changes=changes,geometry=geometry,issues=issues)
(out/'scene-protection.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result,indent=2));raise SystemExit(0 if result['passed'] else 1)
