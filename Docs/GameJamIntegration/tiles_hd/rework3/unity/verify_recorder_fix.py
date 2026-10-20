"""Bounded Before/After repair verification. Never writes production PNGs; restores scene in finally."""
from pathlib import Path
import subprocess,sys,json,hashlib
R=Path(__file__).resolve().parents[5];O=Path(__file__).parent;D=O/'recorder-fix'
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
D.mkdir(exist_ok=True)
assert not (D/'verification.json').exists(), 'Keep prior evidence; choose new directory for reruns'
scene=R/'Assets/Scenes/BiomechanicalCombatScene.unity';original=scene.read_bytes()
assert h(scene)=='541eb0f0d250a05b33acc1e9d528a854a5ce38eb29eb854a5a0058f59582407e'
(D/'final-scene.backup').write_bytes(original)
protected={p.relative_to(R).as_posix():h(p) for p in (R/'Assets').rglob('*') if p.is_file() and p!=scene}
images={p.relative_to(R).as_posix():h(p) for p in O.rglob('*.png')}
source=R/'Assets/Editor/TilesHD/RecordMap.cs'
(D/'RecordMap-tested.cs.txt').write_bytes(source.read_bytes())
(D/'RecordMap-original.cs.txt').write_bytes(subprocess.check_output(['git','show','HEAD:Assets/Editor/TilesHD/RecordMap.cs'],cwd=R))
config={p:(R/p).read_bytes() for p in ['ProjectSettings/ProjectVersion.txt','Packages/packages-lock.json']}
results={};error=None
try:
 baseline=(O/'baseline/BiomechanicalCombatScene.unity').read_bytes()
 assert baseline.replace(b'\r\n',b'\n')==subprocess.check_output(['git','show','e07520f:Assets/Scenes/BiomechanicalCombatScene.unity'],cwd=R).replace(b'\r\n',b'\n')
 scene.write_bytes(baseline)
 try:
  results['before']=subprocess.call([sys.executable,str(O/'run_editor.py'),'TilesHD.RecordMap.Before','recorder-fix/before','-tilesHDRecordOutput',str(D)],cwd=R)
 finally:scene.write_bytes(original)
 assert results['before']==0, results
 results['after']=subprocess.call([sys.executable,str(O/'run_editor.py'),'TilesHD.RecordMap.After','recorder-fix/after','-tilesHDRecordOutput',str(D)],cwd=R)
 assert results['after']==0, results
except Exception as e:error=repr(e)
finally:
 scene.write_bytes(original)
 for p,b in config.items():
  (D/(Path(p).name+'.tested')).write_bytes((R/p).read_bytes());(R/p).write_bytes(b)
 asset_diff=[p for p,v in protected.items() if not (R/p).exists() or h(R/p)!=v]
 image_diff=[p for p,v in images.items() if h(R/p)!=v]
 result=dict(passed=error is None and not asset_diff and not image_diff,exit_codes=results,error=error,scene_sha256=h(scene),protected_asset_differences=asset_diff,existing_image_differences=image_diff,editor_source_sha256=h(source),original_tested_source_manifest_sha256=h(O/'tested-source-sha256.json'),environment_restored=all((R/p).read_bytes()==b for p,b in config.items()),note='Original 534/build tested-source manifest retained. Only Editor recorder changed; fresh Before and After compile/PlayMode/GPU evidence supplement unchanged player production content.')
 (D/'verification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
 print(json.dumps(result));assert result['passed'],result
