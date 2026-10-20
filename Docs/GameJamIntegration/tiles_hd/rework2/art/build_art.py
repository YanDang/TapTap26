from pathlib import Path
import json,subprocess,hashlib,importlib.util,shutil
import numpy as np
from PIL import Image,ImageDraw
from scipy import ndimage as ndi
A=Path(__file__).resolve().parent; P=A.parents[4]; S=P/'Assets/Pic/Test'; R=A.parents[1]/'rework1/art'; V=S/'TilesHDVariants'; V.mkdir(exist_ok=True)
B=A/'baseline';B.mkdir(exist_ok=True)
for name in ['basic_ground_tiles.png','basic_ground_tiles.png.meta','basic_ground_tiles_original_backup.png']:
 p=B/name
 if not p.exists():p.write_bytes(subprocess.check_output(['git','show','879645d:Assets/Pic/Test/'+name],cwd=P))
(A/'READY.json').unlink(missing_ok=True)
spec=importlib.util.spec_from_file_location('prior',R/'build_rework.py');old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)
# Preserve actual previously generated SDXL raw images and successful API histories.
(A/'ai_output').mkdir(exist_ok=True)
for p in list((R/'ai_output').glob('*.png'))+list(R.glob('ai_*.json')):
 shutil.copy2(p,A/'ai_output'/p.name if p.suffix=='.png' else A/p.name)
old.A=A
# The earlier generator supplies authored kelp clusters and SDXL luminance, not its per-tile composition.
tex=[old.texture(k,0).astype(float) for k in range(3)]
# HSV saturation -25%, value -20% exactly on source kelp (quantization aside).
g=tex[0]; mx=g.max(2,keepdims=True);tex[0]=(mx+(g-mx)*.75)*.8
# Soil: no shell/stipple overlay, lower contrast retains genuine SDXL sediment structure.
tex[2]=np.array([43,61,77])+(tex[2]-np.array([43,61,77]))*.45
tex[2]=ndi.gaussian_filter(tex[2],(5,5,0))
# A quieter broad-leaf canopy replaces the prior dense hairline marks.
rawgrass=np.array(Image.open(A/'ai_output/material_0_00001_.png').convert('RGB')).mean(2)
field=ndi.gaussian_filter(rawgrass,12);field=(field-field.mean())/field.std()
leafim=old.arrim(np.array([24,142,118])[None,None,:]*(1+.06*field[...,None]));ld=ImageDraw.Draw(leafim);rg=np.random.default_rng(3217)
for cluster in range(19):
 px,py=rg.uniform(0,1024,2)
 for q in range(7):
  length=rg.uniform(85,175);lean=(q-3)*15+40;wid=rg.uniform(12,22);t=np.linspace(0,1,24);cx=px+lean*t+18*np.sin(t*np.pi);cy=py-length*t;ww=wid*np.sin(np.pi*t)**.8
  col=[(18,121,100),(23,149,123),(32,171,141)][q%3];pts=list(zip(cx-ww,cy))+list(zip((cx+ww)[::-1],cy[::-1]))
  for ox in [-1024,0,1024]:
   for oy in [-1024,0,1024]:
    ld.polygon([(xx+ox,yy+oy) for xx,yy in pts],fill=col)
    if q==0 and cluster%5==0:ld.line([(cx[-2]+ox,cy[-2]+oy),(cx[-1]+ox,cy[-1]+oy)],fill=(92,242,214),width=3)
g=np.array(leafim).astype(float);mx=g.max(2,keepdims=True);tex[0]=(mx+(g-mx)*.75)*.8
# 2048px continuous world slab layout: joints independent of 512px tile boundaries.
rng=np.random.default_rng(784311);N=2048
raw=np.array(Image.open(A/'ai_output/material_1_00001_.png').convert('RGB').resize((N,N))).mean(2)
g=ndi.gaussian_filter(raw,18);g=(g-g.mean())/g.std()
world=old.arrim(np.array([109,129,134])[None,None,:]*.9*(1+.045*g[...,None]));d=ImageDraw.Draw(world)
ys=[-170,265,740,1215,1690,2218]
for row in range(len(ys)-1):
 y0,y1=ys[row:row+2]; xx=-350+(row%2)*263
 while xx<N:
  w=int(rng.integers(420,760));x1=xx+w
  # rounded slab, narrow actual joint; no diamond-edge contour
  col=tuple(np.uint8(np.array([109,129,134])*.9*rng.uniform(.96,1.04)))
  d.rounded_rectangle((xx+3,y0+3,x1-3,y1-3),radius=14,outline=(38,52,58),width=5)
  d.line([(xx+19,y0+10),(x1-20,y0+10)],fill=(132,149,153),width=3)
  d.line([(xx+10,y0+20),(xx+10,y1-18)],fill=(120,138,143),width=3)
  for j in range(3):
   sx=int(rng.integers(xx+25,x1-25));sy=int(rng.integers(y0+25,y1-25));d.line([(sx,sy),(sx+int(rng.integers(12,45)),sy-4)],fill=(86,105,112),width=2)
  xx=x1
world.save(A/'world_stone_4x4.png');stone=np.array(world)
for k in [0,2]:old.arrim(tex[k]).save(A/f'material_final_{k}.png')
orig=np.array(Image.open(B/'basic_ground_tiles_original_backup.png').convert('RGBA'));base=Image.open(B/'basic_ground_tiles.png').convert('RGBA')
Y,X=np.mgrid[:256,:256];u=(X-128)/256+Y/128;v=-(X-128)/256+Y/128;top=(u>=-.012)&(u<=1.012)&(v>=-.012)&(v<=1.012)
front=128-abs(X-128)*.5;dep=Y-front;depth=np.clip(dep/128,0,1)
# Tile-boundary lighting is kept deliberately very weak; actual slab bevel carries relief.
light=1.015-.03*(.45*X/256+.55*Y/128)
cache=[]
for i in range(40):
 rr,cc=divmod(i,8);src=orig[rr*128:(rr+1)*128,cc*128:(cc+1)*128];m,shell=old.old.source_mask(src)
 m=np.repeat(np.repeat(m,2,0),2,1);shell=np.repeat(np.repeat(shell,2,0),2,1);alpha=np.repeat(np.repeat(src[...,3],2,0),2,1)
 dx=1.1*np.sin(Y*.047+np.sin(X*.03));dy=1.1*np.sin(X*.039+np.sin(Y*.025))
 weights=np.stack([ndi.gaussian_filter(ndi.map_coordinates((m==k).astype(float),[Y+dy,X+dx],order=1,mode='nearest'),.55) for k in range(3)],-1);weights/=np.maximum(weights.sum(-1,keepdims=True),1e-6)
 cache.append((m,shell,alpha,weights))
props=[]
for i in range(40,56):
 rr,cc=divmod(i,8);a=np.array(base.crop((cc*256,rr*256,cc*256+256,rr*256+256))).astype(float);r,g,b=a[...,:3].transpose(2,0,1)
 glass=(g>r*1.3)&(b>r*1.3)&(a[...,3]>8)
 # Remove narrow cyan perimeter: only substantial connected glass interiors emit.
 dg=ndi.distance_transform_edt(glass);inner=dg>4.5; candidate=glass.copy();glass=ndi.binary_dilation(inner,iterations=2)&candidate
 glass &= ndi.distance_transform_edt(a[...,3]>8)>4
 a[candidate&~glass,:3]=[43,61,65] # suppress narrow cyan frame strokes and isolated bright pixels
 core=np.clip((dg-1)/np.maximum(ndi.maximum_filter(dg,25)-1,1),0,1)**1.45
 a[glass,:3]=np.array([18,106,102])+core[glass,None]*np.array([109,139,128])
 rim=glass&(dg<2);a[rim,:3]=[32,74,76]
 halo=ndi.gaussian_filter((inner*core).astype(float),2.5,truncate=4);halo[ndi.distance_transform_edt(~inner)>10]=0
 a[...,:3]=a[...,:3]*(1-halo[...,None]*.18)+np.array([92,205,192])*halo[...,None]*.18
 metal=(r>g*1.12)&(r>b*1.3)&(a[...,3]>8);dm=ndi.distance_transform_edt(metal);gx=ndi.sobel(dm,1)/8;gy=ndi.sobel(dm,0)/8
 pick=metal&(dm>1)&(dm<3)&(gx+gy>0);a[pick,:3]=a[pick,:3]*.65+np.array([222,180,110])*.35
 props.append(old.arrim(a))
schedule=[]
for vi in range(16):
 out=Image.new('RGBA',(2048,1792));vx=vi%4;vy=vi//4
 for i,(m,shell,alpha,weights) in enumerate(cache):
  rgb=np.zeros((256,256,3))
  for k in range(3):
   if k==1:t=stone;tx=(u+vx)*512;ty=(v+vy)*512
   elif k==2:
    t=tex[k];tx=(u+vx)*256+i*137;ty=(v+vy)*256+i*211
   else:
    t=np.rot90(tex[k],(vi+i)%4);tx=u*512+((vi*233+i*137)%1024);ty=v*512+((vi*389+i*211)%1024)
   color=np.stack([ndi.map_coordinates(t[...,c].astype(float),[ty,tx],order=1,mode='grid-wrap') for c in range(3)],-1)*light[...,None]
   sb=np.array([(36,99,84),(90,107,112),(38,54,70)][k]);side=np.zeros_like(color)+sb
   factor=np.where(X<128,.84,.67)*(1-depth*.61);phase=depth*19+np.sin(X*.035+vi*.8)*.50
   side*=factor[...,None]*(1+.06*np.sin(phase))[...,None]
   color=np.where(top[...,None],color,side);rgb+=color*weights[...,k,None]
  # Sparse rune schedule across tile variants (~1/6), not a fixed mark in each stone tile.
  if (vi*7+i)%6==0 and ((m==1)&top).sum()>500:
   rrng=np.random.default_rng(vi*101+i);ys1,xs1=np.where((ndi.distance_transform_edt((m==1)&top)>12));
   if len(ys1):
    n=int(rrng.integers(len(ys1)));sy,sx=int(ys1[n]),int(xs1[n]);mask=Image.new('L',(256,256));dd=ImageDraw.Draw(mask);dd.line([(sx-8,sy-3),(sx,sy+1),(sx+6,sy-3)],fill=255,width=1);rune=np.array(mask)/255*((m==1)&top);rgb=rgb*(1-rune[...,None])+np.array([31,116,112])*rune[...,None];schedule.append([vi,i,sx,sy])
  # Only original white-inclusion tiles 18..27 receive small modeled shells.
  if 18<=i<=27:
   lab,n=ndi.label(shell)
   for c in range(1,n+1):
    pick=(lab==c)&top
    if not pick.any():continue
    yy,xx=np.where(pick);cx=xx.mean();cy=yy.mean();dxs=(X-cx)/max(np.ptp(xx)/2,1);dys=(Y-cy)/max(np.ptp(yy)/2,1)
    f=np.clip(.65-.22*dxs-.23*dys+.07*np.cos(np.arctan2(dys,dxs)*7),.2,1);rgb[pick]=np.array([96,124,150])*f[pick,None]
  out.paste(old.arrim(np.dstack([rgb,alpha])),(i%8*256,i//8*256))
 for j,p in enumerate(props):out.paste(p,((j%8)*256,(5+j//8)*256))
 out.save(V/f'variant_{vi:02d}.png',optimize=True)
 if vi==0:out.save(S/'basic_ground_tiles.png',optimize=True)
 print('variant',vi,flush=True)
(A/'rune_schedule.json').write_text(json.dumps(schedule))
print('DONE')
