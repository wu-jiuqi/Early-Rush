"""Package only a frozen, tested v4 Windows build; verify ordinary two-ZIP delivery."""
from pathlib import Path
import argparse, hashlib, json, shutil, zipfile, re

ROOT = Path(__file__).resolve().parent
ART = ROOT / 'artifacts'
PLAYER = ART / 'EarlyRush-V4-Windows'
PROJECT = ROOT / 'EarlyRushUnityV4'

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()

def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))

def record(path):
    p = Path(path)
    return {'path': str(p), 'bytes': p.stat().st_size, 'sha256': sha(p)}

def main():
    p = argparse.ArgumentParser()
    for flag in ('route-report', 'combat-report', 'impact-report', 'build-log', 'tested-build'):
        p.add_argument('--' + flag, required=True, type=Path)
    p.add_argument('--screenshot', action='append', default=[], type=Path)
    args = p.parse_args()
    route, combat, tested = read(args.route_report), read(args.combat_report), read(args.tested_build)
    impact = read(args.impact_report)
    settings=(PROJECT/'ProjectSettings/ProjectSettings.asset').read_text(encoding='utf-8')
    match=re.search(r'(?m)^  ps4Passcode:([^\r\n]*)',settings)
    assert match and not match.group(1).strip(), 'Release source must clear unused platform passcode'
    assert tested['sourceSanitization']['runtimeFilesUnchanged']
    assert route['passed'] and route['failureCount'] == 0
    assert combat['passed'] and combat['failureCount'] == 0
    assert impact['passed'] and impact['failureCount'] == 0
    assert len(route['runs']) >= 4 and all(x['reachedResults'] and x['transitions'] == 3 for x in route['runs'])
    assert 'EARLY_RUSH_BUILD: Succeeded' in args.build_log.read_text(encoding='utf-8-sig')
    assert sha(args.build_log) == tested['buildLog']['sha256'].upper()
    assert args.build_log.stat().st_size == tested['buildLog']['bytes']
    for report in (args.route_report, args.combat_report, args.impact_report):
        assert sha(report.parent / 'tested-build.json') == sha(args.tested_build)
    for field, base in (('binaries', PLAYER), ('sourceFiles', PROJECT)):
        assert tested[field]
        for item in tested[field]:
            q = Path(item['path']).resolve()
            assert q.is_relative_to(base.resolve()) and sha(q) == item['sha256'].upper()
    frozen_source = {Path(x['path']).resolve() for x in tested['sourceFiles']}
    frozen_runtime = {Path(x['path']).resolve() for x in tested['binaries']}
    actual_source = {q.resolve() for d in ('Assets','Packages','ProjectSettings') for q in (PROJECT/d).rglob('*') if q.is_file()}
    actual_runtime = {q.resolve() for q in PLAYER.rglob('*') if q.is_file()}
    assert actual_source == frozen_source, 'Project files differ from tested snapshot'
    assert actual_runtime == frozen_runtime, 'Runtime files differ from tested snapshot'
    for rel in ('EarlyRush.exe', 'UnityPlayer.dll', 'EarlyRush_Data/Managed/Assembly-CSharp.dll'):
        assert (PLAYER / rel).resolve() in frozen_runtime
    assert (PLAYER / 'MonoBleedingEdge').is_dir()
    preserved = {}
    for name in ('repo-setup-v1-baseline.json', 'v3-v2-preservation-baseline.json', 'v4-v3-preservation-baseline.json'):
        baseline = read(ROOT / name)
        assert all(sha(x['path']) == x['sha256'].upper() for x in baseline), name
        preserved[name] = len(baseline)
    outputs = [ART / n for n in ('EarlyRush-V4-Windows-x64.zip', 'EarlyRush-V4-Unity-Source.zip',
               'EarlyRush-V4-1-of-2-Engine.zip', 'EarlyRush-V4-2-of-2-Game.zip')]
    merged = ART / 'v4-two-part-verified'
    assert not any(q.exists() for q in outputs) and not merged.exists(), 'Inspect existing outputs before repackaging'
    for source, destination in ((ROOT/'docs/操作说明-v4.md', PLAYER/'操作说明.md'),
                                (ROOT/'docs/v4-validation.md', PLAYER/'验证说明.md'),
                                (args.route_report, PLAYER/'QA-route-report.json'),
                                (args.combat_report, PLAYER/'QA-combat-report.json'),
                                (args.impact_report, PLAYER/'QA-impact-report.json')):
        shutil.copyfile(source, destination)
    assert len(args.screenshot) >= 3, 'Include visually reviewed warning, airborne and brace screenshots'
    assert len({q.name for q in args.screenshot}) == len(args.screenshot), 'Duplicate screenshot names'
    reported_shots = {Path(x['path']).resolve() for x in combat['screenshots']}
    assert all(q.resolve().is_relative_to(args.combat_report.parent.resolve()) and q.resolve() in reported_shots for q in args.screenshot), 'Screenshot must belong to this tested combat report'
    evidence = PLAYER/'验证截图'
    evidence.mkdir(exist_ok=True)
    for shot in args.screenshot:
        assert shot.is_file()
        shutil.copyfile(shot, evidence/shot.name)
    manifest_files = sorted(q for q in PLAYER.rglob('*') if q.is_file() and q.name != 'FILES.sha256')
    (PLAYER/'FILES.sha256').write_text(''.join(sha(q)+'  '+q.relative_to(PLAYER).as_posix()+'\n' for q in manifest_files), encoding='utf-8')
    with zipfile.ZipFile(outputs[0], 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for q in sorted(PLAYER.rglob('*')):
            if q.is_file(): z.write(q, PLAYER.name+'/'+q.relative_to(PLAYER).as_posix())
    with zipfile.ZipFile(outputs[1], 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for folder in ('Assets', 'Packages', 'ProjectSettings'):
            for q in sorted((PROJECT/folder).rglob('*')):
                if q.is_file(): z.write(q, PROJECT.name+'/'+q.relative_to(PROJECT).as_posix())
        z.write(ROOT/'README-v4.md', 'README-v4.md')
        for q in sorted((ROOT/'docs').iterdir()):
            if q.is_file() and (q.name.startswith('v4-') or q.name == '操作说明-v4.md') and q.name not in ('v4-release-manifest.md','v4-delivery-manifest.json'):
                z.write(q, 'docs/'+q.name)
    note = '先读我-两包解压说明.txt'
    instructions = ('早八冲刺 v4：双向冲撞\n\n下载两份 ZIP，用 Windows 自带“全部提取”。\n'
        '1. 新建一个空文件夹。\n2. 提取 Engine.zip，目标选择这个新建文件夹。\n'
        '3. 提取 Game.zip，目标仍选择完全相同的文件夹。不要选择不同默认目录，也不要再选里面的 EarlyRush-V4-Windows 子目录。\n'
        '4. 打开 EarlyRush-V4-Windows\\EarlyRush.exe。\n\n'
        '两包为普通 ZIP，文件不重叠，不需要拼接命令或第三方解压器。\n'
        'WASD/方向键移动；Space 冲刺撞飞；Shift 稳住重心，减速并抵御预警冲撞的击飞。Esc 暂停，暂停/结果时 R 重开，M 静音。\n'
        '详细操作、测试范围及已知限制见游戏目录中的说明。\n')
    dll = PLAYER.name+'/UnityPlayer.dll'
    with zipfile.ZipFile(outputs[0]) as full:
        assert full.testzip() is None
        original = {i.filename: hashlib.sha256(full.read(i)).hexdigest().upper() for i in full.infolist()}
        for index, target in enumerate(outputs[2:]):
            with zipfile.ZipFile(target, 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as out:
                for info in full.infolist():
                    if (info.filename == dll) == (index == 0):
                        out.writestr(info, full.read(info), compress_type=zipfile.ZIP_DEFLATED, compresslevel=6)
                if index == 0: out.writestr(note, instructions.encode('utf-8-sig'))
            assert target.stat().st_size < 19_000_000, 'Ordinary attachment exceeds safety margin'
    merged.mkdir()
    entries = set()
    for q in outputs[2:]:
        with zipfile.ZipFile(q) as z:
            names = z.namelist()
            assert z.testzip() is None and len(names) == len(set(names)) and not entries.intersection(names)
            assert all((merged/n).resolve().is_relative_to(merged.resolve()) for n in names)
            entries.update(names)
            z.extractall(merged)
    assert entries-{note} == set(original)
    assert all(sha(merged/n) == digest for n,digest in original.items())
    lines = (merged/PLAYER.name/'FILES.sha256').read_text(encoding='utf-8').splitlines()
    for line in lines:
        digest, name = line.split('  ',1)
        assert sha(merged/PLAYER.name/name) == digest
    with zipfile.ZipFile(outputs[1]) as z:
        assert z.testzip() is None
        assert not any('/Library/' in n or '/Temp/' in n or n.lower().endswith(('.exe','.dll','.zip')) for n in z.namelist())
    result = {'routeReport':record(args.route_report),'combatReport':record(args.combat_report),'impactReport':record(args.impact_report),'buildLog':record(args.build_log),
              'testedBuild':tested,'preservedBaselines':preserved,'zipCrcPassed':True,'nonOverlappingEntries':True,
              'mergedOriginalFilesVerified':len(original),'manifestFilesVerified':len(lines),
              'extractedPlayer':str(merged/PLAYER.name/'EarlyRush.exe'),'outputs':[record(q) for q in outputs]}
    (ART/'EarlyRush-V4-build-manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    (ART/'EarlyRush-V4-SHA256.txt').write_text(''.join(x['sha256']+'  '+Path(x['path']).name+'\n' for x in result['outputs']),encoding='utf-8')
    print(json.dumps({'outputs':result['outputs'],'verifiedFiles':len(original)},ensure_ascii=True))

if __name__ == '__main__': main()
