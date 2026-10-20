from pathlib import Path
root=Path(__file__).resolve().parents[5];out=Path(__file__).parent
s=(out.parents[1]/'rework1/unity/verify_scene.py').read_text(encoding='utf-8')
s=s.replace("base=out.parent/'baseline'","base=out/'baseline'")
s=s.replace("expected={(-3,-12,0),(16,4,0),(16,6,0)} if bm['path']=='Grid/Tilemap' else set()","expected=set()")
# Background is protected too in this revision: eliminate old color exception.
a=s.index(' if t==20:');b=s.index(" issues.append('changed protected",a);s=s[:a]+s[b:]
(out/'verify_scene.py').write_text(s,encoding='utf-8')
print('Independent validator adapted: no occupancy additions and no camera exception')
