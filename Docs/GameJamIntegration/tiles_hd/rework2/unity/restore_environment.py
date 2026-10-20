"""Archive actual tested environment, then restore ONLY proven automatic files."""
from pathlib import Path
import subprocess,json,hashlib
root=Path(__file__).resolve().parents[5];u=Path(__file__).parent
sha=lambda b:hashlib.sha256(b).hexdigest()
manifest=json.loads((u/'baseline-manifest.json').read_text())
# Run only after supervised build/player exit. Refuse if a target-workspace Editor/player remains.
ps=subprocess.check_output(['powershell.exe','-NoProfile','-Command',"Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'Unity|TapTap' } | Select-Object ProcessId,Name,CommandLine | ConvertTo-Json -Depth 3"],encoding='utf-8')
processes=json.loads(ps) if ps.strip() else [];processes=[processes] if isinstance(processes,dict) else processes
assert not [p for p in processes if p['Name'].lower() in ['unity.exe','taptap26.exe'] and 'tiles_hd' in (p.get('CommandLine') or '').lower()]
result={}
for rel in ['ProjectSettings/ProjectVersion.txt','Packages/packages-lock.json','Assets/Scenes/Mix.unity']:
 p=root/rel;base=(u/'baseline'/p.name).read_bytes()
 assert base==subprocess.check_output(['git','show','879645d:'+rel],cwd=root)
 assert sha(base)==manifest[rel]['initial_sha256']==manifest[rel]['baseline_sha256']
 current=p.read_bytes();archive=u/('tested-'+p.name)
 if archive.exists():assert archive.read_bytes()==current,'Refuse to replace tested environment with restored bytes'
 else:archive.write_bytes(current)
 # Mix may be unchanged: avoid touching it needlessly.
 if current!=base:p.write_bytes(base)
 result[rel]=dict(initial_sha256=manifest[rel]['initial_sha256'],tested_sha256=sha(current),restored_sha256=sha(p.read_bytes()),restored_matches_initial=sha(p.read_bytes())==manifest[rel]['initial_sha256'],was_automatically_changed=current!=base,tested_archive=archive.name,restored_environment_rebuilt=False)
(u/'automatic-files-restored.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
