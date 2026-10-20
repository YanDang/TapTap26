from pathlib import Path
import subprocess,json,hashlib,shutil
R=Path(__file__).resolve().parents[5]; O=Path(__file__).parent; B=O/'baseline'; B.mkdir(exist_ok=True)
h=lambda b:hashlib.sha256(b).hexdigest()
ready=json.loads((O.parent/'art/READY.json').read_text())
checks={p:h((R/p).read_bytes())==v['sha256'] for p,v in ready['production_pngs'].items()}
for n in ['handoff','manifest','validation','scope_validation']:
 p=O.parent/'art'/(n+('.md' if n=='handoff' else '.json'));checks[n]=h(p.read_bytes())==ready[n+'_sha256']
assert all(checks.values());(O/'production-hash-gate.json').write_text(json.dumps(checks,indent=2))
paths=subprocess.check_output(['git','ls-files'],cwd=R,text=True).splitlines(); protected={}
for p in paths:
 if p.startswith(('Assets/','ProjectSettings/','Packages/','Tools/')):protected[p]=h((R/p).read_bytes())
(O/'initial-hashes.json').write_text(json.dumps(protected,indent=2))
for p in ['Assets/Scenes/BiomechanicalCombatScene.unity','Assets/Scenes/Mix.unity','ProjectSettings/ProjectVersion.txt','Packages/packages-lock.json']:
 raw=subprocess.check_output(['git','show','e07520f:'+p],cwd=R); live=(R/p).read_bytes(); assert raw.replace(b'\r\n',b'\n')==live.replace(b'\r\n',b'\n')
 (B/Path(p).name).write_bytes(live)
shutil.copyfile(O.parents[1]/'rework2/unity/run_editor.py',O/'run_editor.py')
(O/'handoff.md').write_text('# 返工3 Unity交接（执行中）\n已检查e07520f及全部READY哈希。尚未宣称实现/测试通过。禁止提交；不改总报告。\n',encoding='utf-8')
print('READY strict hash gate PASS; initial protection manifest archived')
