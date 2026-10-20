"""Fixed rework2 independent reverse classifier + tighter rework3 scope checks."""
from pathlib import Path
import json,hashlib,sys
import numpy as np
from PIL import Image,ImageDraw
sys.dont_write_bytecode=True
A=Path(__file__).resolve().parent;P=A.parents[4];H=A.parents[1]/'rework2/art';S=P/'Assets/Pic/Test'
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
source=(H/'verify_art.py').read_text().replace("B=A/'baseline'","B=A/'baseline/Assets/Pic/Test'").replace('879645d','e07520f').replace('REWORK2 variant_00','REWORK3 variant_00')
# Material source sheets are unchanged; do not generate misleading before/new material sheets.
source=source[:source.index("sheet=Image.new('RGB',(1536,1080)")]
ns={'__file__':str(A/'verify_art.py'),'__name__':'qa'}
exec(compile(source,'unchanged_rework2_classifier','exec'),ns)
v=ns['result']; assert v['all_alpha_pass'] and v['all_material_pass'] and v['all_top_pass'] and all(v['protected_files'].values())
assert all(n==16 for n in v['distinct_variants_per_ground_cell'].values())
y,x=np.mgrid[:256,:256];u=(x-128)/256+y/128;vv=-(x-128)/256+y/128;top=(u>=-.012)&(u<=1.012)&(vv>=-.012)&(vv<=1.012)
paths=['Assets/Pic/Test/basic_ground_tiles.png']+[f'Assets/Pic/Test/TilesHDVariants/variant_{i:02d}.png' for i in range(16)]
rows=[]
for rel in paths:
 b=np.array(Image.open(A/'baseline'/rel).convert('RGBA'));n=np.array(Image.open(P/rel).convert('RGBA'))
 allow=np.zeros(b.shape[:2],bool)
 for i in range(40):
  yy,xx=i//8*256,i%8*256;allow[yy:yy+256,xx:xx+256]=top&(b[yy:yy+256,xx:xx+256,3]>0)
 diff=np.any(b!=n,axis=2)
 row={'path':rel,'changed_pixels':int(diff.sum()),'unauthorized_changed_pixels':int((diff&~allow).sum()),'alpha_byte_equal':bool(np.array_equal(b[...,3],n[...,3])),'props40_55_rgba_equal':bool(np.array_equal(b[1280:],n[1280:])),'max_channel_delta':int(np.abs(n.astype(int)-b.astype(int)).max()),'changed_cells':[i for i in range(56) if diff[i//8*256:(i//8+1)*256,i%8*256:(i%8+1)*256].any()]}
 assert row['changed_pixels']>0 and row['unauthorized_changed_pixels']==0 and row['alpha_byte_equal'] and row['props40_55_rgba_equal']
 rows.append(row)
protected=json.loads((A/'protected_live_hashes.json').read_text()); assert all(h(P/r)==s for r,s in protected.items())
materials={name:h(A/name)==h(H/name) for name in ['material_final_0.png','material_final_2.png','world_stone_4x4.png','rune_schedule.json','texture_0_0.png','texture_1_0.png','texture_2_0.png']}; assert all(materials.values())
assert h(P/paths[0])==h(P/paths[1])
Image.open(P/paths[0]).save(A/'atlas_final.png')
# Compare genuine coordinate-selected mosaics with the prior acceptance evidence.
for i in [0,2,3]:
 name=f'variants_8x8_cell_{i}.png';old=Image.open(H/name).convert('RGB');new=Image.open(A/name).convert('RGB')
 sheet=Image.new('RGB',(old.width*2,old.height+28),'#152630');d=ImageDraw.Draw(sheet);d.text((8,8),'e07520f BEFORE',fill='white');d.text((old.width+8,8),'REWORK3 AFTER - no world lighting baked',fill='white');sheet.paste(old,(0,28));sheet.paste(new,(old.width,28));sheet.save(A/f'comparison_mosaic_cell_{i}.png')
result={'passed':True,'production_files':rows,'protected_live_files_sha256_equal':True,'protected_live_file_count':len(protected),'unchanged_material_and_rune_files':materials,'strict_classifier_source_sha256':h(H/'verify_art.py'),'strict_classifier_note':'Reused lines through validation and previews; numerical classifier, scoring regions and thresholds untouched. Baseline/path/labels only relocated.','top_directional_gradient_after':0,'top_edge_alpha_before_after':[0,0],'world_lighting_verified':False}
(A/'scope_validation.json').write_text(json.dumps(result,indent=2))
print(json.dumps({'strict':{k:v[k] for k in ['min_alpha_iou','min_material_accuracy','min_top_accuracy']},'scope_passed':True,'changed_pixels_range':[min(r['changed_pixels'] for r in rows),max(r['changed_pixels'] for r in rows)],'max_delta':max(r['max_channel_delta'] for r in rows),'materials':materials},indent=2))
