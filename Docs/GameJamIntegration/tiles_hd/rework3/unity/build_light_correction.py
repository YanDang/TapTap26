"""Remove only the pre-existing soil low-frequency baked luminance. No production atlas writes.
World-projected R16 illumination gain, neutral outside visible top faces. Chroma, alpha,
material partition, high-frequency detail, props and atlas remain untouched.
"""
from pathlib import Path
import json,hashlib
import numpy as np
from PIL import Image
from scipy import ndimage as ndi
R=Path(__file__).resolve().parents[5]; O=Path(__file__).parent
A=O.parent/'art'; src=np.asarray(Image.open(A/'material_final_2.png')).astype(float)[...,:3]
lum=src@np.array([.2126,.7152,.0722]); field=ndi.gaussian_filter(lum,8,mode='wrap'); mean=lum.mean()
# 128 pixels/world unit, enough for a lighting-only field. World bounds derived from cells.
d=json.loads((O/'tilemap-after.json').read_text()); cells=d['maps'][0]['cells']
xmin,ymin=-21.,-10.; width,height=5376,2816; ppu=128
out=np.ones((height,width),np.float32)
orig=np.array(Image.open(R/'Assets/Pic/Test/basic_ground_tiles_original_backup.png'))
for c in sorted(cells,key=lambda c:-(c['position']['x']+c['position']['y'])):
 x,y=c['position']['x'],c['position']['y']; i=int(Path(c['asset']).stem.rsplit('_',1)[1]);vx=x%4;vy=y%4
 # Tile anchor(.5,.5), isometric grid cell(1,.5); Sprite pivot(.5,.25).
 cx=.5*(x-y); cy=.25*(x+y)+.25
 ix0=int(round((cx-.5-xmin)*ppu)); iy0=int(round((cy+.75-ymin)*ppu))
 X,Y=np.meshgrid(np.arange(128)/128*256,np.arange(65)/128*256)
 u=(X-128)/256+Y/128;v=-(X-128)/256+Y/128
 inside=(u>=0)&(u<=1)&(v>=0)&(v<=1)
 tex=orig[i//8*128:(i//8+1)*128,i%8*128:(i%8+1)*128,:3].astype(float)
 soil=(tex[...,0]>tex[...,1]+30)&(tex[...,2]<60)
 weight=ndi.map_coordinates(ndi.gaussian_filter(soil.astype(float),.55),[Y*.5,X*.5],order=1,mode='nearest')
 lighting=ndi.map_coordinates(field,[(v+vy)*256+i*211,(u+vx)*256+i*137],order=1,mode='grid-wrap')
 linear=lambda s:np.where(s<=.04045,s/12.92,((s+.055)/1.055)**2.4)
 gain=1+weight*(linear(mean/255)/linear(lighting/255)-1)
 for row in range(65):
  yy=iy0-row
  if not 0<=yy<height:continue
  xs=np.arange(128)+ix0; mask=inside[row]&(xs>=0)&(xs<width)
  out[yy,xs[mask]]=gain[row,mask]
# PNG 16bit gain encoded /2. Shader decodes *2, neutral=.5. No gamma conversion.
p=R/'Assets/Art/Tiles/Test/TilesHDSoilLight.png';Image.fromarray(np.uint16(np.clip(out/2,0,1)*65535)).save(p)
(O/'light-correction.json').write_text(json.dumps(dict(purpose='remove soil baked low-frequency luminance only; replace with continuous shader world field',source=str(A/'material_final_2.png'),source_sha256=hashlib.sha256((A/'material_final_2.png').read_bytes()).hexdigest(),sigma_source_pixels=8,world_origin=[xmin,ymin],world_size=[width/ppu,height/ppu],pixels_per_world_unit=ppu,gain_min=float(out.min()),gain_max=float(out.max()),production_atlases_unchanged=True),indent=2))
print('LIGHT correction',out.min(),out.max(),p)
