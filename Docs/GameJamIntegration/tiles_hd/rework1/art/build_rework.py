"""SDXL luminance + authored material details; immutable importer and original masks."""
from pathlib import Path
import importlib.util,json,hashlib
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
from scipy import ndimage as ndi
A=Path(__file__).resolve().parent; ROOT=A.parents[4]; SRC=ROOT/'Assets/Pic/Test'; B=A.parent/'baseline'
spec=importlib.util.spec_from_file_location('old',A.parents[1]/'art/build_tiles_hd.py');old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)
S=1024

def arrim(a):return Image.fromarray(np.uint8(np.clip(a,0,255)))
def noise(rng,n=1024):
 y,x=np.mgrid[:n,:n];z=np.zeros((n,n))
 for f,amp in [(2,1),(4,.5),(8,.25),(16,.1)]:
  z+=amp*(np.sin(x/n*2*np.pi*f+rng.uniform(0,6)+.8*np.sin(y/n*2*np.pi*2))+np.cos(y/n*2*np.pi*f+rng.uniform(0,6)+.7*np.sin(x/n*2*np.pi*3)))
 return z/(z.std()+1e-6)

def texture(k,var):
 rng=np.random.default_rng(8912+k*23+var)
 raw=next((A/'ai_output').glob(f'material_{k}_*.png'))
 im=Image.open(raw).convert('RGB').resize((S,S)); gray=np.array(im).astype(float).mean(2)
 # Mirror-crossfade in the last 96px makes values and derivatives periodic, not a whole-image mirror.
 g=ndi.gaussian_filter(gray,[5,26,16][k]); g=(g-np.mean(g))/(np.std(g)+1e-6)
 for axis in [0,1]:
  for j in range(96):
   w=.5*(1-j/96)**2
   if axis==0:
    v0=g[j].copy();v1=g[-1-j].copy();g[j]=v0*(1-w)+v1*w;g[-1-j]=v1*(1-w)+v0*w
   else:
    v0=g[:,j].copy();v1=g[:,-1-j].copy();g[:,j]=v0*(1-w)+v1*w;g[:,-1-j]=v1*(1-w)+v0*w
 g=np.roll(g,(var*217,var*139),(0,1));n=noise(rng)
 field=np.clip(.15*g+.10*n,-.40,.40)
 if k==2:
  yy,xx=np.mgrid[:S,:S];field+=.035*np.sin(yy*2*np.pi*12/S+1.4*np.sin(xx*2*np.pi/S*3))
 base=np.array([(24,155,128),(109,129,134),(43,61,77)][k])
 a=base[None,None,:]*(1+field[...,None]);im=arrim(a);d=ImageDraw.Draw(im)
 def drawpoly(points,fill):
  for dx in [-S,0,S]:
   for dy in [-S,0,S]:d.polygon([(x+dx,y+dy) for x,y in points],fill=fill)
 def line(points,fill,w):
  for dx in [-S,0,S]:
   for dy in [-S,0,S]:d.line([(x+dx,y+dy) for x,y in points],fill=fill,width=w,joint='curve')
 if k==0:
  for j in range(36):
   px,py=rng.uniform(0,S,2)
   for q in range(rng.integers(5,10)):
    length=rng.uniform(110,220);lean=rng.uniform(25,100);wid=rng.uniform(10,21);t=np.linspace(0,1,20)
    cx=px+lean*t+15*np.sin(np.pi*t);cy=py-length*t
    w=wid*np.sin(np.pi*t)**.7; col=int(rng.choice([106,148,185]))
    drawpoly(list(zip(cx-w,cy))+list(zip((cx+w)[::-1],cy[::-1])),(18,col,int(col*.80)))
    line(list(zip(cx-w*.3,cy)),(33,min(col+20,198),int(col*.87)),2)
    if q==0 and j%3==0:line(list(zip(cx[-4:],cy[-4:])),(92,242,214),4)
 elif k==1:
  # Exactly four staggered broad slabs per texture period. Curved, weathered joins.
  for row in range(2):
   yy=row*512+75
   pts=[(x,yy+5*np.sin(x*.012+row)) for x in range(-50,1100,6)]
   line([(x,y+3) for x,y in pts],(34,50,58),10);line([(x,y+10) for x,y in pts],(149,168,171),3)
   for col in range(2):
    xx=col*512+110+(row%2)*256
    pts=[(xx+5*np.sin(t*.013),yy+t) for t in range(5,510,6)]
    line(pts,(34,50,58),9);line([(x+6,y) for x,y in pts],(150,168,171),3)
    # cracks and sparse brass, kept under one percent of a face
    line([(xx+18,yy+26),(xx+42,yy+50),(xx+39,yy+78)],(69,88,96),3)
    if col==row:line([(xx+24,yy+16),(xx+96,yy+16)],(155,111,64),4)
  # irregular dry-brush mineral flecks, not high-frequency random grain
  for j in range(120):
   px,py=rng.uniform(0,S,2);line([(px,py),(px+rng.uniform(5,18),py-2)],(120,140,145),2)
 else:
  for j in range(280):
   px,py=rng.uniform(0,S,2);L=rng.uniform(9,38)
   line([(px,py),(px+L*.6,py-2),(px+L,py-1)],(48,68,84),1)
 a=np.array(im).astype(float)
 if k==0:
  glow=(a[...,1]>215)&(a[...,0]>70)
  a+=ndi.gaussian_filter(glow.astype(float),8)[...,None]*np.array([6,28,24])
 im=arrim(a);im.save(A/f'texture_{k}_{var}.png');return np.array(im)

def props(tile,i):
 a=np.array(tile).astype(float);r,g,b=a[...,:3].transpose(2,0,1);Y,X=np.mgrid[:256,:256]
 metal=(r>g*1.12)&(r>b*1.3)&(a[...,3]>8)
 glass=(g>r*1.3)&(b>r*1.3)&(a[...,3]>8)
 # distance relief supplies local curved surface, with a thin upper-left specular band.
 dist=ndi.distance_transform_edt(metal);gx=ndi.sobel(dist,1)/8;gy=ndi.sobel(dist,0)/8
 field=.72+.20*np.tanh(dist/4)+.18*np.clip(-gx-gy,-1,1)+.09*np.sin(X*.13)
 a[metal,:3]*=field[metal,None]
 spec=(metal)&(dist>1)&(dist<3)&((gx+gy)>0)
 a[spec,:3]=a[spec,:3]*.65+np.array([222,180,110])*.35
 dg=ndi.distance_transform_edt(glass);core=np.clip(dg/7,0,1)
 a[glass,:3]=np.array([18,67,72])+(core[glass,None]**.65)*np.array([76,163,142])
 halo=ndi.gaussian_filter((glass*core).astype(float),3)
 a[...,:3]+=halo[...,None]*np.array([6,26,23])
 # Preserve original footprint/alpha: ground AO lives inside base geometry, not invisible expansion.
 ys=np.where(a[...,3]>8)[0]; floor=ys.max();contact=np.clip((Y-(floor-16))/16,0,1)
 a[...,:3]*=(1-contact*.38)[...,None]
 return arrim(a)

def main():
 orig=np.array(Image.open(SRC/'basic_ground_tiles_original_backup.png').convert('RGBA'))
 baseline=Image.open(B/'basic_ground_tiles.png').convert('RGBA')
 tex=[[texture(k,v) for v in range(3)] for k in range(3)]
 out=Image.new('RGBA',(2048,1792));Y,X=np.mgrid[:256,:256]
 u=(X-128)/256+Y/128;v=-(X-128)/256+Y/128
 top=(u>=-.012)&(u<=1.012)&(v>=-.012)&(v<=1.012)
 front=128-abs(X-128)*.5;dep=Y-front;depth=np.clip(dep/128,0,1)
 light=1.05-.10*(.45*X/256+.55*Y/128)
 # Four edges have 2 screen-pixel soft glints, 3.5px AO below the front two.
 edge=np.minimum.reduce([u,v,1-u,1-v])*114
 stone_cells=[]
 for ci in range(40):
  sr=orig[ci//8*128:(ci//8+1)*128,ci%8*128:(ci%8+1)*128]
  mm,_=old.source_mask(sr);mm=np.repeat(np.repeat(mm,2,0),2,1)
  if ((mm==1)&top).sum()>500:stone_cells.append(ci)
 rune_cells=set(stone_cells[j] for j in np.linspace(0,len(stone_cells)-1,round(len(stone_cells)/4),dtype=int))
 (A/'rune_schedule.json').write_text(json.dumps({'stone_bearing_cells':stone_cells,'rune_cells':sorted(rune_cells)},indent=2))
 for i in range(40):
  rr,cc=divmod(i,8);src=orig[rr*128:(rr+1)*128,cc*128:(cc+1)*128];m,shell=old.source_mask(src)
  m=np.repeat(np.repeat(m,2,0),2,1);shell=np.repeat(np.repeat(shell,2,0),2,1)
  alpha=np.repeat(np.repeat(src[...,3],2,0),2,1);opaque=alpha>8
  # Low-frequency, sub-2px displacement and antialiased two-pixel contact feather.
  dx=1.35*np.sin(Y*.047+np.sin(X*.03));dy=1.35*np.sin(X*.039+np.sin(Y*.025))
  weights=np.stack([ndi.gaussian_filter(ndi.map_coordinates((m==k).astype(float),[Y+dy,X+dx],order=1,mode='nearest'),.65) for k in range(3)],-1)
  weights/=np.maximum(weights.sum(-1,keepdims=True),1e-6)
  rgb=np.zeros((256,256,3));tx=np.mod(u*1024,1024);ty=np.mod(v*1024,1024)
  for k in range(3):
   color=np.stack([ndi.map_coordinates(tex[k][i%3][...,c].astype(float),[ty,tx],order=1,mode='grid-wrap') for c in range(3)],-1)
   color*=light[...,None]
   side=np.zeros_like(color)+[(19,122,101),(100,119,124),(38,54,70)][k]
   factor=np.where(X<128,.84,.67)*(1-depth*.61)
   phase=depth*19+np.sin(X*.035)*.50+np.sin(X*.079)*.14
   layers=1+.07*np.sin(phase)-.12*np.exp(-((np.sin(phase)-.95)/.12)**2)
   side*=factor[...,None]*layers[...,None]
   # side mineral islands and curved buried roots, local variation not stipple noise
   pebbles=(np.sin(X*.16+np.sin(depth*14))+.6*np.cos(depth*47+X*.033)>1.48)&(depth>.18)
   side[pebbles]*=1.18
   if k==0:
    roots=(np.cos(X*.19+depth*8+np.sin(depth*17))>.987)&(depth<.62)
    side[roots]*=.67
   reflect=np.exp(-np.maximum(dep,0)/6)*.11
   side*=1+reflect[...,None]
   side*=np.where((dep>=0)&(dep<4),.78+.22*dep/4,1)[...,None]
   color=np.where(top[...,None],color,side)
   # subtle bevel rather than uniformly white diamond outline
   color*=np.where(top&(edge<2)&(edge>-.8),1.055,1)[...,None]
   rgb+=color*weights[...,k,None]
  # grass curls over a front edge only 2--10px, while retaining RGB ownership of source grass.
  dangling=(dep>0)&(dep<10)&(m==0)&(np.sin(X*.37+np.sin(X*.09))>.2)
  rgb[dangling]*=1.16
  blade=Image.new('L',(256,256));bd=ImageDraw.Draw(blade)
  for bx in range(16+i%7,246,19):
   by=128-abs(bx-128)*.5
   bd.line([(bx,by-3),(bx+1,by+2),(bx+3,by+7),(bx+2,by+10)],fill=170,width=2)
  bp=np.array(blade)/255*((m==0)&opaque)
  rgb=rgb*(1-bp[...,None])+np.array([23,133,109])*bp[...,None]
  if i in stone_cells and i%3==0:
   diststone=ndi.distance_transform_edt((m==1)&top);sy,sx=np.unravel_index(np.argmax(diststone),m.shape)
   # Small limestone barnacle rim and shadow, not soil-colored freckles.
   for bx,by in [(sx+14,sy+4),(sx+18,sy+6)]:
    ring=((X-bx)**2/6+(Y-by)**2/3)
    select=(ring<1)&(m==1)&top
    rgb[select]=np.array([139,157,162])
    select=(ring<.32)&(m==1)&top;rgb[select]=np.array([69,83,88])
  # Contact AO only at material boundaries, never transparent corners.
  for k in [0,1]:
   other=(m!=k)&opaque&top
   if other.any():
    dist=ndi.distance_transform_edt(~other);pick=(m==k)&top&(dist<3)
    rgb[pick]*=(.86+.14*dist[pick,None]/3)
  # Every fourth stone-bearing tile has one rune, clipped to actual stone.
  if i in rune_cells:
   sy,sx=np.unravel_index(np.argmax(ndi.distance_transform_edt((m==1)&top)),m.shape)
   mask=Image.new('L',(256,256));d=ImageDraw.Draw(mask);d.line([(sx-16,sy-4),(sx-4,sy+2),(sx+7,sy-4),(sx+16,sy+2),(sx+4,sy+8)],fill=255,width=2)
   rune=np.array(mask)/255*((m==1)&top);glow=ndi.gaussian_filter(rune,2.6)
   rgb+=glow[...,None]*np.array([5,25,24]);rgb=rgb*(1-rune[...,None])+rune[...,None]*np.array([62,232,224])
  # Source white inclusions become shaded, ribbed shells. Soil-blue rims keep fixed semantics.
  lab,count=ndi.label(shell)
  for c in range(1,count+1):
   pick=(lab==c)&top
   if not pick.any():continue
   yy,xx=np.where(pick);cx=xx.mean();cy=yy.mean();rx=max(np.ptp(xx)/2,1);ry=max(np.ptp(yy)/2,1)
   dxs=(X-cx)/rx;dys=(Y-cy)/ry
   f=np.clip(.64-.28*dxs-.30*dys+.12*np.cos(np.arctan2(dys,dxs)*7),.15,1)
   # rare bright intact shell, majority partially buried and blue-shaded
   base=np.array([160,190,218]) if (c+i)%5==0 else np.array([76,100,125])
   rgb[pick]=base*f[pick,None]
  out.paste(arrim(np.dstack([rgb,alpha])),(cc*256,rr*256))
 for i in range(40,56):
  rr,cc=divmod(i,8);tile=baseline.crop((cc*256,rr*256,cc*256+256,rr*256+256));out.paste(props(tile,i),(cc*256,rr*256))
 out.save(SRC/'basic_ground_tiles.png');out.save(A/'atlas_final.png')
 sheet=Image.new('RGB',(1536,550),'#13232b');d=ImageDraw.Draw(sheet)
 for k,name in enumerate(['KELP / SDXL + painted leaf clusters','LIMESTONE / SDXL + bevel & joints','SILT / SDXL + current ripples']):
  sheet.paste(Image.open(A/f'texture_{k}_0.png').resize((512,512)),(k*512,38));d.text((k*512+12,12),name,fill='white')
 sheet.save(A/'materials_comparison.png')
 print('atlas',hashlib.sha256((SRC/'basic_ground_tiles.png').read_bytes()).hexdigest())
if __name__=='__main__':main()
