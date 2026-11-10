"""Source/resource validation only. This is not a Unity compile or player test."""
from pathlib import Path
import json, hashlib
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
checks=[]
def check(name,ok):
 checks.append({'name':name,'passed':bool(ok)})
 if not ok:raise AssertionError(name)
for filename in ['props','people','icons','extras','floors']:
 p=ROOT/'Assets/Resources/RushArt'/f'{filename}.png'
 check(filename+' exists',p.is_file())
 im=Image.open(p)
 check(filename+' minimum size',min(im.size)>=700)
 if filename!='floors':
  check(filename+' alpha',im.mode=='RGBA' and im.getextrema()[3][0]==0)
for p in (ROOT/'Assets').rglob('*'):
 if p.suffix=='.svg':raise AssertionError('SVG placeholder: '+str(p))
for p in (ROOT/'Assets').rglob('*.cs'):
 check(p.name+' metadata',p.with_suffix(p.suffix+'.meta').is_file())
check('Independent v7 output','EarlyRush-V7-Windows' in (ROOT/'Assets/Editor/BuildEarlyRush.cs').read_text())
check('Raster character creation','RushArt.Sprite(rig' in (ROOT/'Assets/Scripts/RushVisuals.cs').read_text())
check('Generated mesh cleanup','RushArtMeshCleanup' in (ROOT/'Assets/Scripts/RushArt.cs').read_text())
report={'scope':'Source/resource checks only; Unity build, Player, visuals and performance NOT verified','checks':checks,'passed':len(checks)}
(ROOT/'Docs/v7-source-resource-checks.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
print(f'{len(checks)} source/resource checks passed; NOT a Unity test')
