"""Supplemental read-only audit; run after build_tiles_hd.py."""
from pathlib import Path
import hashlib,json,re,subprocess
import numpy as np
from PIL import Image,ImageDraw
P=Path(__file__).resolve().parents[4];A=Path(__file__).resolve().parent;S=P/'Assets/Pic/Test';B=A.parent/'baseline_source'
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
o=(B/'basic_ground_tiles.png.meta').read_text();n=(S/'basic_ground_tiles.png.meta').read_text()
rect=lambda t:[tuple(map(int,m)) for m in re.findall(r'        x: (\d+)\n        y: (\d+)\n        width: (\d+)\n        height: (\d+)',t)]
assert len(rect(n))==56
assert rect(n)==[tuple(v*2 for v in r) for r in rect(o)]
original_path='Assets/Pic/Test/basic_ground_tiles_original_backup.png'
git_bytes=subprocess.check_output(['git','show','HEAD:'+original_path],cwd=P)
assert hashlib.sha256(git_bytes).hexdigest()==h(S/'basic_ground_tiles_original_backup.png')
assert h(B/'basic_ground_tiles.png')==h(S/'basic_ground_tiles_v1_backup.png')
im=Image.open(S/'basic_ground_tiles.png');old=Image.open(B/'basic_ground_tiles.png')
# reference + actual 256px props, legible engineering visual inspection.
can=Image.new('RGBA',(2048,1050),'#26363E');can.alpha_composite(old.crop((0,640,1024,896)).resize((2048,512),Image.Resampling.NEAREST),(0,20));can.alpha_composite(im.crop((0,1280,2048,1792)),(0,538));can.convert('RGB').save(A/'props_before_after_256px.png')
checks={'original_backup_matches_HEAD':True,'v1_matches_baseline':True,'rects_all_56_exactly_doubled':True,'v1_sha256':h(B/'basic_ground_tiles.png'),'original_sha256':h(S/'basic_ground_tiles_original_backup.png'),'final_sha256':h(S/'basic_ground_tiles.png'),'props':[]}
for i in range(40,56):
 c,r=i%8,i//8;a=old.crop((c*128,r*128,c*128+128,r*128+128));b=im.crop((c*256,r*256,c*256+256,r*256+256))
 checks['props'].append({'cell':i,'reference_bbox_x2':[v*2 for v in a.getbbox()],'new_bbox':b.getbbox(),'not_equal_to_lanczos_upscale':not np.array_equal(np.array(b),np.array(a.resize((256,256),Image.Resampling.LANCZOS)))})
# Texture edge finite-difference metrics; these are not a claim of aesthetic acceptance.
checks['texture_edges']=[]
for p in sorted(A.glob('texture_*.png')):
 a=np.array(Image.open(p)).astype(float);checks['texture_edges'].append({'file':p.name,'wrap_x_MAE':float(abs(a[:,0]-a[:,-1]).mean()),'wrap_y_MAE':float(abs(a[0]-a[-1]).mean()),'interior_x_MAE':float(abs(np.diff(a,axis=1)).mean()),'interior_y_MAE':float(abs(np.diff(a,axis=0)).mean())})
(A/'supplemental_validation.json').write_text(json.dumps(checks,indent=2));print(json.dumps(checks,indent=2))
