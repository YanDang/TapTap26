"""Deterministic procedural HD repaint. Python + Pillow + numpy + scipy. No Unity.
Run from anywhere: python build_tiles_hd.py ; validator reads the saved PNG, not construction masks.
"""
from pathlib import Path
import json,re,hashlib,csv
import numpy as np
from PIL import Image,ImageDraw,ImageFilter,ImageFont
from scipy import ndimage as ndi
ROOT=Path(__file__).resolve().parents[4]
ART=Path(__file__).resolve().parent
SRC=ROOT/'Assets/Pic/Test'
BASE=ART.parent/'baseline_source'
S=1024

def save(a,p): Image.fromarray(np.uint8(np.clip(a,0,255))).save(p)
def source_mask(a):
    r,g,b=a[...,:3].astype(float).transpose(2,0,1)
    m=np.full(r.shape,2,np.uint8)
    m[(g>r+20)&(g>90)]=0
    m[(abs(r-g)<18)&(abs(g-b)<18)&(r>65)]=1
    lab,n=ndi.label((m==1)&(a[...,3]>8)); sizes=np.bincount(lab.ravel())
    shell=(sizes[lab]<60)&(lab>0)
    m[shell]=2
    return m,shell

def texture(k,v):
    rng=np.random.default_rng(500+k*10+v)
    y,x=np.mgrid[:S,:S]; wave=(np.sin(x*2*np.pi/S*3+v)+np.cos(y*2*np.pi/S*4)+np.sin((x+y)*2*np.pi/S*2))*1.6
    colors=[(28,156,130),(98,117,122),(43,60,75)]
    a=np.zeros((S,S,3))+colors[k]+wave[...,None]
    if k==1:
        # Broad panel relief: bevel light at upper edge, soft shadow at lower edge.
        # Smooth periodic relief, not half-cosine sawtooth resets at panel/tile edges.
        relief=2.5*np.cos((y-96)*2*np.pi/256)-1.5*np.cos((x-96)*2*np.pi/512)
        a+=relief[...,None]
    im=Image.fromarray(np.uint8(a)); d=ImageDraw.Draw(im)
    # Periodic draw: translate authored motifs at all wrap boundaries.
    def line(points,fill,width):
        for dx in [-S,0,S]:
            for dy in [-S,0,S]: d.line([(px+dx,py+dy) for px,py in points],fill=fill,width=width)
    if k==0:
        # Uneven rooted fans with tapered curved ribbons, not repeated three-prong sticks.
        def polygon(points,fill):
            for dx in [-S,0,S]:
                for dy in [-S,0,S]:
                    d.polygon([(px+dx,py+dy) for px,py in points],fill=fill)
        for j in range(64):
            px,py=rng.integers(0,S,2)
            for q in range(int(rng.integers(4,7))):
                length=float(rng.uniform(65,155)); lean=float(rng.uniform(-34,74)); width=float(rng.uniform(5,12))
                t=np.linspace(0,1,18)
                cx=px+lean*t+22*np.sin(t*np.pi)*t;cy=py-length*t
                w=width*np.sin(np.pi*t)**.8
                pts=list(zip(cx-w,cy))+list(zip((cx+w)[::-1],cy[::-1]))
                green=int(rng.integers(128,173))
                polygon(pts,(22,green,int(green*.84)))
                line(list(zip(cx-w*.3,cy)),(36,green+10,int(green*.88)),2)
            if j%23==0: line(list(zip(cx[-3:],cy[-3:])),(77,207,178),2)
    elif k==1:
        # Staggered broad cut slabs; periodic divisions are internal material detail, never a tile frame.
        for row in range(4):
            yy=row*256+96; offset=(row%2)*256+96
            line([(0,yy),(S,yy)],(73,94,101),5)
            line([(0,yy+5),(S,yy+5)],(115,133,138),2)
            for col in range(3):
                xx=col*512+offset
                line([(xx,yy),(xx+8,yy+256)],(76,96,103),4)
                if (row+col+v)%5==0:
                    line([(xx+20,yy+12),(xx+90,yy+12)],(159,123,76),3)
                if (row*5+col+v)%17==0:
                    line([(xx+80,yy+48),(xx+80,yy+79),(xx+120,yy+79)],(72,160,160),3)
        for j in range(110):
            px,py=rng.integers(0,S,2); line([(px,py),(px+int(rng.integers(4,15)),py-2)],(120,141,145),2)
    else:
        for j in range(380):
            px,py=rng.integers(0,S,2);line([(px,py),(px+int(rng.integers(3,15)),py-1)],(49,67,82),1)
    im.save(ART/f'texture_{["kelp","limestone","silt"][k]}_{v}.png')
    return np.array(im)


def prop(i,bbox):
    # New layered vector design at 3x target res; never reads reference RGB.
    scale=3; im=Image.new('RGBA',(768,768));d=ImageDraw.Draw(im)
    def coords(p): return tuple(int(v*scale) for v in p)
    def ellipse(b,c,outline=None,w=1): d.ellipse(coords(b),fill=c,outline=outline,width=w*scale)
    def poly(p,c):d.polygon([(int(x*scale),int(y*scale)) for x,y in p],fill=c)
    def line(p,c,w=1):d.line([(int(x*scale),int(y*scale)) for x,y in p],fill=c,width=w*scale,joint='curve')
    def rect(b,c,r=0):d.rounded_rectangle(coords(b),radius=r*scale,fill=c)
    dark='#22363D';bronze='#89643F';light='#BE9359';shade='#4E4636';teal='#287C7C';glow='#80DACC'
    active=i%2
    if i<48:
        typ=(i-40)//2
        # feet, asymmetrical return tubing, calm core and plated housing
        for x in [93,128,161]:
            line([(128,183),(x,216),(x-5,223)],dark,13);line([(128,182),(x-2,214)],bronze,6)
        ellipse((94,193,164,218),shade);ellipse((96,190,162,207),bronze)
        width=[52,43,34,43][typ]; l=128-width/2;r=128+width/2
        line([(l,73),(l-24,97),(l-27,160),(l-8,177)],dark,12)
        line([(l-2,74),(l-23,98),(l-24,158),(l-7,176)],bronze,5)
        if typ==0:
            line([(r,88),(r+24,104),(r+26,155),(r,171)],shade,12)
            line([(r,88),(r+23,104),(r+24,154),(r,171)],light,3)
        rect((l-6,52,r+6,190),dark,12);rect((l,57,r,186),bronze,9)
        rect((l+8,72,r-7,162),dark,8);rect((l+12,77,r-11,157),teal,6)
        rect((l+13,80,l+17,150),'#54B4AE',2)
        for yy in [65,168,180]:
            rect((l-6,yy,r+6,yy+8),shade,2);line([(l-4,yy),(r+3,yy)],light,2)
        poly([(l-6,52),(l+1,36),(r-2,36),(r+8,52)],bronze)
        ellipse((l-5,43,r+6,57),light);ellipse((l+4,44,r-2,52),shade)
        line([(128,36),(128,15)],bronze,7);ellipse((123,8,133,19),light)
        if typ in [0,3]:
            cx=128 if typ==0 else 141
            ellipse((cx-24,96,cx+24,144),dark);ellipse((cx-21,98,cx+21,140),light)
            ellipse((cx-16,102,cx+16,136),shade);ellipse((cx-12,105,cx+12,132),teal)
            line([(cx-6,110),(cx-6,124)],glow,3)
        for yy in [61,174]:
            for xx in [l+2,r-2]:ellipse((xx-2,yy-2,xx+2,yy+2),'#D2B17A')
        if active:
            for xx,yy in [(98,211),(128,218),(158,211)]:ellipse((xx-3,yy-3,xx+3,yy+3),glow)
    else:
        # Isometric pressurised pipe, two collars, separate feet and return hose.
        flip=((i-48)//2)%2
        for x,y in [(67,164),(184,132)]:
            line([(x,y),(x+4,y+39),(x-10,y+48)],dark,16);line([(x-2,y),(x+1,y+36),(x-10,y+44)],bronze,7)
        line([(63,177),(73,209),(137,210),(178,177)],dark,10)
        line([(63,177),(73,206),(137,207),(178,177)],bronze,4)
        poly([(43,105),(176,55),(216,109),(81,164)],dark)
        poly([(53,105),(178,65),(207,109),(79,152)],teal)
        line([(66,111),(183,73)],'#68B3AC',5)
        line([(75,141),(201,101)],'#205C65',9)
        for cx,cy in [(60,132),(185,88)]:
            ellipse((cx-26,cy-44,cx+26,cy+44),dark)
            ellipse((cx-23,cy-42,cx+23,cy+40),bronze)
            ellipse((cx-19,cy-37,cx+18,cy+34),light)
            ellipse((cx-13,cy-30,cx+16,cy+30),shade)
            ellipse((cx-9,cy-24,cx+12,cy+24),dark)
            for ang in range(0,360,60):
                t=np.deg2rad(ang);x=cx+19*np.cos(t);y=cy+34*np.sin(t);ellipse((x-2,y-2,x+2,y+2),'#D0B282')
        for t in [.3,.65]:
            x=60+125*t;y=132-44*t;line([(x-9,y-32),(x+14,y+23)],bronze,8);line([(x-11,y-31),(x+12,y+22)],light,2)
        if active:ellipse((117,191,123,197),glow)
        if flip:im=im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    # Add broad cylindrical reflected light and asymmetric top-left illumination.
    ar=np.array(im).astype(float); yy,xx=np.mgrid[:768,:768];xx=xx/3;yy=yy/3
    if i<48:
        radius=[26,21.5,17,21.5][(i-40)//2]
        curved=.70+.38*np.sqrt(np.clip(1-((xx-124)/(radius+8))**2,0,1))
        shadefield=np.where((yy>57)&(yy<190)&(abs(xx-128)<radius+6),curved,1)
    else:
        axis=151-xx*.35
        curved=.64+.48*np.sqrt(np.clip(1-((yy-axis+8)/36)**2,0,1))
        shadefield=np.where((xx>80)&(xx<168)&(abs(yy-axis)<33),curved,1)
    ar[...,:3]*=shadefield[...,None];im=Image.fromarray(np.uint8(np.clip(ar,0,255)))
    # Match reference occupancy extents and exact lowest anchor, not its pixels.
    box=im.getbbox();im=im.crop(box)
    x0,y0,x1,y1=[v*2 for v in bbox]
    im=im.resize((x1-x0,y1-y0),Image.Resampling.LANCZOS)
    out=Image.new('RGBA',(256,256));out.alpha_composite(im,(x0,y0));return out


def main():
    ART.mkdir(parents=True,exist_ok=True)
    orig=np.array(Image.open(SRC/'basic_ground_tiles_original_backup.png').convert('RGBA'))
    v1=Image.open(BASE/'basic_ground_tiles.png').convert('RGBA')
    tex=[[texture(k,v) for v in range(3)] for k in range(3)]
    out=Image.new('RGBA',(2048,1792)); y,x=np.mgrid[:256,:256]
    # Correct inverse iso: top vertex (128,0), right (256,64), bottom (128,128).
    u=(x-128)/256+y/128;v=-(x-128)/256+y/128
    top=(u>=-.012)&(u<=1.012)&(v>=-.012)&(v<=1.012)
    # Extend top shading under the inherited alpha stair-step by ~1.5 screen pixels.
    # This removes dark tile-frame pixels; silhouette/alpha remains byte-identical.
    front=128-abs(x-128)*.5;depth=np.clip((y-front)/128,0,1)
    for i in range(40):
        rr,cc=divmod(i,8);a=orig[rr*128:(rr+1)*128,cc*128:(cc+1)*128]
        m,shell=source_mask(a);m=np.repeat(np.repeat(m,2,0),2,1);shell=np.repeat(np.repeat(shell,2,0),2,1)
        rgb=np.zeros((256,256,3),float)
        # Full-period sampling fixes the first review's half-period edge discontinuity.
        tx=((u*1024)%1024).astype(int);ty=((v*1024)%1024).astype(int)
        for k in range(3):
            color=tex[k][i%3][ty,tx].astype(float)
            # side layers retain chromatic family for valid independent full-cell classification
            side=np.zeros_like(color)+[(17,105,88),(91,110,113),(38,53,68)][k]
            fac=np.where(x<128,.84,.67)*(1-depth*.52)
            strata=1+0.045*np.sin(depth*20+np.sin(x*.023))
            side*=fac[...,None]*strata[...,None]
            # Broad sediment strata and sparse vertical root channels, not high-frequency noise.
            band=(np.cos(depth*19+np.sin(x*.037)*.3)>0.94)
            side*=np.where(band,.84,1)[...,None]
            if k==0:
                root=(np.sin(x*.19+depth*5)>0.97)&(depth<.38)
                side*=np.where(root,1.19,1)[...,None]
            color=np.where(top[...,None],color,side)
            rgb[m==k]=color[m==k]
        # Material contact shading adds shallow overlap without altering class ownership.
        opaque=np.repeat(np.repeat(a[...,3]>8,2,0),2,1)
        for k in [0,1]:
            # Shade real material contacts only, never alpha/tile frame boundaries.
            other=(m!=k)&opaque&top
            if not other.any(): continue
            dist=ndi.distance_transform_edt(~other)
            contact=(m==k)&(dist<2.8)&top
            rgb[contact]*=.87
        # source white inclusions become restrained shell shards (separate semantic label).
        # Keep source positions but turn most inclusions into nearly buried blue-gray grains.
        # Deterministic component selection: only one in four retains modest contrast.
        lab,count=ndi.label(shell)
        for component in range(1,count+1):
            pick=(lab==component)&top
            strength=.55 if (component+i)%4==0 else .12
            rgb[pick]=rgb[pick]*(1-strength)+np.array([61,79,94])*strength
        alpha=np.repeat(np.repeat(a[...,3],2,0),2,1)
        cell=np.dstack([np.clip(rgb,0,255).astype('uint8'),alpha]);out.paste(Image.fromarray(cell),(cc*256,rr*256))
    for i in range(40,56):
        rr,cc=divmod(i,8);bbox=v1.crop((cc*128,rr*128,cc*128+128,rr*128+128)).getbbox()
        out.paste(prop(i,bbox),(cc*256,rr*256))
    out.save(SRC/'basic_ground_tiles.png')
    meta=(BASE/'basic_ground_tiles.png.meta').read_text()
    meta=re.sub(r'(      rect:\n        serializedVersion: 2\n)(        x: (\d+)\n        y: (\d+)\n        width: (\d+)\n        height: (\d+))',lambda q:q[1]+''.join(f'        {key}: {int(q[n])*2}\n' for key,n in [('x',3),('y',4),('width',5),('height',6)]).rstrip(),meta)
    meta=meta.replace('spritePixelsToUnits: 128','spritePixelsToUnits: 256').replace('filterMode: 0','filterMode: 1')
    meta=re.sub(r'textureCompression: \d+','textureCompression: 0',meta);meta=meta.replace('compressionQuality: 50','compressionQuality: 100')
    (SRC/'basic_ground_tiles.png.meta').write_text(meta)
    # Reopen actual deliverable for independent reverse classification.
    final=np.array(Image.open(SRC/'basic_ground_tiles.png'))
    rows=[]
    for i in range(40):
        rr,cc=divmod(i,8);a=orig[rr*128:(rr+1)*128,cc*128:(cc+1)*128];m,shell=source_mask(a)
        m=np.repeat(np.repeat(m,2,0),2,1);shell=np.repeat(np.repeat(shell,2,0),2,1)
        b=final[rr*256:(rr+1)*256,cc*256:(cc+1)*256];r,g,bl=b[...,:3].astype(float).transpose(2,0,1)
        # Palette reverse-classifier uses ONLY final RGB. Soil is blue, stone near-neutral,
        # kelp has strong green/red ratio. No destination mask is used in prediction.
        pred=np.full(m.shape,2,np.uint8);pred[(bl-g)<=g*.12]=1;pred[(g>r*1.55)&(g>bl*1.08)]=0
        valid=np.repeat(np.repeat(a[...,3]>8,2,0),2,1);na=b[...,3]>8
        score=(pred[valid]==m[valid]).mean(); validtop=valid&top
        rows.append(dict(cell=i,alpha_iou=float((valid&na).sum()/(valid|na).sum()),material_accuracy=float(score),top_accuracy=float((pred[validtop]==m[validtop]).mean()),shell_pixels=int((shell&valid).sum()),shell_aware_accuracy=float(((pred==m)|shell)[valid].mean())))
    with (ART/'per_cell_validation.csv').open('w',newline='') as f:
        w=csv.DictWriter(f,fieldnames=rows[0].keys());w.writeheader();w.writerows(rows)
    old=(BASE/'basic_ground_tiles.png.meta').read_text()
    identities=lambda t:re.findall(r'^\s*(?:guid|name|spriteID|internalID|pivot|spritePivot):.*$',t,re.M)
    identity_ok=identities(old)==identities(meta)
    glow=(final[...,1]>190)&(final[...,2]>150)&(final[...,0]<150)&(final[...,3]>8)
    result={'method':'deterministic procedural mask-driven repaint, 3 seamless texture variants per material, newly vector-authored props; no generative AI', 'size':list(out.size),'all_40_alpha_pass':all(q['alpha_iou']>=.98 for q in rows),'all_40_material_pass':all(q['material_accuracy']>=.95 for q in rows),'identity_and_pivot_unchanged':identity_ok,'glow_fraction_opaque':float(glow.sum()/(final[...,3]>8).sum()),'rows':rows,'sha256':hashlib.sha256((SRC/'basic_ground_tiles.png').read_bytes()).hexdigest()}
    (ART/'validation.json').write_text(json.dumps(result,indent=2))
    # Same 128px display sizes; checkerless calm backdrop, not engine screenshots.
    sheet=Image.new('RGB',(2048,930),'#17252C');sheet.paste(v1,(0,34),v1);small=out.resize((1024,896),Image.Resampling.LANCZOS);sheet.paste(small,(1024,34),small)
    d=ImageDraw.Draw(sheet);d.text((20,10),'V1 reference | 128px per cell',fill='white');d.text((1044,10),'HD procedural repaint | downsampled to 128px',fill='white');sheet.save(ART/'comparison_128px.png')
    def mosaic(grid,name):
        n=len(grid);canvas=Image.new('RGBA',(n*256+64,n*128+256),'#17252C')
        for s in range(n*2-1):
            for j in range(n):
                k=s-j
                if not 0<=k<n:continue
                i=grid[j][k];tile=out.crop(((i%8)*256,(i//8)*256,(i%8+1)*256,(i//8+1)*256))
                canvas.alpha_composite(tile,(int((k-j)*128+(n-1)*128+32),int((j+k)*64+16)))
        canvas.convert('RGB').save(ART/name)
    for i in [0,2,3]:mosaic([[i]*3 for _ in range(3)],f'seam_3x3_cell_{i}.png')
    # Transition selection derived from original material edge agreement, not random placement.
    masks=[]
    for i in range(40):
        a=orig[(i//8)*128:(i//8+1)*128,(i%8)*128:(i%8+1)*128];m,_=source_mask(a)
        edge=[]
        for side in range(4):
            t=np.linspace(.06,.94,48); uu=t if side in [0,2] else np.full(48,0 if side==3 else 1);vv=t if side in [1,3] else np.full(48,0 if side==0 else 1)
            xx=np.clip((64+64*(uu-vv)).astype(int),0,127);yy=np.clip((32*(uu+vv)).astype(int),0,127);edge.append(m[yy,xx])
        masks.append(edge)
    grid=[]
    for j in range(5):
        row=[]
        for k in range(5):
            target=0 if k<2 else (1 if k<4 else 2);best=None
            for i in range(40):
                mismatch=0
                if k:mismatch+=np.mean(masks[row[-1]][1]!=masks[i][3])*4
                if j:mismatch+=np.mean(masks[grid[j-1][k]][2]!=masks[i][0])*4
                a=orig[(i//8)*128:(i//8+1)*128,(i%8)*128:(i%8+1)*128];mm,_=source_mask(a)
                mismatch+=1-np.mean(mm[:64]==target)
                if best is None or mismatch<best[0]:best=(mismatch,i)
            row.append(best[1])
        grid.append(row)
    # Global relaxation: reserve actual half-transition columns; greedy rows alone
    # produced misleading hard stripes in the first visual review.
    yy,xx=np.mgrid[:128,:128];tt=(yy>abs(xx-64)/2+2)&(yy<64-abs(xx-64)/2-2)
    coverage=[]
    for i in range(40):
        aa=orig[(i//8)*128:(i//8+1)*128,(i%8)*128:(i%8+1)*128]
        mm,_=source_mask(aa);coverage.append(np.bincount(mm[tt],minlength=3)/tt.sum())
    desired=np.array([[1,0,0],[.54,.46,0],[0,1,0],[0,.33,.67],[0,0,1]])
    unary=np.array([[np.abs(cov-des).sum()*4 for cov in coverage] for des in desired])
    grid=[[int(np.argmin(unary[k])) for k in range(5)] for j in range(5)]
    for iteration in range(12):
        for j in range(5):
            for k in range(5):
                costs=unary[k].copy()
                for i in range(40):
                    if k:costs[i]+=np.mean(masks[grid[j][k-1]][1]!=masks[i][3])
                    if k<4:costs[i]+=np.mean(masks[i][1]!=masks[grid[j][k+1]][3])
                    if j:costs[i]+=np.mean(masks[grid[j-1][k]][2]!=masks[i][0])
                    if j<4:costs[i]+=np.mean(masks[i][2]!=masks[grid[j+1][k]][0])
                grid[j][k]=int(np.argmin(costs))
    mosaic(grid,'transition_5x5.png');(ART/'preview_grid.json').write_text(json.dumps(grid))
    print(json.dumps({k:v for k,v in result.items() if k!='rows'},indent=2));print('MIN MATERIAL',min(r['material_accuracy'] for r in rows))
if __name__=='__main__':main()
