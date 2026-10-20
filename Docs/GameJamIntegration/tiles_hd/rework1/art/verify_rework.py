"""Independent readback QA. Fixed 1fed22f RGB classifier and fixed scoring regions."""
from pathlib import Path
import json,csv,hashlib,subprocess
import numpy as np
from PIL import Image,ImageDraw
from scipy import ndimage as ndi
A=Path(__file__).resolve().parent;P=A.parents[4];S=P/'Assets/Pic/Test';B=A.parent/'baseline'
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
im=Image.open(S/'basic_ground_tiles.png').convert('RGBA');new=np.array(im);orig=np.array(Image.open(S/'basic_ground_tiles_original_backup.png').convert('RGBA'))
y,x=np.mgrid[:256,:256];u=(x-128)/256+y/128;v=-(x-128)/256+y/128;top=(u>=-.012)&(u<=1.012)&(v>=-.012)&(v<=1.012)
rows=[]
for i in range(40):
 rr,cc=divmod(i,8);a=orig[rr*128:(rr+1)*128,cc*128:(cc+1)*128]
 r,g,b=a[...,:3].astype(float).transpose(2,0,1);m=np.full(r.shape,2,np.uint8);m[(g>r+20)&(g>90)]=0;m[(abs(r-g)<18)&(abs(g-b)<18)&(r>65)]=1
 lab,n=ndi.label((m==1)&(a[...,3]>8));sizes=np.bincount(lab.ravel());shell=(sizes[lab]<60)&(lab>0);m[shell]=2
 m=np.repeat(np.repeat(m,2,0),2,1);valid=np.repeat(np.repeat(a[...,3]>8,2,0),2,1)
 cell=new[rr*256:(rr+1)*256,cc*256:(cc+1)*256];r,g,bl=cell[...,:3].astype(float).transpose(2,0,1)
 # EXACT historical reverse classifier: do not calibrate against generated colors.
 pred=np.full(m.shape,2,np.uint8);pred[(bl-g)<=g*.12]=1;pred[(g>r*1.55)&(g>bl*1.08)]=0
 na=cell[...,3]>8;vt=valid&top
 lum=cell[...,:3]@np.array([.2126,.7152,.0722]);med=ndi.gaussian_filter(lum,2)
 rows.append({'cell':i,'alpha_iou':float((valid&na).sum()/(valid|na).sum()),'material_accuracy':float((pred[valid]==m[valid]).mean()),'top_accuracy':float((pred[vt]==m[vt]).mean()),'top_luminance_mean':float(lum[vt].mean()),'medium_frequency_relative_std':float(med[vt].std()/med[vt].mean())})
with (A/'per_cell_validation.csv').open('w',newline='') as f:
 w=csv.DictWriter(f,fieldnames=rows[0]);w.writeheader();w.writerows(rows)
protected={}
for name in ['basic_ground_tiles.png.meta','basic_ground_tiles_original_backup.png','basic_ground_tiles_original_backup.png.meta','basic_ground_tiles_v1_backup.png','basic_ground_tiles_v1_backup.png.meta']:
 expected=subprocess.check_output(['git','show','1fed22f:Assets/Pic/Test/'+name],cwd=P)
 protected[name]=h(S/name)==hashlib.sha256(expected).hexdigest()
 if name.endswith('.meta'):
  protected[name]= (S/name).read_bytes().replace(b'\r\n',b'\n')==expected.replace(b'\r\n',b'\n')
glow=(new[...,1]>190)&(new[...,2]>150)&(new[...,0]<150)&(new[...,3]>8)
result={'atlas_sha256':h(S/'basic_ground_tiles.png'),'size':list(im.size),'classifier':'exact 1fed22f: soil default; stone (B-G)<=G*.12; kelp G>R*1.55 and G>B*1.08; no shell exemption','all_alpha_pass':all(r['alpha_iou']>=.98 for r in rows),'all_material_pass':all(r['material_accuracy']>=.95 for r in rows),'all_top_pass':all(r['top_accuracy']>=.95 for r in rows),'min_alpha_iou':min(r['alpha_iou'] for r in rows),'min_material_accuracy':min(r['material_accuracy'] for r in rows),'min_top_accuracy':min(r['top_accuracy'] for r in rows),'glow_fraction_opaque':float(glow.sum()/(new[...,3]>8).sum()),'protected_files':protected,'rows':rows}
(A/'validation.json').write_text(json.dumps(result,indent=2))
# Comparison at actual 128 pixels per tile, and full native-size comparison.
baseline=Image.open(B/'basic_ground_tiles.png').convert('RGBA')
for scale in [128,256]:
 width=8*scale;height=7*scale;sheet=Image.new('RGBA',(width*2,height+38),'#152630');d=ImageDraw.Draw(sheet)
 d.text((12,12),'1fed22f / BEFORE',fill='white');d.text((width+12,12),'REWORK 1 / AI + authored materials',fill='white')
 for j,obj in enumerate([baseline,im]):sheet.alpha_composite(obj.resize((width,height),Image.Resampling.LANCZOS),(j*width,38))
 sheet.convert('RGB').save(A/f'comparison_{scale}px.png')
# Native scale representative sheet, plus exact 128px reduction.
ids=[0,2,3,8,18,26,40,49];sheet=Image.new('RGBA',(1024,550),'#152630');d=ImageDraw.Draw(sheet)
for j,i in enumerate(ids):
 tile=im.crop((i%8*256,i//8*256,i%8*256+256,i//8*256+256));sheet.alpha_composite(tile,(j%4*256,j//4*275+18));d.text((j%4*256+8,j//4*275+4),f'CELL {i}',fill='white')
sheet.convert('RGB').save(A/'representatives_256.png');sheet.resize((512,275),Image.Resampling.LANCZOS).convert('RGB').save(A/'representatives_128.png')
def mosaic(grid,name):
 n=len(grid);can=Image.new('RGBA',(n*256+64,n*128+256),'#10232c')
 for s in range(n*2-1):
  for j in range(n):
   k=s-j
   if not 0<=k<n:continue
   i=grid[j][k];tile=im.crop((i%8*256,i//8*256,i%8*256+256,i//8*256+256));can.alpha_composite(tile,((k-j)*128+(n-1)*128+32,(j+k)*64+16))
 can.convert('RGB').save(A/name)
for i in [0,2,3]:mosaic([[i]*3 for _ in range(3)],f'seam_3x3_cell_{i}.png')
grid=json.loads((A.parents[1]/'art/preview_grid.json').read_text());mosaic(grid,'transition_5x5.png');(A/'preview_grid.json').write_text(json.dumps(grid))
print(json.dumps({k:v for k,v in result.items() if k!='rows'},indent=2))
