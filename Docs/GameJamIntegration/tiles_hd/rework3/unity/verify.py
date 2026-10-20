"""Independent e07520f serialized protection, local road scope and final fresh-process readback."""
from pathlib import Path
import json,re,hashlib,csv
R=Path(__file__).resolve().parents[5];O=Path(__file__).parent
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def blocks(p):
 s=p.read_text(encoding='utf-8-sig');return {m[2]:(int(m[1]),m[0]) for m in re.finditer(r'^--- !u!(\d+) &(\d+).*?(?=^--- !u!|\Z)',s,re.M|re.S)}
b=blocks(O/'baseline/BiomechanicalCombatScene.unity');a=blocks(R/'Assets/Scenes/BiomechanicalCombatScene.unity');issues=[];changes=[]
if a.keys()!=b.keys():issues.append('scene object/component IDs added or deleted')
for k,(t,s) in b.items():
 ns=a[k][1]
 if s==ns:continue
 if t==1839735485:changes.append(dict(id=k,field='Tilemap local rim cells'));continue
 if t==483693784:
  clean=lambda v:re.sub(r'  m_Materials:\n(?:  - .*\n)+','  m_Materials: []\n',v)
  if clean(s)==clean(ns):changes.append(dict(id=k,field='TilemapRenderer material only'));continue
 issues.append('protected record changed '+k)
old=json.loads((O/'tilemap-before.json').read_text());new=json.loads((O/'fresh-readback.json').read_text());saved=json.loads((O/'tilemap-after.json').read_text())
if new!=saved:issues.append('fresh process snapshot differs')
if old['objects']!=new['objects']:issues.append('hierarchy/transforms/components changed')
pos=lambda c:tuple(c['position'][k] for k in ['x','y','z'])
bm=old['maps'][0];am=new['maps'][0];bc={pos(c):c for c in bm['cells']};ac={pos(c):c for c in am['cells']}
if bc.keys()!=ac.keys() or len(ac)!=698 or bm['origin']!=am['origin'] or bm['size']!=am['size']:issues.append('geometry/occupancy changed')
def edge(p):
 x,y,z=p;return (-2<=x<=11 and y in (-2,2)) or (-2<=y<=11 and x in (7,11)) or (x==-2 and -1<=y<=1) or (y==11 and 8<=x<=10)
diff=[]
for p,c in bc.items():
 n=ac[p]
 if any(c[k]!=n[k] for k in ['color','matrix','flags']):issues.append('cell properties changed '+str(p))
 if c['asset']!=n['asset']:
  diff.append(dict(position=p,before=c['asset'],after=n['asset']))
  if not edge(p):issues.append('changed outside local road rim '+str(p))
 for c in [n]:
  v,i=map(int,re.search(r'v(\d+)_tile_(\d+)',c['asset']).groups())
  if v!=p[0]%4+4*(p[1]%4):issues.append('variant coordinate mismatch '+str(p))
# Four exposed longitudinal edges, excluding the interior of the L intersection.
segments={'horizontal_south':[(x,-2,0) for x in range(0,11)],'horizontal_north':[(x,2,0) for x in range(0,7)],'vertical_west':[(7,y,0) for y in range(3,11)],'vertical_east':[(11,y,0) for y in range(0,11)]}
coverage={}
for name,ps in segments.items():
 ids=[int(re.search(r'tile_(\d+)',ac[p]['asset'])[1]) for p in ps]
 transition=[j for j,i in enumerate(ids) if 16<=i<=27]
 windows=[any(16<=i<=27 for i in ids[j:j+3]) for j in range(len(ids)-2)]
 coverage[name]=dict(positions=ps,tiles=ids,transition_offsets=transition,every_three_has_transition=all(windows))
 if not all(windows):issues.append('road gap >3 '+name)
scene=dict(passed=not issues,baseline_records=len(b),final_records=len(a),changes=changes,occupied=len(ac),origin_unchanged=bm['origin']==am['origin'],size_unchanged=bm['size']==am['size'],fresh_readback_matches_saved=new==saved,issues=issues)
(O/'scene-protection.json').write_text(json.dumps(scene,indent=2))
initial=json.loads((O/'initial-hashes.json').read_text());allow={'Assets/Scenes/BiomechanicalCombatScene.unity','Assets/Editor/TilesHD/RepaintMap.cs','Assets/Editor/TilesHD/RecordMap.cs','ProjectSettings/ProjectVersion.txt','Packages/packages-lock.json'}
protected={p:h(R/p)==v for p,v in initial.items() if p not in allow}
if not all(protected.values()):issues.append('protected file hash mismatch')
ready=json.loads((O.parent/'art/READY.json').read_text());png={p:h(R/p)==v['sha256'] for p,v in ready['production_pngs'].items()}
if not all(png.values()):issues.append('READY mismatch')
result=dict(passed=not issues,changed_cells=len(diff),cell_changes=diff,road_edges=coverage,protected_files=len(protected),protected_file_failures=[p for p,v in protected.items() if not v],ready_hashes=png,issues=issues)
(O/'integration-protection.json').write_text(json.dumps(result,indent=2));(O/'scope-validation.json').write_text(json.dumps(dict(passed=not issues,allowed_scene_changes=['local road rim Tiles','ground renderer material only'],cell_changes=diff,all_decor_camera_gameplay_records_unchanged=not scene['issues'],issues=issues),indent=2))
print(json.dumps(dict(passed=not issues,changed_cells=len(diff),protected_files=len(protected),road_coverage={k:v['every_three_has_transition'] for k,v in coverage.items()},issues=issues),indent=2));assert not issues
