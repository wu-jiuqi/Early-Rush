# 早八冲刺 / Early Rush v2

Unity 6000.0.26f1 制作的 Windows x64 通勤游戏：骑车赶地铁 → 穿过站台挤入车厢 → 穿过公司大厅和走廊打卡。

本次 v2 将三段扩展为多屏俯视地图，加入平滑跟随镜头、密集的目的地人流、路线选择和可恢复的冲撞失衡。最终候选 build5 已成功构建；该构建目录中的实际 Windows Player 验收 run4 已完成，**240 项通过、0 项失败**（`passed=true`、`failureCount=0`）。详细指标、测试口径与限制见 [v2 验收报告](docs/v2-validation.md) 和 [仓库中的规范化 JSON 副本](docs/qa-final.json)。本地原始 JSON 与该副本的字节格式可能不同，SHA-256 分别记录于报告，不能混用。

## 运行与发行文件

已生成的本地 Player 位于：

```text
D:\CodexWorkspace\2026-10-31\task-4\artifacts\EarlyRush-V2-Windows\EarlyRush.exe
```

直接从该完整目录运行 `EarlyRush.exe`。必须保留 `EarlyRush_Data`、`UnityPlayer.dll`、`MonoBleedingEdge` 等全部随包依赖；玩家无需安装 Unity、登录或联网。

| 按键 | 操作 |
|---|---|
| WASD / 方向键 | 移动 |
| Space | 短冲刺；观察冷却，松开后再按 |
| Shift | 稳住抗推，减速并取消当前冲刺 |
| Esc / M | 暂停或继续 / 切换声音 |
| Enter | 开始、确认教学或结算后再次出发 |
| R | 暂停或结算时从头重开 |

Windows 发行文件为 `EarlyRush-V2-Windows-x64.zip`，源码发行文件为 `EarlyRush-V2-Unity-Source.zip`。请将 Windows ZIP 完整解压后运行，不能只取 EXE。包的字节数、SHA-256 及解压完整性核验记录独立保存于工作区 `artifacts/EarlyRush-V2-SHA256.txt` 和交付清单；本说明不嵌入 ZIP 自身哈希。

三段共享 **110 秒**时限；暂停和阶段转换不消耗通勤计时。超时后仍可继续完成流程并得到迟到结算。青色提示指向目标，头顶“你”标识辅助寻找玩家。详细说明见 [运行与操作说明](docs/操作说明-v2.md)。

## 规模与玩法变化

| 项目 | v1 基线 | 最终 v2 |
|---|---|---|
| 每段地图边界 | 17 × 23 米 | 40 × 100 米 |
| 相机 | 单屏俯视 | 随玩家推进，带预看、边界限制和离屏指引 |
| 三段 NPC 数 | 9 / 9 / 7 | 120 / 128 / 124 |
| 通勤选择 | 小场景穿行 | 风险短路、安全绕行、让行、冲刺和稳住 |

v1 的地图和 NPC 数来自本次只读源码核对，本次没有重新运行 v1。前线程的 11.8 秒属于历史测试记录，不能当作本次重测结果。v2 早期试版 run1 的 58 / 66 / 65 名 NPC 是大地图加密前的数据，也不是 v1 数量。

行人朝目的地推进，在路口和入口汇流，离屏后回收补入。冲刺硬闯人群可能失衡；稳住、侧移或短暂让行提供恢复手段，短暂保护避免被连续失衡锁住。110 秒时限由密集人流版本的实际路线测试调整；难度变化同时来自地图分段、人流交汇、瓶颈和动作决策。

## 验收口径

测试运行于真实 Windows Player，通过游戏的移动、冲刺、稳住输入通道走完路线，不传送玩家或直接跳关。run4 包含风险、绕行、直冲及正常等待超时后的完整通关，并检查暂停、重开、切换、碰撞边界、镜头可见性、狭门退回再进入和离屏人流行为。

这属于 **Player 内输入驱动自动化**，不是操作系统键盘自动化，也不是真人试玩。风险/绕行策略主动避让且较保守，数据不是熟练玩家的最优路线成绩。性能需区分稳态遍历与包含换关、等待的整体采样；准确值见验收报告。当前仅覆盖本测试机与有限策略样本，尚未完成多设备或真人手感评估。

## 源码与构建

使用 Unity 6000.0.26f1 打开 `EarlyRushUnityV2/`，打开 `Assets/Scenes/EarlyRush.unity` 后可进入 Play。命令行构建：

```text
Unity.exe -batchmode -quit -projectPath <EarlyRushUnityV2绝对路径> -executeMethod BuildEarlyRush.Build -logFile <构建日志路径>
```

Windows 输出目录为工程上一级的 `artifacts/EarlyRush-V2-Windows/`。构建后可用 `EarlyRush.exe --qa --qa-output=<绝对目录>` 复跑输入验收。

- `RushGame.cs`：输入、运动、人流、碰撞、镜头和关卡流程。
- `RushWorld.cs` / `RushVisuals.cs`：程序化关卡、路线、角色和场景造型。
- `RushUI.cs` / `RushRouteHUD.cs`：中文界面和目标、玩家状态提示。
- `RushQA.cs`：Player 输入验收；`Assets/Editor/BuildEarlyRush.cs`：构建入口。

圆润卡通灰盒美术与短音效由项目代码生成，没有购买付费资产。原始 v1 工程和旧 ZIP 保留在本地。发行包路径、字节数与 SHA-256 以工作区 `artifacts/EarlyRush-V2-SHA256.txt` 和交付清单为准。包内文件校验使用 `FILES.sha256`；ZIP 自身校验保留在包外，避免自循环。

仓库按功能保存中文提交，排除 Library、缓存、构建二进制和凭据。所有分支更新不强制推送，保留开发历史。
