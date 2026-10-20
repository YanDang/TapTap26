"""After all owned Editor/player jobs exit: archive tested config then restore ONLY proven auto-edits."""
from pathlib import Path
import subprocess,json,hashlib,importlib.util,datetime
R=Path(__file__).resolve().parents[5];O=Path(__file__).parent
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def save(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2),encoding='utf-8')
ps="Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'Unity|TapTap26' } | Select-Object ProcessId,ParentProcessId,Name,CommandLine | ConvertTo-Json -Depth 4"
s=subprocess.check_output(['powershell.exe','-NoProfile','-Command',ps],text=True,encoding='utf-8').strip()
processes=json.loads(s) if s else []
if isinstance(processes,dict):processes=[processes]
owned=[p for p in processes if p['Name'].lower() in ('unity.exe','taptap26.exe') and 'tiles_hd' in (p['CommandLine'] or '').lower()]
audit=dict(timestamp_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),owned_active=owned,observed_unity_related=processes,passed=not owned,completed_sessions={'proc_48d46f21bb1a':0,'proc_8bbc00b450d5':0,'proc_51e7cab708ba':0},note='Session exits independently polled by agent; process inventory is live. Shared licensing/MCP services are not killed.')
save(O/'process-release.json',audit);assert not owned
spec=importlib.util.spec_from_file_location('rawprefs',R/'Tools/run_tap_selftest.py');prefs=importlib.util.module_from_spec(spec);spec.loader.exec_module(prefs)
results={}
for label,namespace,journal in [('editor',r'Software\Unity\UnityEditor\瓦卡棱镜\螺壳海底游记','repaint-v1-supervisor-before.json'),('player',r'Software\瓦卡棱镜\螺壳海底游记','selftest/supervisor-prefs-before.json')]:
 prefs.REG_PATH=namespace;original=json.loads((O/journal).read_text(encoding='utf-8'));current=prefs.capture()
 results[label]=dict(original_count=len(original),matches_original_raw=current==original,supervisor_recovery_needed=False)
 assert current==original, label+' raw prefs differs from original: investigate before overwriting'
save(O/'final-prefs-audit.json',dict(passed=True,namespaces=results,note='Native registry type/raw bytes and existence for TAP_, GJ_UltChoice, GJ_GEAR_ prefixes; real player namespace initially empty. Synthetic raw-type test is separate.'))
D=O/'tested-environment';D.mkdir(exist_ok=True)
# Freeze hashes of actual tested asset/script/config bytes before restoring auto config.
manifest={}
for folder in ['Assets','Packages','ProjectSettings','Tools']:
 for p in (R/folder).rglob('*'):
  if p.is_file() and '__pycache__' not in p.parts:manifest[p.relative_to(R).as_posix()]=h(p)
manifest_path=O/'tested-source-sha256.json'
if not manifest_path.exists():save(manifest_path,manifest)
else:
 prior=json.loads(manifest_path.read_text())
 fix=json.loads((O/'recorder-fix/verification.json').read_text());ep='Assets/Editor/TilesHD/RecordMap.cs'
 assert fix['passed'] is True and manifest[ep]==fix['editor_source_sha256']==h(O/'recorder-fix/RecordMap-tested.cs.txt')
 assert h(manifest_path)==fix['original_tested_source_manifest_sha256']
 assert all(manifest[k]==v for k,v in prior.items() if k not in ('ProjectSettings/ProjectVersion.txt','Packages/packages-lock.json',ep))
rows=[]
for rel in ['ProjectSettings/ProjectVersion.txt','Packages/packages-lock.json']:
 p=R/rel;b=O/'baseline'/p.name;git=subprocess.check_output(['git','show','e07520f:'+rel],cwd=R)
 assert b.read_bytes().replace(b'\r\n',b'\n')==git.replace(b'\r\n',b'\n')
 archived=D/p.name
 if not archived.exists():archived.write_bytes(p.read_bytes())
 row=dict(path=rel,tested_sha256=h(archived),baseline_sha256=h(b),restored_from_preexecution_baseline=True)
 if p.read_bytes()!=b.read_bytes():p.write_bytes(b.read_bytes())
 row['current_sha256']=h(p);assert row['current_sha256']==row['baseline_sha256'];rows.append(row)
mix='Assets/Scenes/Mix.unity';assert h(R/mix)==h(O/'baseline/Mix.unity')
save(O/'environment-restoration.json',dict(passed=True,files=rows,tested_editor='E:/unity/Downloads/Editor/Unity.exe',tested_version=(D/'ProjectVersion.txt').read_text(),restored_version=(R/'ProjectSettings/ProjectVersion.txt').read_text(),restored_environment_rebuilt=False,unrelated_Mix_unchanged=True,note='Only the two known automatic environment edits restored; no blanket checkout/clean. Build certifies archived tested environment, NOT a rebuild under restored 2022.3.62f3c1.'))
print(json.dumps({'process_release':True,'prefs':results,'environment_restored':True},ensure_ascii=False))
