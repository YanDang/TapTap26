from pathlib import Path
import subprocess,json,hashlib,shutil
root=Path(__file__).resolve().parents[5]; out=Path(__file__).parent; base=out/'baseline';base.mkdir(exist_ok=True)
sha=lambda b:hashlib.sha256(b).hexdigest()
ps=subprocess.check_output(['powershell.exe','-NoProfile','-Command',"Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'Unity|TapTap' } | Select-Object ProcessId,Name,CommandLine | ConvertTo-Json -Depth 3"],encoding='utf-8')
(out/'process-before.json').write_text(ps or '[]',encoding='utf-8');print(ps)
proc=json.loads(ps) if ps.strip() else [];proc=[proc] if isinstance(proc,dict) else proc
assert not [p for p in proc if p['Name'].lower() in ['unity.exe','taptap26.exe']], 'Existing Unity/player: do not launch'
files=['Assets/Scenes/BiomechanicalCombatScene.unity','Assets/Pic/Test/basic_ground_tiles.png','Assets/Pic/Test/basic_ground_tiles.png.meta','Assets/Pic/Test/basic_ground_tiles_original_backup.png','ProjectSettings/ProjectVersion.txt','Packages/packages-lock.json','Assets/Scenes/Mix.unity']
manifest={}
for f in files:
 b=subprocess.check_output(['git','show','879645d:'+f],cwd=root);target=base/Path(f).name
 if target.exists():assert target.read_bytes()==b
 else:target.write_bytes(b)
 manifest[f]={'baseline_sha256':sha(b),'initial_sha256':sha((root/f).read_bytes()),'initial_matches_baseline_eol':(root/f).read_bytes().replace(b'\r\n',b'\n')==b.replace(b'\r\n',b'\n')}
assert manifest[files[0]]['initial_matches_baseline_eol']
# Normalize immutable scene copy to the already-clean worktree EOL for fail-closed recorder equality.
assert (root/files[0]).read_bytes()==(base/'BiomechanicalCombatScene.unity').read_bytes()
ready=json.loads((out.parent/'art/READY.json').read_text())
for f,d in ready['production_pngs'].items():assert sha((root/f).read_bytes())==d['sha256'],f
(out/'baseline-manifest.json').write_text(json.dumps(manifest,indent=2))
(out/'production-hash-gate.json').write_text(json.dumps({'passed':True,'production_pngs':ready['production_pngs']},indent=2))
shutil.copyfile(out.parents[1]/'rework1/unity/run_editor.py',out/'run_editor.py')
(out/'handoff.md').write_text('# 返工2 Unity交接（进行中）\n基线879645d；17张生产PNG hash已匹配READY。未提交/未push；主报告不改。\n待完成before、重铺、after、构建、自测、独立验证、自动环境恢复。\n',encoding='utf-8')
print('BASELINE AND 17 PNG HASHES VERIFIED')
