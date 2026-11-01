from pathlib import Path
import argparse, hashlib, json, re

ROOT=Path(__file__).resolve().parent
ART=ROOT/'artifacts'
parser=argparse.ArgumentParser()
parser.add_argument('--source-commit',required=True)
parser.add_argument('--build',required=True,type=int)
args=parser.parse_args()
assert re.fullmatch('[0-9a-f]{40}',args.source_commit)
def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest().upper()
manifest=read(ART/'EarlyRush-V4-build-manifest.json')
assert Path(manifest['buildLog']['path']).name==f'unity-v4-build-{args.build}.log'
smoke=read(ART/'EarlyRush-V4-merged-smoke.json')
assert manifest['zipCrcPassed'] and manifest['nonOverlappingEntries']
assert smoke['normalCloseExitCode']==0 and smoke['basicInputChecksFailed']==0
assert Path(smoke['player']).resolve()==Path(manifest['extractedPlayer']).resolve()
assert smoke['basicInputChecksPassed']>=24 and smoke['menuScreenshotVisuallyReviewed']
assert smoke['filesRecheckedAfterStartup']==manifest['manifestFilesVerified']
assert smoke['originalFilesRecheckedAfterStartup']==manifest['mergedOriginalFilesVerified']
assert smoke['allFourZipHashesUnchanged'] and not smoke['isFullSuite']
for item in manifest['outputs']:
    p=Path(item['path']); assert p.stat().st_size==item['bytes'] and sha(p)==item['sha256']
keys=('routeReport','combatReport','impactReport','buildLog','preservedBaselines','zipCrcPassed','nonOverlappingEntries','mergedOriginalFilesVerified','manifestFilesVerified','outputs')
delivery={k:manifest[k] for k in keys}
delivery['sourceCommit']=args.source_commit
delivery['sourceSanitization']=manifest['testedBuild']['sourceSanitization']
delivery['mergedStartupSmoke']=smoke
(ROOT/'docs/v4-delivery-manifest.json').write_text(json.dumps(delivery,ensure_ascii=False,indent=2),encoding='utf-8')
rows='\n'.join('| '+Path(x['path']).name+' | '+str(x['bytes'])+' | `'+x['sha256']+'` |' for x in manifest['outputs'])
text=f'''# v4 Windows 交付清单

游戏源码功能提交：`{delivery['sourceCommit']}`。最终验收与交付使用build{args.build}；全部原报告与统计范围见 `v4-validation.md`。本清单包含本地成品校验事实，附件保存及聊天发送状态以实际回执为准。

| 文件 | 字节 | SHA-256 |
|---|---:|---|
{rows}

目录：`D:/CodexWorkspace/2026-10-31/task-4/artifacts/`。完整 Windows ZIP 和源码 ZIP 均通过CRC；聊天附件为两个各小于19,000,000字节的普通ZIP，文件内容不重叠，不是需要拼接命令的分卷。

下载 Engine 和 Game 两包后，新建一个空目录，分别使用 Windows“全部提取”，两次明确选择同一个目标目录；不要使用不同默认目录，也不要第二次选进内部 `EarlyRush-V4-Windows` 子目录。完成后运行 `EarlyRush-V4-Windows/EarlyRush.exe`。

两包按此规则实际合并解压，{manifest['mergedOriginalFilesVerified']}个原始文件逐项SHA匹配，`FILES.sha256`中{manifest['manifestFilesVerified']}条全部通过。包内保留EXE、Data、UnityPlayer.dll、MonoBleedingEdge等完整依赖，附中文操作说明、验证说明、三个原始QA报告及实际Player截图。

合并解压目录的真实EXE另做基础启动检查：{smoke['basicInputChecksPassed']}项通过、0失败，正常关闭退出码0，并实际查看菜单截图。它是解压启动与基础输入smoke，不重复充当完整路线验收。启动后再次核对{smoke['filesRecheckedAfterStartup']}条文件清单与全部ZIP SHA，均未变化。

v1/v2/v3记录的基线分别51/54/59项，打包时全部SHA相同。仓库保留旧版本分支，不提交EXE/DLL/ZIP、缓存、真实凭据或Library身份回执。

发布源码在Editor退出后仅清空自动回填、未使用的非Windows平台字段；该过程及前后配置SHA写入 sourceSanitization，133个Windows运行文件前后SHA完全相同，C#源码未改。源码ZIP与远端工程采用脱敏后的59文件快照；不声称那个配置字段与构建时逐字节一致。
'''
(ROOT/'docs/v4-release-manifest.md').write_text(text,encoding='utf-8')
print(json.dumps({'packages':len(manifest['outputs']),'mergedFiles':manifest['mergedOriginalFilesVerified'],'startupChecks':smoke['basicInputChecksPassed']}))
