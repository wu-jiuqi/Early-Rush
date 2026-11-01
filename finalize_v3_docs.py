from pathlib import Path
import argparse, hashlib, json, shutil

ROOT = Path(__file__).resolve().parent
def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--route-report', type=Path, required=True)
    ap.add_argument('--impact-report', type=Path, required=True)
    ap.add_argument('--build-manifest', type=Path, required=True)
    args = ap.parse_args()
    route, impact, build = load(args.route_report), load(args.impact_report), load(args.build_manifest)
    assert route['passed'] and route['failureCount'] == 0 and len(route['runs']) == 4
    assert impact['passed'] and impact['failureCount'] == 0
    assert all(sha(Path(x['path'])) == x['sha256'] for x in build['binaries'] + build['sourceFiles'])
    cases = {x['name']: x for x in impact['cases']}
    normal, dash, chain = [cases[k] for k in ('front-normal', 'front-dash', 'chain-dash')]
    wall = cases['wall-chain-recovery']
    rows = []
    for run in route['runs']:
        assert run['reachedResults'] and run['transitions'] == 3
        stages = run['stages']
        rows.append('| ' + ' | '.join([run['name'], f"{run['totalSeconds']:.2f}",
            str(run['collisions']), f"{sum(x['actualDistance'] for x in stages):.1f}",
            f"{sum(x['commandedWaitSeconds'] for x in stages):.1f}",
            f"{sum(x['bracedSeconds'] for x in stages):.1f}",
            str(run['impactLaunches']), str(run['chainLaunches'])]) + ' |')
    stages = [s for r in route['runs'] for s in r['stages']]
    ground_max = max(s['maxGroundNearCoincidentCount'] for s in stages)
    all_max = max(s['maxNearCoincidentCount'] for s in stages)
    worst_traversal = max(s['worstTraversalFrameMilliseconds'] for s in stages)
    fps_values = [1000/s['meanTraversalFrameMilliseconds'] for s in stages]
    docs = ROOT / 'docs'
    shutil.copyfile(args.route_report, docs / 'v3-route-qa-final.json')
    shutil.copyfile(args.impact_report, docs / 'v3-impact-qa-final.json')
    text = f'''# v3 Windows Player 验证

最终构建日志：`{Path(build['buildLog']['path']).name}`。实际 Player 的碰撞专项 **{len(impact['checks'])} 项通过、0 失败**；四路线验收 **{len(route['checks'])} 项通过、0 失败**。两轮测试前记录同一份构建的 {len(build['binaries'])} 个运行文件和 {len(build['sourceFiles'])} 个源码/config/meta 哈希，封包前再次匹配。

测试设备：{route['device']}，{route['graphicsApi']}，Unity {route['engine']}。这是 Windows Player 内的输入驱动自动化，使用移动、冲刺、稳住输入通道；不是系统键盘自动化或真人试玩。

## 撞飞与果冻专项

约 0.9 秒目标总位移：普通轻推 {normal['earlyDisplacement']:.3f} 米，冲刺 {dash['earlyDisplacement']:.3f} 米；这是目标位移，包含自主走路，不等于纯冲量距离。轻推没有击飞；直接冲撞峰高 {dash['peakHeight']:.3f} 米、翻转 {dash['peakSpin']:.0f} 度并恢复落地导航。玩家命中后约 0.45 秒仍前进 {dash['playerAdvance']:.3f} 米，没有自身失衡。

九人测试布置实际击飞 {chain['uniqueTargets']} 人，其中 {chain['chainLaunches']} 次次级连锁，最大深度 {chain['maxDepth']}，同次冲刺无重复命中。普通碰撞、正撞、侧撞、边界、有限速度、物理根节点不缩放、视觉回中、落地导航、暂停、重开和震屏开关通过。

四人墙边回归：实际击飞并落地 {wall['allTargetsLanded']} 人，恢复期真实分离位移合计 {wall['recoverySeparationDistance']:.3f} 米；此值是四人的采样位移之和。玩家使用 Shift 刹车并向左退开，恢复期与人群最小距离 {wall['minRecoveryPlayerDistance']:.3f} 米，普通推挤接触 {wall['normalContacts']} 次，排除玩家继续推开人群作为解释。四人各自恢复导航数量 {wall['targetsNavigationRecovered']}，恢复后地面邻域最大 {wall['maxGroundNearby']}。恢复期分离仍使用有上限的扫掠移动，未放宽速度或地面堆叠阈值。

60 毫秒短停顿每次冲刺最多触发一次，跨批次间隔至少 180 毫秒，连锁不延长停顿。命中时暂停位移和表现推进，但共享通勤计时继续；菜单暂停才冻结计时。截图编码会拉长实际渲染帧，不能把带截图的墙钟间隔当作逻辑停顿预算。

专项使用显式 `--impact-qa` 布置角色，初始化后由普通输入触发碰撞。实际查看了压缩、离地翻转、落地回弹截图；标名 `apex` 的截图取高度大于 1 米的高位帧，不保证恰为数学最高点。完整三关验收使用正常人群和路线，没有传送或直接完成关卡。

## 四条完整路线

| 输入策略 | 全程秒 | 碰撞事件 | 实走米 | 主动等待秒 | 稳住秒 | 击飞总数 | 次级连锁 |
|---|---:|---:|---:|---:|---:|---:|---:|
{chr(10).join(rows)}

表中“主动等待秒”只累计各段 Traverse 内的 `commandedWaitSeconds`，不包含 late 路线前置约 110 秒的正常等待；该等待已计入“全程秒”。

110 秒时限，超时仍完成迟到结算。risk/safe 使用较保守的避让策略；charge 更频繁使用冲撞，其成绩不能视为普通玩家首次试玩成绩。late 包含正常等待超时。数据表反映有限策略样本，不能声称每个人都会在相同时间通关，或难度已完成真人校准。

三段维持 40×100 米地图、120/128/124 人口配置、目的地人流、离屏回收与跟随相机。路线检查覆盖准时/迟到、完整输入通关、静态墙体、镜头跨度和玩家可见性、狭门接触后退再进入、冲刺/稳住组合、重复暂停与重开、换关状态清理、目标到达和离屏回收。

拥挤诊断保留所有活动角色的二维中心统计：最终最大 {all_max}。空中和恢复期角色暂不阻挡玩家，因此另统计会阻挡玩家的地面角色邻域，最大 {ground_max}，沿用原阈值 ≤3；所有活动角色仍作为几何采样中心，探针槽位/代数/位置必须对齐。恢复中的 NPC 仍参与身体分离，故这项玩家通行阈值不等于“恢复期间绝无身体或视觉重叠”。原二维计数未被删除；恢复阶段的真实位移、速度与有限重叠证据见墙边专项及独立 `v3-recovery-audit.json`。持续量是间隔采样中的连续阳性跨度，不是逐帧保证。

## 性能与限制

全部 Playing 状态采样平均帧 {route['meanFrameMilliseconds']:.3f} 毫秒（约 {1000/route['meanFrameMilliseconds']:.2f} FPS），最差帧 {route['worstFrameMilliseconds']:.2f} 毫秒；各段路线执行均值约 {min(fps_values):.2f}–{max(fps_values):.2f} FPS，路线执行最差帧 {worst_traversal:.2f} 毫秒。Playing 采样包含迟到等待及运行中的截图开销，不包含完整启动、菜单、暂停或过场耗时，切换附近的帧可能被计入；不能只报平均值而省略卡顿峰值。

仅覆盖当前 Intel Iris Xe / D3D11 机器和有限输入策略；没有多设备兼容性、真人操作手感或主观听觉评估。角色使用自定义平面碰撞、卡通高度/翻转和视觉节点形变；落地恢复沿现有通勤路径工作，未加入全局寻路系统。专项边界场景与完整路线无法穷举所有墙角组合。

## 证据

- 原始完整路线：`{args.route_report}`，SHA-256 `{sha(args.route_report)}`。
- 原始碰撞专项：`{args.impact_report}`，SHA-256 `{sha(args.impact_report)}`。
- 仓库副本：`v3-route-qa-final.json`、`v3-impact-qa-final.json`，逐字节复制原始报告。
- 测试构建清单：`{args.build_manifest}`，SHA-256 `{sha(args.build_manifest)}`。
- 原型失败和改进过程见 `v3-prototype-log.md`；v1、v2 已有源码、包与报告保持独立。

最终 ZIP 的 SHA-256、CRC、分包合并逐文件校验和解压启动结果另见发行清单；验收报告不预先声称尚未执行的封包/上传完成。
'''
    (docs / 'v3-validation.md').write_text(text, encoding='utf-8')
    for name in ('操作说明-v3.md', 'v3-impact-design.md'):
        p = docs / name
        t = p.read_text(encoding='utf-8')
        t = t.replace('（待最终验收）', '').replace('目前结果待最终运行后补充。', '最终运行结果见 v3-validation.md。')
        t = t.replace('最终通过数、性能、截图和包校验由独立验收记录补充。', '最终通过数、性能与截图检查见 v3-validation.md；包校验见发行清单。')
        t = t.replace('未来收到 v3 ZIP 时需完整解压', '收到 v3 ZIP 后需完整解压')
        t = t.replace('当前仅完成说明与代码逻辑的静态核对，**本文件尚不报告 v3 通过数、性能、通关时间或发行校验值**。这些信息需由最终构建的 Player 实际运行和截图检查补充。',
            f'同一构建已完成实际 Windows Player 验收：碰撞专项 {len(impact["checks"])} 项通过、0 失败，四路线 {len(route["checks"])} 项通过、0 失败。性能、通关时间和限制见 `v3-validation.md`；发行校验由封包后的 `v3-release-manifest.md` 记录。')
        p.write_text(t, encoding='utf-8')
    (ROOT / 'README.md').write_text((ROOT / 'README-v3.md').read_text(encoding='utf-8') + '\n\n保留的 v2：源码 `EarlyRushUnityV2/`，原验收见 [v2 报告](docs/v2-validation.md)。\n', encoding='utf-8')
    print(json.dumps({'routeChecks':len(route['checks']), 'impactChecks':len(impact['checks']), 'groundPileMax':ground_max, 'allActivePileMax':all_max}))

if __name__ == '__main__': main()
