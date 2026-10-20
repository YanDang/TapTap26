"""Strict revision-bound closeout. Never writes the main report or performs git operations."""
from pathlib import Path
import json,re,subprocess,hashlib
from PIL import Image
root=Path(__file__).resolve().parents[5];u=Path(__file__).parent;r=u.parent
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
(u/'closeout-result.json').write_text(json.dumps({'technical_passed':False,'status':'checking','art_approved':False}))
try:
 assert subprocess.check_output(['git','branch','--show-current'],cwd=root,text=True).strip()=='feature/hd-ground-tiles'
 ready=load(r/'art/READY.json')
 for f,d in ready['production_pngs'].items():assert sha(root/f)==d['sha256'],f
 exits={}
 labels=['before','repaint-final','repaint-final-repeat','after-final','build']
 for label in labels:
  data=load(u/(label+'-supervisor-result.json'))
  assert type(data.get('exit_code')) is int and data['exit_code']==0,label
  assert data.get('editor_restored_before_supervisor') is True and data.get('raw_restored_after_exit') is True,label
  assert not any('error' in k.lower() and v for k,v in data.items()),label
  exits[label]=data['exit_code']
 p=load(u/'selftest/supervisor-result.json')
 assert type(p.get('exit_code')) is int and p['exit_code']==0
 assert p.get('player_restored_before_supervisor') is True and p.get('post_exit_raw_restore') is True
 assert not any('error' in k.lower() and v for k,v in p.items())
 test=(u/'selftest/selftest.txt').read_text(encoding='utf-8');assert 'RESULT passed=534 failed=0' in test
 for phase in ['before','after']:
  assert 'GPU capture completed; editor raw preferences restored=True' in (u/f'record-{phase}-result.txt').read_text()
  for view in ['panorama','closeup']:
   with Image.open(u/f'BiomechanicalCombatScene-{phase}-{view}.png') as im:assert im.size==(1920,1080)
 assert 'unique=1' in (u/'texture-batches-after.txt').read_text()
 decor=(u/'decor-runtime-after.txt').read_text().splitlines()
 assert len(decor)==15 and sum(s.startswith('PathLamp_') for s in decor)==6 and sum(s.startswith('EdgePipe_') for s in decor)==9
 assert all('sorting=Default:1' in s for s in decor)
 assert 'changed=0' in (u/'repaint-result.txt').read_text()
 scene=root/'Assets/Scenes/BiomechanicalCombatScene.unity'
 assert scene.read_bytes()==(u/'repaint-accepted.unity.txt').read_bytes()
 assert '[TapWindowsBuild] Succeeded errors=0' in (u/'build.log').read_text(encoding='utf-8')
 protection=load(u/'scene-protection.json');integration=load(u/'integration-protection.json')
 assert protection['passed'] is True and protection['issues']==[] and integration['passed'] is True and integration['issues']==[]
 assert integration['lamp_count']==6 and integration['pipe_count']==9 and integration['decor_renderers_enabled_with_valid_sprite_and_order'] is True
 assert protection['final_scene_sha256']==sha(scene)
 for g in protection['geometry']:
  assert g['passed'] is True and g['origin_unchanged'] is True and g['size_unchanged'] is True
  assert g['before']==g['after']==698 and g['added']==g['removed']==[]
 errors=[]
 for f in [*(label+'.log' for label in labels),'selftest/player.log']:
  for line in (u/f).read_text(encoding='utf-8',errors='replace').splitlines():
   if re.search(r'error CS\d+|\b\w*Exception:|\bFAIL\b|RESULT.*failed=[1-9]',line):errors.append([f,line])
 assert not errors,errors
 restored=load(u/'automatic-files-restored.json')
 assert all(v['restored_matches_initial'] is True for v in restored.values())
 ps=subprocess.check_output(['powershell.exe','-NoProfile','-Command',"Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'Unity|TapTap' } | Select-Object ProcessId,Name,CommandLine | ConvertTo-Json -Depth 3"],encoding='utf-8')
 processes=json.loads(ps) if ps.strip() else [];processes=[processes] if isinstance(processes,dict) else processes
 (u/'process-audit.json').write_text(json.dumps(processes,ensure_ascii=False,indent=2),encoding='utf-8')
 owned=[p for p in processes if p['Name'].lower() in ['unity.exe','taptap26.exe','unitycrashhandler64.exe'] and ('tiles_hd' in (p.get('CommandLine') or '').lower() or 'Rework2' in (p.get('CommandLine') or ''))]
 # All jobs were sequential and their supervised children exited. Unknown processes never terminated.
 assert not owned
 files=[root/f for f in ready['production_pngs']]+[scene,root/'Tools/run_tap_selftest.py']
 files+=list((root/'Assets/Editor/TilesHD').glob('*'))+list((root/'Assets/Art/Tiles/Test/TilesHDVariants').glob('*'))+list((root/'Assets/Pic/Test/TilesHDVariants').glob('*.meta'))
 files+=list((root/'Builds/Rework2/Windows').rglob('*'))
 files+=list(u.glob('*.png'))+list(u.glob('*.log'))+list(u.glob('tested-*'))+list(u.glob('*.py'))
 files+=[u/f for f in ['repaint-result.txt','repaint-final-result.txt','repaint-accepted.unity.txt','decor-source-references.txt','decor-source-references-first.txt','decor-runtime-after.txt','texture-batches-after.txt','automatic-files-restored.json','process-audit.json','visual-review.md']]
 files+=[u/'selftest/selftest.txt',u/'selftest/player.log',u/'selftest/target-after.png',u/'selftest/supervisor-result.json',u/'scene-protection.json',u/'integration-protection.json']
 hashes={f.relative_to(root).as_posix():sha(f) for f in files if f.is_file() and f.name!='tested-artifact-hashes.json'}
 (u/'tested-artifact-hashes.json').write_text(json.dumps(hashes,indent=2))
 result=dict(technical_passed=True,art_approved=False,baseline='879645d',editor_exits=exits,player_exit=0,selftest='RESULT passed=534 failed=0',atlas_sha256=ready['atlas_sha256'],scene_sha256=sha(scene),repeat_changed=0,repeat_scene_bytes_identical=True,detected_error_lines=errors,editor_original_prefix_count=len(load(u/'after-final-supervisor-before.json')),player_original_prefix_count=p['original_TAP_count'],owned_processes_remaining=owned,actual_unity=(u/'tested-ProjectVersion.txt').read_text().strip(),restored_declared_unity=(root/'ProjectSettings/ProjectVersion.txt').read_text().strip(),restored_environment_rebuilt=False,shared_sprite_atlas=True,raw_prefs_restored=True,scene_protection_passed=True,integration_protection_passed=True)
 (u/'closeout-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(result,ensure_ascii=False,indent=2))
except Exception as e:
 (u/'closeout-result.json').write_text(json.dumps(dict(technical_passed=False,status='failed',error=repr(e),art_approved=False),indent=2));raise
