from pathlib import Path
import argparse,hashlib,json,re,zipfile
ROOT=Path(__file__).resolve().parent
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
p=argparse.ArgumentParser()
p.add_argument('--visually-reviewed-menu',required=True,type=Path)
a=p.parse_args()
m=read(ROOT/'artifacts/EarlyRush-V4-build-manifest.json')
e=read(ROOT/'artifacts/v4-merged-smoke-exit.json')
assert e['normalCloseAccepted'] and e['normalCloseExitCode']==0
player=Path(e['player']).resolve()
assert player==Path(m['extractedPlayer']).resolve()
log=Path(e['playerLog'])
text=log.read_text(encoding='utf-8-sig')
passed=re.findall(r'^RUSH_QA_CHECK PASS (.+)$',text,re.M)
failed=re.findall(r'^RUSH_QA_CHECK FAIL (.+)$',text,re.M)
assert len(passed)>=24 and not failed
assert any('Dash accelerates and consumes cooldown' in c for c in passed)
shot=Path(e['output'])/'01-menu.png'
assert a.visually_reviewed_menu.resolve()==shot.resolve() and shot.is_file()
lines=(player.parent/'FILES.sha256').read_text(encoding='utf-8').splitlines()
for line in lines:
    digest,name=line.split('  ',1)
    assert sha(player.parent/name)==digest
with zipfile.ZipFile(ROOT/'artifacts/EarlyRush-V4-Windows-x64.zip') as z:
    names=z.namelist()
    for name in names:
        assert sha(player.parent.parent/name)==hashlib.sha256(z.read(name)).hexdigest().upper()
assert len(names)==m['mergedOriginalFilesVerified'] and len(lines)==m['manifestFilesVerified']
for o in m['outputs']:
    q=Path(o['path']);assert q.stat().st_size==o['bytes'] and sha(q)==o['sha256']
result={'player':str(player),'menuScreenshot':str(shot),'menuScreenshotSha256':sha(shot),
 'menuScreenshotVisuallyReviewed':True,'basicInputChecksPassed':len(passed),'basicInputChecksFailed':len(failed),
 'normalCloseExitCode':e['normalCloseExitCode'],'normalCloseAccepted':e['normalCloseAccepted'],
 'filesRecheckedAfterStartup':len(lines),'originalFilesRecheckedAfterStartup':len(names),
 'allFourZipHashesUnchanged':True,'isFullSuite':False,
 'scope':'Merged-package real Player startup and basic input smoke only. Final build8 combat, impact, and full-route suites are separate evidence.',
 'playerLog':{'path':str(log),'sha256':sha(log)},'checks':passed}
first_log=ROOT/'artifacts/player-v4-merged-smoke.log'
if first_log.exists() and first_log.resolve()!=log.resolve():
    result['diagnostics']=[{'firstAttemptLog':str(first_log),'sha256':sha(first_log),
      'status':'Not accepted: launcher log polling raised a file-sharing IOException before basic QA. No Player remained at subsequent process check and no successful exit receipt was recorded. Original log and output directory retained. The next launcher used FileShare.ReadWrite; game files were not edited.'}]
(ROOT/'artifacts/EarlyRush-V4-merged-smoke.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'checks':len(passed),'files':len(lines),'originalFiles':len(names),'exit':e['normalCloseExitCode']}))
