from pathlib import Path
import argparse, hashlib, json, shutil

ROOT=Path(__file__).resolve().parent
def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest().upper()
parser=argparse.ArgumentParser()
parser.add_argument('--build',required=True,type=int)
parser.add_argument('--route-run',required=True,type=int)
args=parser.parse_args()
paths={'route':ROOT/f'qa-v4-route{args.route_run}/qa-report.json','combat':ROOT/f'qa-v4-combat{args.build}/combat-report.json','impact':ROOT/f'qa-v4-impact{args.build}/impact-report.json'}
reports={k:read(p) for k,p in paths.items()}
frozen=ROOT/f'artifacts/v4-build{args.build}-tested.json'
for key,report in reports.items():
    assert report['passed'] and report['failureCount']==0
    assert sha(paths[key].parent/'tested-build.json')==sha(frozen)
    shutil.copyfile(paths[key],ROOT/('docs/v4-'+key+'-qa-final.json'))
route,combat,impact=reports['route'],reports['combat'],reports['impact']
assert len(route['runs'])==4 and all(r['reachedResults'] and r['transitions']==3 for r in route['runs'])
rows=[]
for r in route['runs']:
    rows.append('| '+r['name']+' | '+f"{r['totalSeconds']:.3f}"+' | '+str(r['collisions'])+' | '+f"{sum(s['actualDistance'] for s in r['stages']):.2f}"+' | '+str(r['npcCombatLaunches'])+' | '+str(r['playerCombatLaunches'])+' | '+str(r['braceBlocks'])+' |')
stages=[s for r in route['runs'] for s in r['stages']]
cases={c['name']:c for c in combat['cases']}
front,brace=cases['front'],cases['brace']
outside,inside=cases['near-outside'],cases['near-inside']
lead=[a['observedLead'] for c in combat['cases'] for a in c['attacks'] if a['rushAt']>=0 and a['observedLead']>=0]
rawrows='\n'.join('| '+k+' | '+str(len(reports[k]['checks']))+' / 0 | `'+str(paths[k].relative_to(ROOT)).replace('\\','/')+'` | `'+sha(paths[k])+'` |' for k in ('combat','impact','route'))
text=f'''# v4 最终 Windows Player 验收

最终验收使用同一 build{args.build} Windows x64/D3D11 二进制。设备：{route['device']}，Unity {route['engine']}。三套 Player 均由程序向普通移动/冲刺/稳住输入通道送入操作；菜单、暂停与重开调用现有 UI 方法。不是系统键盘自动化，也不是真人试玩。

| 测试集合 | 通过 / 失败 | 本地原报告 | SHA-256 |
|---|---:|---|---|
{rawrows}

三个原报告旁的 `tested-build.json` 与最终冻结清单 SHA 相同：`{sha(frozen)}`。工程含59份文件，运行时133份文件；封包新增说明、报告与截图另作交付清单校验。

## 双向强撞与反制

主角正面未稳住：实际击飞{front['playerLaunches']}次、落地{front['lands']}次，峰高{front['peakHeight']:.3f}米、旋转{front['peakSpin']:.1f}度，落地后真实输入移动{front['movedAfterRecovery']:.3f}米。同一布置按Shift：真实防御{brace['braceBlocks']}次，击飞{brace['playerLaunches']}次，峰高{brace['peakHeight']:.3f}米。

已进入冲撞的预警采样提前量为{min(lead):.3f}–{max(lead):.3f}秒，锁定起点、方向及路径偏差由原报告记录。危险区外{outside['minimumOutsideMargin']:.3f}米有{outside['outsideRushSamples']}个真实冲撞采样，击飞{outside['playerLaunches']}次；内侧{abs(inside['minimumOutsideMargin']):.3f}米实际击飞{inside['playerLaunches']}次并落地。边界是玩家中心的胶囊范围，不能把角色模型某个像素是否压线当作同一几何定义。

NPC互撞实际连锁击飞{cases['npc-chain']['npcLaunches']}人；Space可以在蓄力时打断。落地保护案例第二次真实接触未重复击飞。冲撞已移动{cases['pause-rush']['pauseRushDistance']:.3f}米后暂停，攻击被取消；预警、空中暂停和重开也已验证。墙边案例实际击飞、落地后可操作离墙，不以静止无攻击冒充脱困通过。

## 自然人群与完整三关

| 路线 | 总秒数 | 碰撞计数 | 移动米数 | NPC击飞NPC | 玩家被NPC击飞 | 稳住成功 |
|---|---:|---:|---:|---:|---:|---:|
{chr(10).join(rows)}

四条路线都通过普通输入连续走完三个大地图，没有传送、改计时或直接跳关。risk/safe比较路线选择；charge保留冲刺穿人群且不持续按Shift；late包含开局故意等待直至超时，此等待计入总秒数，但不包含在各段 commandedWaitSeconds 中。完整路线数据与专项初始布置分开，不把专项排程称作自然AI选择证据。

三关配置人数120/128/124。全路线观测到的可见预警/冲撞并发峰值{max(s['visibleThreatPeak'] for s in stages)}，攻击历史容量峰值{max(s['maxNpcBurstHistory'] for s in stages)}。各段预警、冲撞、玩家腾空、到站、回收、离屏行为、门口与镜头数据保存在原报告中。

## 恢复与性能的边界

阻挡玩家的地面角色邻域峰值{max(s['maxGroundNearCoincidentCount'] for s in stages)}，全部活跃状态邻域峰值{max(s['maxNearCoincidentCount'] for s in stages)}；全状态超限采样累计{sum(s['allActivePileSamples'] for s in stages)}。恢复中的NPC参与彼此分离，暂时不阻挡玩家。全部状态/地面/腾空/恢复计数继续保留；约0.5秒离散采样不能证明每帧没有短暂视觉重叠，连续正采样跨度也不是完整的连续重叠时间。

Playing状态采样平均{route['meanFrameMilliseconds']:.3f}ms，约{1000/route['meanFrameMilliseconds']:.2f}FPS；最大单帧{route['worstFrameMilliseconds']:.3f}ms。各段路线执行平均帧时范围{min(s['meanTraversalFrameMilliseconds'] for s in stages):.3f}–{max(s['meanTraversalFrameMilliseconds'] for s in stages):.3f}ms，路线执行最大帧{max(s['worstTraversalFrameMilliseconds'] for s in stages):.3f}ms。此范围包含测试采样，整体Playing统计包含迟到等待，不包含完整启动/菜单/暂停/Transition时段。截图和建场额外耗时见日志；缓存两份材质与增加计时标记不等于证明旧550ms长帧已彻底消除。

实际截图复核见 `v4-visual-review.md`；静态图不能代替动画连续性、主观音效或真人手感评价，也不是跨硬件或长时间压力测试。

## 历史与源码发布处理

build1专项104通过/2失败：墙边夹具起点在花坛里，完全未发起冲撞。调整至同一路缘可通区域，未弱化阈值；build2为109/0。build3专项128/0、原撞飞189/0，但其完整route1因配置发布处理被正常中止，不计为完整路线通过。build4完整route2为283通过/5失败，其中三项真实地面聚团，另两项为错误要求charge必须受击、迟到等待后误作刚重置。修复固定预警体周边分离与恢复体提前避让；纠正两个验证时点/语义，保留ground<=3分类阈值、频率和全部路线输入。失败原报告与过程日志均保留，详见原型记录。

仓库自动审核发现未使用平台字段 `ps4Passcode` 非空。清空后Unity构建会自动回填，因此在最终构建编辑器退出后仅对发布源码清空该字段，记录于冻结清单的 sourceSanitization。处理前后133个Windows运行文件逐项SHA相同，C#源码未改；59文件是脱敏后的发布源码快照，不声称这一配置字段与构建前完全相同。字段值没有写入新报告，v3基线没有修改。源码打包与仓库同步均使用空字段版本。
'''
text+='\n封包前build6完整路线曾有一次真实四体地面聚团，287通过/1失败，未交付为通过版本。失败原报告与逐成员核对见 `v4-route-build6-failure.json` 和 `v4-route-build6-failure.md`，UI修复及普通步行后续处理见 `v4-build6-followup.md`。保留原阈值与失败历史；本页顶部的数据来自最后实际验收构建。\n'
text+='\nbuild7加入普通步行防深入后仍在公司终点附近出现两项聚团失败，286通过/2失败，原报告保留为 `v4-route-build7-failure.json`。随后修正已越过中间路点却回追的可发生死点，须下一段完整NPC胶囊静态可通才推进路点，不传送角色；并记录成员实际路点、目标、朝向和堵塞时间。旧快照缺少这些字段，不能仅凭位置把内部路点状态写成已直接观测。\n'
text+='\n验收结论边界：最终四条自然路线中，玩家被NPC击飞均为0次；未稳住腾空与Shift防击飞对照来自受控专项。自然流程中威胁频率是否合适尚未验证，不能用这些自动输入结果代替真人难度或手感评价。约402ms偶发长帧限制继续保留。此次说明补充不修改玩法、二进制或已交付ZIP。\n'
(ROOT/'docs/v4-validation.md').write_text(text,encoding='utf-8')
(ROOT/'README.md').write_text('# Early Rush / 早八冲刺\n\n当前版本：[v4 双向冲撞说明](README-v4.md)。Windows 操作见 [操作说明](docs/操作说明-v4.md)，真实 Player 结果见 [最终验收](docs/v4-validation.md)。\n\n保留 [v3 说明](README-v3.md)、`EarlyRushUnityV3` 与 `EarlyRushUnityV2` 基线。各版本 Windows 成品位于本地 artifacts，仓库仅同步工程源码、文档和工具。\n',encoding='utf-8')
print(json.dumps({'checks':{k:len(v['checks']) for k,v in reports.items()},'routeSeconds':{r['name']:r['totalSeconds'] for r in route['runs']}},ensure_ascii=True))
