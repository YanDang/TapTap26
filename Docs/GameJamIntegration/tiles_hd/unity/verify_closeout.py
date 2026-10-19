"""Read-only final artifact/process audit; writes evidence only in this directory."""
from pathlib import Path
import hashlib, json, re, subprocess
from PIL import Image
root=Path(__file__).resolve().parents[4]
out=Path(__file__).parent
# A failed rerun must not leave a stale successful verdict.
(out/'final-verification.json').unlink(missing_ok=True)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def records(p):
    text=p.read_text(encoding='utf-8-sig')
    return {m.group(2):(m.group(1),m.group(3)) for m in re.finditer(r'^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S)}
a=records(out/'BiomechanicalCombatScene.before.unity.txt')
b=records(root/'Assets/Scenes/BiomechanicalCombatScene.unity')
changed=[{'fileID':k,'classID':a[k][0]} for k in a if k in b and a[k]!=b[k]]
old=json.loads((out/'tilemap-before.json').read_text())['maps'][0]
new=json.loads((out/'tilemap-after.json').read_text())['maps'][0]
positions=lambda m:{tuple(c['position'][v] for v in ('x','y','z')) for c in m['cells']}
ps=subprocess.run(['powershell.exe','-NoProfile','-Command',"@(Get-CimInstance Win32_Process | Where-Object {$_.Name -match '^(Unity|UnityCrashHandler64|TapTap26)\\.exe$'} | Select-Object ProcessId,Name,CommandLine) | ConvertTo-Json -Depth 3"],capture_output=True,text=True,check=True)
procs=json.loads(ps.stdout) if ps.stdout.strip() else []
if isinstance(procs,dict):procs=[procs]
images=[{'file':str(p.relative_to(out)),'size':list(Image.open(p).size),'sha256':sha(p)} for p in sorted(out.rglob('*.png')) if 'baseline_source' not in str(p)]
scene_sha=sha(root/'Assets/Scenes/BiomechanicalCombatScene.unity')
texture_sha=sha(root/'Assets/Pic/Test/basic_ground_tiles.png')
result={
 'scene_sha256':scene_sha,
 'idempotent_scene_bytes':scene_sha==(out/'idempotence-before.sha256').read_text().strip(),
 'texture_sha256':texture_sha,
 'frozen_texture_unchanged':texture_sha=='cb7f29e4fdf6ca93a8fe024a2af867c510a8e2821cf67f0f0f11b51276e21ed9',
 'old_serialized_records':len(a),'new_serialized_records':len(b),
 'removed_records':[k for k in a if k not in b], 'changed_existing_records':changed,
 'only_tilemap_and_scene_roots_changed':all(x['classID'] in ('1839735485','1660057539') for x in changed),
 'old_cells':len(old['cells']),'new_cells':len(new['cells']),
 'identical_occupied_positions':positions(old)==positions(new),
 'identical_origin_size':old['origin']==new['origin'] and old['size']==new['size'],
 'repaint_result':(out/'repaint-result.txt').read_text(),
 'build_result':json.loads((out/'build-supervisor-result.json').read_text()),
 'player_result':json.loads((out/'selftest/supervisor-result.json').read_text()),
 'selftest_summary':(out/'selftest/selftest.txt').read_text().splitlines()[-1],
 'player_error_lines':[l for l in (out/'selftest/player.log').read_text(encoding='utf-8',errors='replace').splitlines() if re.search(r'Exception|error CS|\[TapSelfTest\] FAIL',l)],
 'player_exe_sha256':sha(root/'Builds/Windows/TapTap26.exe'),
 'unity_player_crashhandler_processes':procs,
 'images':images,
}
assert result['idempotent_scene_bytes'] and result['frozen_texture_unchanged']
assert result['identical_occupied_positions'] and result['only_tilemap_and_scene_roots_changed']
assert not result['removed_records'] and not procs
assert result['selftest_summary']=='RESULT passed=534 failed=0'
assert result['identical_origin_size']
assert not result['player_error_lines']
for name, restore_keys in (
    ('build_result', ('editor_restored_before_supervisor', 'raw_restored_after_exit')),
    ('player_result', ('player_restored_before_supervisor', 'post_exit_raw_restore')),
):
    job=result[name]
    assert type(job.get('exit_code')) is int and job['exit_code']==0, name
    assert 'error' not in job, name
    assert all(job.get(key) is True for key in restore_keys), name
result['passed']=True
(out/'final-verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='images'},ensure_ascii=True,indent=2))
print('IMAGE_COUNT',len(images))
