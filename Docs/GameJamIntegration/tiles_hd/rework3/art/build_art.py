"""Narrow rework3 adapter. Execute with python -B; never writes historical evidence."""
from pathlib import Path
import subprocess, hashlib, json, sys
import numpy as np
from PIL import Image
sys.dont_write_bytecode=True
A=Path(__file__).resolve().parent; P=A.parents[4]; S=P/'Assets/Pic/Test'
H=A.parents[1]/'rework2/art'; B=A/'baseline'; B.mkdir(parents=True,exist_ok=True)
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
(A/'READY.json').unlink(missing_ok=True)
paths=['Assets/Pic/Test/basic_ground_tiles.png']+[f'Assets/Pic/Test/TilesHDVariants/variant_{i:02d}.png' for i in range(16)]
protected=subprocess.check_output(['git','ls-files','Assets/Pic/Test/*.meta','Assets/Pic/Test/TilesHDVariants/*.meta','Assets/Pic/Test/*backup*'],cwd=P,text=True).splitlines()
for rel in paths+protected:
 dest=B/rel; dest.parent.mkdir(parents=True,exist_ok=True)
 raw=subprocess.check_output(['git','show','e07520f:'+rel],cwd=P)
 if dest.exists(): assert dest.read_bytes()==raw
 else: dest.write_bytes(raw)
# Capture actual importer byte hashes, preserving CRLF exactly as on disk.
protection={rel:h(P/rel) for rel in protected}
(A/'protected_live_hashes.json').write_text(json.dumps(protection,indent=2))
source=(H/'build_art.py').read_text()
# Only relocate all writes to this directory. Immutable historical source remains untouched.
source=source.replace("B=A/'baseline';B.mkdir(exist_ok=True)","B=A.parents[1]/'rework2/art/baseline'")
source=source.replace("V.mkdir(exist_ok=True)","V.mkdir(exist_ok=True); S=A/'reproduction'; S.mkdir(exist_ok=True); V=S/'TilesHDVariants'; V.mkdir(exist_ok=True)")
ns={'__file__':str(A/'build_art.py'),'__name__':'reproduction'}
exec(compile(source,'rework2_relocated','exec'),ns)
checks=[]
for rel in paths:
 rp=A/'reproduction'/Path(rel).relative_to('Assets/Pic/Test')
 same=bool(np.array_equal(np.array(Image.open(rp)),np.array(Image.open(B/rel))))
 checks.append({'path':rel,'reproduced_rgba_equal_to_e07520f':same})
assert all(c['reproduced_rgba_equal_to_e07520f'] for c in checks), checks
(A/'reproduction_validation.json').write_text(json.dumps({'source_sha256':h(H/'build_art.py'),'checks':checks},indent=2))
# Reuse the SAME material arrays, masks, sampling, runes, shells and side geometry.
# Rework2 has no diamond bevel overlay: opacity=0; halve zero rather than add one.
ns['light']=np.ones((256,256),float)
ns['TOP_EDGE_WIDTH_PX']=1
ns['TOP_EDGE_ALPHA_BEFORE']=0.0
ns['TOP_EDGE_ALPHA_AFTER']=ns['TOP_EDGE_ALPHA_BEFORE']*.5
ns['S']=S;ns['V']=S/'TilesHDVariants'
base=np.array(Image.open(B/paths[0]).convert('RGBA'))
ns['props']=[Image.fromarray(base[i//8*256:(i//8+1)*256,i%8*256:(i%8+1)*256]) for i in range(40,56)]
# Guard every pixel outside authorized visible top. Alpha remains immutable everywhere.
ns['baseline_dir']=B
loop=source[source.index('schedule=[]'):]
loop=loop.replace(" out.save(V/f'variant_{vi:02d}.png',optimize=True)",""" fresh=np.array(out)
 prior=np.array(Image.open(baseline_dir/f'Assets/Pic/Test/TilesHDVariants/variant_{vi:02d}.png').convert('RGBA'))
 allow=np.zeros(prior.shape[:2],bool)
 for ci in range(40):
  yy,xx=ci//8*256,ci%8*256
  allow[yy:yy+256,xx:xx+256]=top&(prior[yy:yy+256,xx:xx+256,3]>0)
 fresh[~allow]=prior[~allow]
 fresh[...,3]=prior[...,3]
 out=Image.fromarray(fresh)
 out.save(V/f'variant_{vi:02d}.png',optimize=True)""")
exec(compile(loop,'rework3_lighting_only','exec'),ns)
assert all(h(P/r)==v for r,v in protection.items())
(A/'lighting_contract.json').write_text(json.dumps({'baseline':'e07520f','directional_light_before':'1.015-.03*(.45*X/256+.55*Y/128)','directional_light_after':'1.0','directional_peak_to_peak_after':0,'top_edge_width_px':1,'top_edge_alpha_before':0,'top_edge_alpha_after':0,'edge_note':'No separate diamond edge highlight exists in rework2. Zero opacity retained (0 * 0.5); no new edge painted. Existing slab bevels are material detail and unchanged.','world_lighting':'Not baked here; map owner must supply continuous large-scale world field, not 4x4 texture period.','protected':'All alpha; all RGBA outside visible top of cells0-39; props40-55; all importer metadata'},indent=2))
print('REWORK3 PRODUCED 17 PNG; exact baseline replay passed')
