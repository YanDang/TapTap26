"""Windows selftest supervisor. Restores raw TAP_* registry values AFTER player exit,
including crash/timeout; other prefs untouched. Python 3 standard library only.
Usage: python Tools/run_tap_selftest.py [--baseline] [--output PATH] [--exe PATH]
Do not run concurrently with another instance using the same PlayerPrefs namespace.
"""
import argparse
import base64
import ctypes as c
from ctypes import wintypes as w
import json
from pathlib import Path
import subprocess
import winreg

REG_PATH = r'Software\瓦卡棱镜\螺壳海底游记'
api = c.WinDLL('advapi32', use_last_error=True)
api.RegEnumValueW.argtypes = [w.HKEY,w.DWORD,w.LPWSTR,c.POINTER(w.DWORD),c.c_void_p,c.POINTER(w.DWORD),c.c_void_p,c.POINTER(w.DWORD)]
api.RegSetValueExW.argtypes = [w.HKEY,w.LPCWSTR,w.DWORD,w.DWORD,c.c_void_p,w.DWORD]

def capture():
    result = {}
    try:
        key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, REG_PATH, 0, winreg.KEY_READ)
    except FileNotFoundError:
        return result
    with key:
        index=0
        while True:
            name=c.create_unicode_buffer(16384); length=w.DWORD(16384); typ=w.DWORD(); size=w.DWORD()
            code=api.RegEnumValueW(int(key),index,name,c.byref(length),None,c.byref(typ),None,c.byref(size))
            if code==259: break
            if code not in (0,234): raise OSError(code)
            data=c.create_string_buffer(size.value); length=w.DWORD(16384)
            code=api.RegEnumValueW(int(key),index,name,c.byref(length),None,c.byref(typ),data,c.byref(size))
            if code: raise OSError(code)
            if name.value.startswith(('TAP_', 'GJ_UltChoice')):
                result[name.value]={'type':typ.value,'base64':base64.b64encode(data.raw[:size.value]).decode('ascii')}
            index+=1
    return result

def restore(snapshot):
    with winreg.CreateKeyEx(winreg.HKEY_CURRENT_USER,REG_PATH,0,winreg.KEY_ALL_ACCESS) as key:
        for name in capture(): winreg.DeleteValue(key,name)
        for name,value in snapshot.items():
            raw=base64.b64decode(value['base64']); buf=c.create_string_buffer(raw)
            code=api.RegSetValueExW(int(key),name,0,value['type'],buf,len(raw))
            if code: raise OSError(code)
    assert capture()==snapshot, 'Raw TAP_* restoration mismatch'

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--exe',default='Builds/Windows/TapTap26.exe')
    parser.add_argument('--output',default='Builds/Windows')
    parser.add_argument('--timeout',type=float,default=180)
    parser.add_argument('--baseline',action='store_true')
    parser.add_argument('--boss',action='store_true',help='Also validate the real -tapboss startup entry')
    parser.add_argument('--recover',help='Restore a supervisor-prefs-before.json after a supervisor/OS crash; close game first')
    args=parser.parse_args()
    if args.recover:
        restore(json.loads(Path(args.recover).read_text(encoding='utf-8'))); print('RECOVERED'); return 0
    output=Path(args.output).resolve(); output.mkdir(parents=True,exist_ok=True)
    original=capture()
    (output/'supervisor-prefs-before.json').write_text(json.dumps(original,ensure_ascii=False,indent=2),encoding='utf-8')
    command=[str(Path(args.exe).resolve()),'-tapselftest','-tapoutput',str(output),'-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','-logFile',str(output/'player.log')]
    if args.baseline: command.append('-tapbaseline')
    if args.boss: command.append('-tapboss')
    code=2; player=None; observed=None
    try:
        player=subprocess.Popen(command)
        try: code=player.wait(timeout=args.timeout)
        except subprocess.TimeoutExpired:
            player.terminate(); player.wait(timeout=20); code=124
        observed=capture()==original
    finally:
        # Never write the registry while the child can still save over it.
        if player is not None and player.poll() is None:
            player.terminate(); player.wait(timeout=20)
        restore(original)
        result={'command':command,'exit_code':code,'player_restored_before_supervisor':observed,'post_exit_raw_restore':capture()==original,'original_TAP_count':len(original)}
        (output/'supervisor-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps(result,ensure_ascii=True))
    return code if observed else 3

if __name__=='__main__': raise SystemExit(main())
