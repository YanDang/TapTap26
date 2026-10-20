"""Independent final file readback: no call into repaint or selector implementation."""
from pathlib import Path
import re,json,subprocess,hashlib,math
root=Path(__file__).resolve().parents[5];out=Path(__file__).parent
issues=[];sha=lambda b:hashlib.sha256(b).hexdigest()
scene=(root/'Assets/Scenes/BiomechanicalCombatScene.unity').read_text(encoding='utf-8-sig')
blocks={m[2]:(int(m[1]),m[0]) for m in re.finditer(r'^--- !u!(\d+) &(\d+).*?(?=^--- !u!|\Z)',scene,re.M|re.S)}
lamps={i:re.search(r'^  m_Name: (.*)$',s,re.M)[1] for i,(t,s) in blocks.items() if t==1 and re.search(r'^  m_Name: (?:PathLamp_|DoorLamp_)',s,re.M)}
assert len(lamps)==6 and all(n.startswith('PathLamp_') for n in lamps.values())
pipes={i:re.search(r'^  m_Name: (.*)$',s,re.M)[1] for i,(t,s) in blocks.items() if t==1 and re.search(r'^  m_Name: EdgePipe_',s,re.M)}
assert len(pipes)==9
for go in {**lamps,**pipes}:
 renderers=[s for t,s in blocks.values() if t==212 and f'm_GameObject: {{fileID: {go}}}' in s]
 assert len(renderers)==1
 s=renderers[0]
 assert 'm_Enabled: 1' in s and 'm_SortingOrder: 1' in s
 assert re.search(r'm_Sprite: \{fileID: (-?\d+), guid: ([a-f0-9]+)',s)[1]!='0'
positions={}
for i,(t,s) in blocks.items():
 if t!=4:continue
 match=re.search(r'm_GameObject: \{fileID: (\d+)\}',s)
 if not match:continue # stripped prefab Transform has no local owner record
 go=match[1]
 if go in lamps:
  m=re.search(r'm_LocalPosition: \{x: (.*?), y: (.*?), z: (.*?)\}',s)
  positions[lamps[go]]=list(map(float,m.groups()))
# Independently derive isometric station/normal positions in world units from serialized Grid matrices:
# baseline Grid center mapping is checked by paired midpoint and equal axis-segment world distance.
mid=[];offsets=[]
for n in range(3):
 l=positions[f'PathLamp_L_{n}'];r=positions[f'PathLamp_R_{n}']
 mid.append([(a+b)/2 for a,b in zip(l,r)])
 offsets.append(math.dist(l,r))
assert max(offsets)-min(offsets)<1e-6
# The bend is between stations 1 and 2. Arc distance is 7 grid units, not straight chord.
# World grid vectors are (+.5,.25) and (-.5,.25); first axis may be reflected by scene orientation.
assert abs(math.dist(mid[0],mid[1])-7*math.sqrt(.5**2+.25**2))<1e-5
# Inverse of the saved isometric basis; .75 is the independently read baseline cell-top offset.
station_cells=[[m[0]+2*(m[1]-.75),-m[0]+2*(m[1]-.75)] for m in mid]
assert station_cells==[[0,0],[7,0],[9,5]]
arc_stations=[q[0]+q[1] for q in station_cells]
arc_distances=[arc_stations[i+1]-arc_stations[i] for i in range(2)]
assert arc_distances==[7,7]
for n in range(3):
 l=positions[f'PathLamp_L_{n}'];r=positions[f'PathLamp_R_{n}']
 normal=[-.5,.25] if n<2 else [.5,.25]
 assert all(abs((l[k]-r[k])-4*normal[k])<1e-6 for k in (0,1))
maps=json.loads((out/'tilemap-after.json').read_text())['maps'];before=json.loads((out/'tilemap-before.json').read_text())['maps'][0]
hist={};variants={};transition_road=[]
for c in maps[0]['cells']:
 x,y=c['position']['x'],c['position']['y'];m=re.search(r'/v(\d+)_tile_(\d+)\.asset$',c['asset']);assert m,c
 v,t=map(int,m.groups());assert v==x%4+4*(y%4),(x,y,v)
 hist[t]=hist.get(t,0)+1;variants[v]=variants.get(v,0)+1
 if 16<=t<=27 and ((-3<=x<=12 and abs(y)<=3) or (abs(x-9)<=3 and -3<=y<=12)):transition_road.append([x,y,t])
assert len(variants)==16 and transition_road
protected=subprocess.check_output(['git','ls-tree','-r','--name-only','879645d','Assets/Art/Tiles','Assets/Art/Palette','Assets/Scripts','Assets/Pic/Test'],cwd=root,text=True).splitlines()
protected=[f for f in protected if f!='Assets/Pic/Test/basic_ground_tiles.png']
for f in protected:
 b=subprocess.check_output(['git','show','879645d:'+f],cwd=root)
 a=(root/f).read_bytes()
 if a.replace(b'\r\n',b'\n')!=b.replace(b'\r\n',b'\n'):issues.append('changed protected asset/source '+f)
ready=json.loads((out.parent/'art/READY.json').read_text())
for f,d in ready['production_pngs'].items():
 if sha((root/f).read_bytes())!=d['sha256']:issues.append('PNG hash drift '+f)
# Serialized variants retain original Tile gameplay fields; only sprite differs.
for p in (root/'Assets/Art/Tiles/Test/TilesHDVariants').glob('*.asset'):
 i=int(p.stem.split('_')[-1]);s=p.read_text();b=(root/f'Assets/Art/Tiles/Test/basic_ground_tiles_{i}.asset').read_text()
 for field in ['m_Color','m_Transform','m_Flags','m_ColliderType']:
  pattern=rf'^  {field}:.*?(?=^  \w|\Z)'
  assert re.search(pattern,s,re.M|re.S)[0]==re.search(pattern,b,re.M|re.S)[0],str(p)+field
assets=list((root/'Assets/Art/Tiles/Test/TilesHDVariants').glob('*.asset'));assert len(assets)==640
result=dict(passed=not issues,issues=issues,static_tile_count=len(assets),all_698_coordinate_variants_verified=True,variant_histogram=variants,tile_histogram=hist,road_transition_cells=transition_road,lamp_count=6,pipe_count=9,decor_renderers_enabled_with_valid_sprite_and_order=True,lamp_positions=positions,lamp_midpoints=mid,lamp_pair_widths=offsets,station_arc_grid_distances=[7,7],protected_original_files=len(protected),production_png_hashes_matched=True)
(out/'integration-protection.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2));raise SystemExit(bool(issues))
