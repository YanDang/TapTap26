from pathlib import Path
import json,csv,hashlib,subprocess
import numpy as np
from PIL import Image,ImageDraw
from scipy import ndimage as ndi
A=Path(__file__).resolve().parent;P=A.parents[4];S=P/'Assets/Pic/Test';B=A/'baseline';R=A.parents[1]/'rework1/art'
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
orig=np.array(Image.open(B/'basic_ground_tiles_original_backup.png').convert('RGBA'));baseline=Image.open(B/'basic_ground_tiles.png').convert('RGBA')
y,x=np.mgrid[:256,:256];u=(x-128)/256+y/128;v=-(x-128)/256+y/128;top=(u>=-.012)&(u<=1.012)&(v>=-.012)&(v<=1.012)
images=[Image.open(S/'TilesHDVariants'/f'variant_{j:02d}.png').convert('RGBA') for j in range(16)];rows=[];unique={i:set() for i in range(40)}
for vi,im in enumerate(images):
 new=np.array(im);assert im.size==(2048,1792)
 for i in range(40):
  rr,cc=divmod(i,8);a=orig[rr*128:(rr+1)*128,cc*128:(cc+1)*128]
  r,g,b=a[...,:3].astype(float).transpose(2,0,1);m=np.full(r.shape,2,np.uint8);m[(g>r+20)&(g>90)]=0;m[(abs(r-g)<18)&(abs(g-b)<18)&(r>65)]=1
  lab,n=ndi.label((m==1)&(a[...,3]>8));sizes=np.bincount(lab.ravel());shell=(sizes[lab]<60)&(lab>0);m[shell]=2
  m=np.repeat(np.repeat(m,2,0),2,1);valid=np.repeat(np.repeat(a[...,3]>8,2,0),2,1)
  cell=new[rr*256:(rr+1)*256,cc*256:(cc+1)*256];r,g,bl=cell[...,:3].astype(float).transpose(2,0,1)
  pred=np.full(m.shape,2,np.uint8);pred[(bl-g)<=g*.12]=1;pred[(g>r*1.55)&(g>bl*1.08)]=0
  na=cell[...,3]>8;vt=valid&top;lum=cell[...,:3]@np.array([.2126,.7152,.0722]);med=ndi.gaussian_filter(lum,2)
  rows.append({'variant':vi,'cell':i,'alpha_iou':float((valid&na).sum()/(valid|na).sum()),'material_accuracy':float((pred[valid]==m[valid]).mean()),'top_accuracy':float((pred[vt]==m[vt]).mean()),'top_luminance_mean':float(lum[vt].mean()),'medium_frequency_relative_std':float(med[vt].std()/med[vt].mean())});unique[i].add(hashlib.sha256(cell.tobytes()).hexdigest())
with (A/'per_cell_validation.csv').open('w',newline='') as f:
 w=csv.DictWriter(f,fieldnames=rows[0]);w.writeheader();w.writerows(rows)
protected={}
for name in ['basic_ground_tiles.png.meta','basic_ground_tiles_original_backup.png','basic_ground_tiles_original_backup.png.meta','basic_ground_tiles_v1_backup.png','basic_ground_tiles_v1_backup.png.meta']:
 expected=subprocess.check_output(['git','show','879645d:Assets/Pic/Test/'+name],cwd=P);actual=(S/name).read_bytes();protected[name]=actual.replace(b'\r\n',b'\n')==expected.replace(b'\r\n',b'\n') if name.endswith('.meta') else actual==expected
result={'all_alpha_pass':all(r['alpha_iou']>=.98 for r in rows),'all_material_pass':all(r['material_accuracy']>=.95 for r in rows),'all_top_pass':all(r['top_accuracy']>=.95 for r in rows),'min_alpha_iou':min(r['alpha_iou'] for r in rows),'min_material_accuracy':min(r['material_accuracy'] for r in rows),'min_top_accuracy':min(r['top_accuracy'] for r in rows),'protected_files':protected,'distinct_variants_per_ground_cell':{i:len(s) for i,s in unique.items()},'classifier':'UNCHANGED 1fed22f/879645d thresholds and whole-cell / top regions; no exemptions','failures':[r for r in rows if min(r['material_accuracy'],r['top_accuracy'])<.95]}
(A/'validation.json').write_text(json.dumps(result,indent=2))
# Export actual-scale before/new sheets
for scale in [128,256]:
 w=8*scale;hh=7*scale;sheet=Image.new('RGBA',(w*2,hh+36),'#152630');d=ImageDraw.Draw(sheet);d.text((12,10),'879645d BEFORE',fill='white');d.text((w+12,10),'REWORK2 variant_00',fill='white')
 for j,im in enumerate([baseline,images[0]]):sheet.alpha_composite(im.resize((w,hh),Image.Resampling.LANCZOS),(j*w,36))
 sheet.convert('RGB').save(A/f'comparison_{scale}px.png')
ids=[0,2,3,18,24,26,40,49];sheet=Image.new('RGBA',(1024,550),'#152630');d=ImageDraw.Draw(sheet)
for j,i in enumerate(ids):
 tile=images[0].crop((i%8*256,i//8*256,i%8*256+256,i//8*256+256));sheet.alpha_composite(tile,(j%4*256,j//4*275+18));d.text((j%4*256+8,j//4*275+4),f'CELL {i}',fill='white')
sheet.convert('RGB').save(A/'representatives_256.png');sheet.resize((512,275),Image.Resampling.LANCZOS).convert('RGB').save(A/'representatives_128.png')
def mosaic(grid,name,variants=True):
 n=len(grid);can=Image.new('RGBA',(n*256+64,n*128+288),'#10232c');d=ImageDraw.Draw(can);d.text((16,8),'REAL COORDINATE VARIANTS' if variants else 'DIAGNOSTIC: SINGLE IMAGE REPEATED (not variant acceptance)',fill='white')
 for s in range(n*2-1):
  for j in range(n):
   k=s-j
   if not 0<=k<n:continue
   i=grid[j][k];vi=(k%4)+(j%4)*4 if variants else 0;im=images[vi];tile=im.crop((i%8*256,i//8*256,i%8*256+256,i//8*256+256));can.alpha_composite(tile,((k-j)*128+(n-1)*128+32,(j+k)*64+40))
 can.convert('RGB').save(A/name)
for i in [0,2,3]:
 for n in [3,8]:mosaic([[i]*n for _ in range(n)],f'variants_{n}x{n}_cell_{i}.png')
 mosaic([[i]*8 for _ in range(8)],f'diagnostic_single_8x8_cell_{i}.png',False)
grid=json.loads((R/'preview_grid.json').read_text());mosaic(grid,'transition_5x5.png');(A/'preview_grid.json').write_text(json.dumps(grid))
sheet=Image.new('RGB',(1536,1080),'#152630');d=ImageDraw.Draw(sheet)
for k in range(3):
 for row in range(2):
  p=R/f'texture_{k}_0.png' if row==0 else (A/'world_stone_4x4.png' if k==1 else A/f'material_final_{k}.png')
  sheet.paste(Image.open(p).resize((512,512)),(512*k,row*540+28))
 d.text((512*k+12,8),'879645d material '+str(k),fill='white');d.text((512*k+12,548),'REWORK2 material '+str(k),fill='white')
sheet.save(A/'materials_comparison.png')
print(json.dumps({k:v for k,v in result.items() if k not in ['failures','distinct_variants_per_ground_cell']},indent=2));print('failures',len(result['failures']))
