"""Owned Editor process supervisor; uses existing raw-pref journal helpers. No concurrent Editor jobs."""
import sys, json, subprocess, importlib.util
from pathlib import Path
root=Path(__file__).resolve().parents[5]
spec=importlib.util.spec_from_file_location('prefs',root/'Tools/run_tap_selftest.py')
prefs=importlib.util.module_from_spec(spec);spec.loader.exec_module(prefs)
prefs.REG_PATH='Software\\Unity\\UnityEditor\\瓦卡棱镜\\螺壳海底游记'
method=sys.argv[1]; label=sys.argv[2]
out=Path(__file__).parent
before=prefs.capture()
(out/(label+'-supervisor-before.json')).write_text(json.dumps(before,ensure_ascii=False,indent=2),encoding='utf-8')
cmd=['E:/unity/Downloads/Editor/Unity.exe','-batchmode','-projectPath',str(root),'-executeMethod',method,'-logFile',str(out/(label+'.log'))]
if method not in ('TilesHD.RecordMap.Before','TilesHD.RecordMap.After'):cmd+=['-quit']
cmd+=sys.argv[3:]
p=None;code=2;observed=False
try:
 p=subprocess.Popen(cmd,cwd=root)
 try:code=p.wait(timeout=1200)
 except subprocess.TimeoutExpired:p.terminate();p.wait(timeout=30);code=124
 observed=prefs.capture()==before
finally:
 if p is not None and p.poll() is None:p.terminate();p.wait(timeout=30)
 prefs.restore(before)
 result=dict(command=cmd,exit_code=code,editor_restored_before_supervisor=observed,raw_restored_after_exit=prefs.capture()==before)
 (out/(label+'-supervisor-result.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps(result,ensure_ascii=True))
sys.exit(code)
