"""Record exact post-build runtime and project bytes before Player QA."""
from pathlib import Path
import argparse, hashlib, json, re

root=Path(__file__).resolve().parent
p=argparse.ArgumentParser()
p.add_argument('--log',required=True,type=Path)
p.add_argument('--output',required=True,type=Path)
a=p.parse_args()
assert not a.output.exists(), 'Keep prior build evidence; choose a fresh manifest name'
assert 'EARLY_RUSH_BUILD: Succeeded' in a.log.read_text(encoding='utf-8-sig')
def record(path):
    return {'path':str(path.resolve()),'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest().upper()}
project=root/'EarlyRushUnityV4'; player=root/'artifacts/EarlyRush-V4-Windows'
for name in ('EarlyRush.exe','UnityPlayer.dll','EarlyRush_Data/Managed/Assembly-CSharp.dll'):
    assert (player/name).is_file()
binaries=sorted(f for f in player.rglob('*') if f.is_file())
runtime_before=[record(f) for f in binaries]
# Unity repopulates this unused platform field during Editor serialization.
# Redact ONLY the release source configuration after Editor exits; Windows
# runtime and C# compilation inputs remain unchanged. Record this distinction.
settings=project/'ProjectSettings/ProjectSettings.asset'
before=record(settings)
text=settings.read_bytes().decode('utf-8')
text,count=re.subn(r'(?m)^(  ps4Passcode:)[^\r\n]*',r'\1',text)
assert count==1
settings.write_bytes(text.encode('utf-8'))
after=record(settings)
runtime_after=[record(f) for f in binaries]
assert runtime_before==runtime_after
source=sorted(f for d in ('Assets','Packages','ProjectSettings') for f in (project/d).rglob('*') if f.is_file())
sanitization={'field':'ps4Passcode','path':str(settings.resolve()),'beforeSha256':before['sha256'],'afterSha256':after['sha256'],
    'changed':before['sha256']!=after['sha256'],'runtimeFilesUnchanged':True,
    'scope':'Unused non-Windows platform field cleared after Unity Editor exits. Release source snapshot is post-sanitization; C# sources and tested Windows runtime bytes are unchanged. No field value is retained.'}
result={'buildLog':record(a.log),'binaries':runtime_after,'sourceFiles':[record(f) for f in source],'sourceSanitization':sanitization}
a.output.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'sourceFiles':len(source),'runtimeFiles':len(binaries),'manifest':record(a.output)}))
