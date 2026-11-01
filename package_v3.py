"""Package the exact tested v3 build and verify two ordinary ZIPs merge losslessly."""
from pathlib import Path
import argparse
import hashlib
import json
import shutil
import zipfile

ROOT = Path(__file__).resolve().parent
ART = ROOT / 'artifacts'
PLAYER = ART / 'EarlyRush-V3-Windows'
PROJECT = ROOT / 'EarlyRushUnityV3'

def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()

def read_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))

def record(path):
    return {'path': str(path), 'bytes': path.stat().st_size, 'sha256': sha(path)}

def main():
    parser = argparse.ArgumentParser()
    for flag in ('route-report', 'impact-report', 'build-log', 'test-build-manifest'):
        parser.add_argument('--' + flag, required=True, type=Path)
    parser.add_argument('--reuse-engine', action='store_true')
    args = parser.parse_args()
    route, impact = read_json(args.route_report), read_json(args.impact_report)
    assert route['passed'] and route['failureCount'] == 0
    assert impact['passed'] and impact['failureCount'] == 0
    assert len(route['runs']) == 4 and all(r['reachedResults'] and r['transitions'] == 3 for r in route['runs'])
    assert 'EARLY_RUSH_BUILD: Succeeded' in args.build_log.read_text(encoding='utf-8-sig')
    tested = read_json(args.test_build_manifest)
    required = ['EarlyRush.exe', 'UnityPlayer.dll', 'EarlyRush_Data/Managed/Assembly-CSharp.dll']
    binary_paths = {Path(x['path']).resolve() for x in tested['binaries']}
    source_paths = {Path(x['path']).resolve() for x in tested['sourceFiles']}
    assert binary_paths and all(p.is_relative_to(PLAYER.resolve()) for p in binary_paths)
    assert {(PLAYER / p).resolve() for p in required} <= binary_paths
    assert source_paths and all(p.is_relative_to(PROJECT.resolve()) for p in source_paths)
    assert all(sha(Path(x['path'])) == x['sha256'].upper() for x in tested['binaries'])
    assert all(sha(Path(x['path'])) == x['sha256'].upper() for x in tested['sourceFiles'])
    for report_path in (args.route_report, args.impact_report):
        assert sha(report_path.parent / 'tested-build.json') == sha(args.test_build_manifest)
    preserved = {}
    for name in ('repo-setup-v1-baseline.json', 'v3-v2-preservation-baseline.json'):
        baseline = read_json(ROOT / name)
        assert all(sha(x['path']) == x['sha256'].upper() for x in baseline), name + ' changed'
        preserved[name] = len(baseline)
    assert all((PLAYER / p).is_file() for p in required)
    assert (PLAYER / 'MonoBleedingEdge').is_dir()
    outputs = [ART / n for n in ('EarlyRush-V3-Windows-x64.zip', 'EarlyRush-V3-Unity-Source.zip',
        'EarlyRush-V3-1-of-2-Engine.zip', 'EarlyRush-V3-2-of-2-Game.zip')]
    destination = ART / 'v3-two-part-verified'
    assert not any(p.exists() for i,p in enumerate(outputs) if not (args.reuse_engine and i==2)), 'Inspect existing outputs; do not overwrite'
    assert not destination.exists(), 'Inspect existing extraction; do not overwrite'
    shutil.copyfile(ROOT / 'docs' / '操作说明-v3.md', PLAYER / '操作说明.md')
    shutil.copyfile(ROOT / 'docs' / 'v3-validation.md', PLAYER / '验证说明.md')
    shutil.copyfile(args.route_report, PLAYER / 'QA-route-report.json')
    shutil.copyfile(args.impact_report, PLAYER / 'QA-impact-report.json')
    evidence_dir = PLAYER / '验证截图'
    evidence_dir.mkdir(exist_ok=True)
    evidence_names = ('front-visual-compression.png', 'front-visual-tumble.png', 'chain-dash-landing-rebound.png')
    shots = {Path(s['path']).name: s for s in impact['screenshots']}
    for name in evidence_names:
        source = args.impact_report.parent / name
        assert name in shots and source.is_file()
        shutil.copyfile(source, evidence_dir / name)
    (evidence_dir / '说明.txt').write_text(
        '实际 Windows Player 最终构建截图：压缩、腾空翻转和多人落地回弹。\n'
        '这是明确启用的碰撞测试布置；完整通勤地图与人群见路线验收报告。\n'
        '截图不是真人试玩，阶段数据来自同一帧探针；动作是否合个人偏好仍需实际游玩。\n', encoding='utf-8-sig')
    files = sorted(p for p in PLAYER.rglob('*') if p.is_file() and p.name != 'FILES.sha256')
    (PLAYER / 'FILES.sha256').write_text(''.join(sha(p) + '  ' + p.relative_to(PLAYER).as_posix() + '\n' for p in files), encoding='utf-8')
    with zipfile.ZipFile(outputs[0], 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for p in sorted(PLAYER.rglob('*')):
            if p.is_file(): z.write(p, PLAYER.name + '/' + p.relative_to(PLAYER).as_posix())
    source_files = []
    with zipfile.ZipFile(outputs[1], 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for directory in ('Assets', 'Packages', 'ProjectSettings'):
            for p in sorted((PROJECT / directory).rglob('*')):
                if not p.is_file(): continue
                rel = p.relative_to(PROJECT).as_posix()
                source_files.append({'path': rel, 'bytes': p.stat().st_size, 'sha256': sha(p)})
                z.write(p, PROJECT.name + '/' + rel)
        z.write(ROOT / 'README-v3.md', 'README-v3.md')
        for p in sorted((ROOT / 'docs').iterdir()):
            if p.is_file() and p.name not in ('v3-delivery-manifest.json','v3-release-manifest.md') and (p.name.startswith('v3-') or p.name == '操作说明-v3.md'):
                z.write(p, 'docs/' + p.name)
    note_name = '先读我-两包解压说明.txt'
    instructions = '''早八冲刺 v3 果冻冲撞版

下载两个 ZIP，使用 Windows 自带“全部提取”即可。
1. 新建一个空文件夹，例如 D:\\Games\\早八冲刺v3。
2. 右键 Engine.zip → 全部提取，把目标改为这个新建文件夹。
3. 右键 Game.zip → 全部提取，目标仍选完全相同的文件夹。
   不要选择默认的不同文件夹，也不要选择里面的 EarlyRush-V3-Windows 子文件夹。
4. 两包均解压完成后，打开 EarlyRush-V3-Windows\\EarlyRush.exe。

EXE、UnityPlayer.dll、EarlyRush_Data、MonoBleedingEdge 应在同一层。
两包是普通 ZIP，文件互不重叠；无需第三方解压器或拼接命令。

WASD/方向键移动；Space 冲刺撞飞；Shift 稳住抗推（取消冲刺）。
Esc 暂停；暂停或结算时 R 重开；M 声音。菜单/暂停可开关轻微震屏。
三关共享110秒，超时仍可继续完成并获得迟到结算。
详细操作和验证范围见游戏目录内说明。
'''
    top = PLAYER.name + '/'
    with zipfile.ZipFile(outputs[0]) as source:
        assert source.testzip() is None
        original = {i.filename: hashlib.sha256(source.read(i.filename)).hexdigest().upper() for i in source.infolist()}
        for index, target in enumerate(outputs[2:]):
            if index==0 and args.reuse_engine:
                with zipfile.ZipFile(target) as existing:
                    assert existing.testzip() is None
                    assert set(existing.namelist()) == {top + 'UnityPlayer.dll', note_name}
                    assert hashlib.sha256(existing.read(top + 'UnityPlayer.dll')).hexdigest().upper() == original[top + 'UnityPlayer.dll']
                    assert existing.read(note_name) == instructions.encode('utf-8-sig')
                assert target.stat().st_size < 19_000_000
                continue
            with zipfile.ZipFile(target, 'x', zipfile.ZIP_DEFLATED, compresslevel=6) as out:
                for info in source.infolist():
                    if (info.filename == top + 'UnityPlayer.dll') == (index == 0):
                        out.writestr(info, source.read(info.filename), compress_type=zipfile.ZIP_DEFLATED, compresslevel=6)
                if index == 0: out.writestr(note_name, instructions.encode('utf-8-sig'))
            assert target.stat().st_size < 19_000_000, 'Attachment safety margin exceeded'
    destination.mkdir()
    entries = set()
    for part in outputs[2:]:
        with zipfile.ZipFile(part) as z:
            assert z.testzip() is None
            names = set(z.namelist())
            assert not entries & names
            assert all((destination / n).resolve().is_relative_to(destination.resolve()) for n in names)
            entries |= names
            z.extractall(destination)
    assert entries - {note_name} == set(original)
    assert all(sha(destination / n) == digest for n, digest in original.items())
    merged = destination / PLAYER.name
    hashes = (merged / 'FILES.sha256').read_text(encoding='utf-8').splitlines()
    for line in hashes:
        digest, rel = line.split('  ', 1)
        assert sha(merged / rel) == digest
    with zipfile.ZipFile(outputs[1]) as z:
        assert z.testzip() is None
        assert not any('/Library/' in n or '/Temp/' in n or n.lower().endswith(('.exe', '.dll', '.zip')) for n in z.namelist())
    result = {'routeReport': record(args.route_report), 'impactReport': record(args.impact_report),
        'buildLog': record(args.build_log), 'testedBuild': tested, 'preservedBaselines': preserved,
        'zipCrcPassed': True, 'nonOverlappingEntries': True, 'mergedOriginalFilesVerified': len(original),
        'manifestFilesVerified': len(hashes), 'extractedPlayer': str(merged / 'EarlyRush.exe'),
        'outputs': [record(p) for p in outputs], 'sourceFiles': source_files}
    (ART / 'EarlyRush-V3-build-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    (ART / 'EarlyRush-V3-SHA256.txt').write_text(''.join(x['sha256'] + '  ' + Path(x['path']).name + '\n' for x in result['outputs']), encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k != 'sourceFiles'}))

if __name__ == '__main__':
    main()
