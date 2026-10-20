"""Fail-closed closeout of actual Rework3 candidate; no engine, image or production edits."""
from pathlib import Path
import json,re,hashlib,sys,copy
R=Path(__file__).resolve().parents[5];O=Path(__file__).parent
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def load(n):return json.loads((O/n).read_text(encoding='utf-8'))
def save(n,d):(O/n).write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8')
def supervisor_ok(d,player=False):
 fields=['player_restored_before_supervisor','post_exit_raw_restore'] if player else ['editor_restored_before_supervisor','raw_restored_after_exit']
 return type(d.get('exit_code')) is int and d['exit_code']==0 and all(d.get(k) is True for k in fields) and not any('error' in k.lower() for k in d)
def predicate_tests():
 good={'exit_code':0,'editor_restored_before_supervisor':True,'raw_restored_after_exit':True};assert supervisor_ok(good)
 tests=[]
 for key,val in [('exit_code',1),('exit_code','0'),('exit_code',False),('editor_restored_before_supervisor',False),('editor_restored_before_supervisor','true'),('raw_restored_after_exit',None),('error','failure')]:
  d=dict(good);d[key]=val;assert not supervisor_ok(d);tests.append([key,val])
 for key in good:
  d=dict(good);del d[key];assert not supervisor_ok(d);tests.append([key,'missing'])
 return {'passed':True,'negative_cases':tests,'note':'Predicate tests only; does not replace real editor/player execution.'}
save('closeout.json',{'passed':False,'status':'RUNNING_OR_INTERRUPTED'})
try:
 checks={};supervisors={}
 for name in ['repaint-v3','after-v3','repeat','readback','build']:
  d=load(name+'-supervisor-result.json');supervisors[name]=d;checks[name+'_supervisor']=supervisor_ok(d)
 d=load('selftest/supervisor-result.json');supervisors['player']=d;checks['player_supervisor']=supervisor_ok(d,True)
 test=(O/'selftest/selftest.txt').read_text();passed=sum(l.startswith('PASS ') for l in test.splitlines());failed=[l for l in test.splitlines() if l.startswith('FAIL ')]
 checks['selftest_534']=passed==534 and not failed and test.rstrip().endswith('RESULT passed=534 failed=0') and '-tapbaseline' not in d['command']
 checks['fresh_build']=any('Builds\\Rework3\\TapTap26.exe' in s or 'Builds/Rework3/TapTap26.exe' in s for s in d['command']) and '[TapWindowsBuild] Succeeded errors=0 warnings=0' in (O/'build.log').read_text(errors='replace')
 for n in ['repeat-validation','scene-protection','integration-protection','scope-validation','environment-restoration','final-prefs-audit','process-release','visual-review']:
  checks[n]=load(n+'.json').get('passed') is True
 scene=load('scene-protection.json');checks['geometry']=all(scene.get(k) is True for k in ['origin_unchanged','size_unchanged','fresh_readback_matches_saved']) and scene.get('occupied')==698 and scene.get('issues')==[]
 repeat=load('repeat-validation.json');checks['scene_bytes_match_capture']=h(R/'Assets/Scenes/BiomechanicalCombatScene.unity')==repeat['before']==repeat['after'] and 'changed=0;' in repeat['result']
 integration=load('integration-protection.json');checks['road_coverage']=all(v.get('every_three_has_transition') is True for v in integration['road_edges'].values()) and integration['changed_cells']==27 and integration['issues']==[]
 checks['capture_complete']='GPU capture completed; editor raw preferences restored=True' in (O/'record-after-result.txt').read_text()
 checks['player_frame_reviewed']=load('visual-review.json').get('player_frame_reviewed') is True
 ready=json.loads((O.parent/'art/READY.json').read_text());checks['ready_pngs']=all(h(R/p)==v['sha256'] for p,v in ready['production_pngs'].items())
 for n in ['handoff','manifest','validation','scope_validation']:
  checks['ready_'+n]=h(O.parent/'art'/(n+('.md' if n=='handoff' else '.json')))==ready[n+'_sha256']
 source=load('tested-source-sha256.json');restored={'ProjectSettings/ProjectVersion.txt','Packages/packages-lock.json'}
 source_diff=[p for p,v in source.items() if p not in restored and (not (R/p).exists() or h(R/p)!=v)]
 # Preserve original player/build manifest; bind the sole later Editor-only repair to fresh evidence.
 fix=load('recorder-fix/verification.json');editor_path='Assets/Editor/TilesHD/RecordMap.cs'
 checks['tested_player_source_unchanged']=not [p for p in source_diff if p!=editor_path]
 checks['recorder_fix_source_bound']=source_diff==[editor_path] and h(R/editor_path)==fix.get('editor_source_sha256')==h(O/'recorder-fix/RecordMap-tested.cs.txt') and h(O/'tested-source-sha256.json')==fix.get('original_tested_source_manifest_sha256')
 checks['recorder_fix_protection']=fix.get('passed') is True and fix.get('protected_asset_differences')==[] and fix.get('existing_image_differences')==[] and fix.get('environment_restored') is True and fix.get('scene_sha256')==h(R/'Assets/Scenes/BiomechanicalCombatScene.unity')
 for phase in ['before','after']:
  checks['recorder_fix_'+phase]=supervisor_ok(load('recorder-fix/'+phase+'-supervisor-result.json')) and (O/('recorder-fix/record-'+phase+'-result.txt')).read_text().strip()=='GPU capture completed; editor raw preferences restored=True'
 guard=load('recorder-fix/guard-supervisor-result.json')
 checks['recorder_rejects_final_as_before']=type(guard.get('exit_code')) is int and guard['exit_code']==1 and guard.get('editor_restored_before_supervisor') is True and guard.get('raw_restored_after_exit') is True and 'Before recording requires exact e07520f baseline scene' in (O/'recorder-fix/guard.log').read_text(errors='replace') and not (O/'recorder-fix/guard').exists()
 lognames=['repaint-v3.log','after-v3.log','repeat.log','readback.log','build.log','selftest/player.log','recorder-fix/before.log','recorder-fix/after.log']
 # Known service diagnostics are retained separately, never described as an error-free raw log.
 errors=[];service=[];broad=[]
 pattern=re.compile(r'error CS\d+|Shader error|(?:NullReference|MissingReference|InvalidOperation|Argument|IndexOutOfRange)Exception\s*:|\[TapSelfTest\] FAIL|\[TilesHD\].*FAIL|Compilation failed|BuildFailedException',re.I)
 for n in lognames:
  for line in (O/n).read_text(errors='replace').splitlines():
   if pattern.search(line):errors.append({'log':n,'line':line})
   if re.search(r'error|exception|failed',line,re.I):
    broad.append({'log':n,'line':line})
    if line.startswith('[Licensing::') or line.startswith('Curl error'):service.append({'log':n,'line':line})
 checks['no_detected_compile_render_runtime_errors']=not errors
 save('log-audit.json',dict(detected_error_lines=errors,service_diagnostics=service,broad_keyword_matches=broad,pattern=pattern.pattern,note='Licensing cache/token errors and readback curl cancellation observed despite successful exits; asset names and allocator fallback statistics are not runtime exceptions.'))
 save('closeout-predicate-tests.json',predicate_tests())
 build={p.relative_to(R).as_posix():h(p) for p in (R/'Builds/Rework3').rglob('*') if p.is_file()}
 checks['build_content_present']=bool(build) and 'Builds/Rework3/TapTap26_Data/Managed/Assembly-CSharp.dll' in build
 evidence={}
 for p in O.rglob('*'):
  if p.is_file() and p.name not in ['closeout.json','artifact-sha256.json'] and '__pycache__' not in p.parts:
   evidence[p.relative_to(R).as_posix()]=h(p)
 manifest={'build_files':build,'evidence_files':evidence,'production':{p:h(R/p) for p in source if p.startswith('Assets/')},'tested_source_manifest_sha256':h(O/'tested-source-sha256.json')}
 save('artifact-sha256.json',manifest)
 result={'passed':all(v is True for v in checks.values()),'status':'COMPLETE' if all(checks.values()) else 'FAILED','baseline':'e07520f16254b12ac7d4625c36209fc334e08650','branch':'feature/hd-ground-tiles','checks':checks,'selftest':{'passed':passed,'failed':len(failed),'exit_code':d['exit_code']},'supervisors':supervisors,'detected_error_lines':errors,'service_diagnostic_count':len(service),'tested_source_differences':source_diff,'artifact_manifest_sha256':h(O/'artifact-sha256.json'),'scene_sha256':h(R/'Assets/Scenes/BiomechanicalCombatScene.unity'),'assembly_sha256':build.get('Builds/Rework3/TapTap26_Data/Managed/Assembly-CSharp.dll'),'visual_scope':load('visual-review.json'),'restored_environment_rebuilt':False,'requirement_document':'E:/gamejam/Docs/tasks/REWORK_TILES_HD_3.md exists; lead has read it; recorder-fix delegate independently read the document. Prior not-found statement corrected. No commit under delegated scope.','no_commit':True}
 save('closeout.json',result);print(json.dumps({k:result[k] for k in ['passed','status','selftest','scene_sha256','assembly_sha256','service_diagnostic_count']},ensure_ascii=False));assert result['passed'],checks
except Exception as e:
 save('closeout.json',{'passed':False,'status':'FAILED','error':repr(e)});raise
