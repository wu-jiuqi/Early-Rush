# V15 · 图标 UI 实际接入说明

本轮基于提交 `847668c`，在分支 `feat/v15-ui-kit-integration` 接入 UI 素材包。开发环境为 Unity **6000.0.26f1**，使用 Unity UI（`com.unity.ugui`）**2.0.0**。本文件记录已落地代码；素材包内的原接入建议不应被当成这一版的实现状态。

## 1. 当前入口与已实现范围

打开 `Assets/Scenes/EarlyRushV15.unity`，按 Play。场景中的 `RushPlayV9` 沿用现有三章地图和 `RunV9` 模拟；Awake 创建 `RushUIV15`，由后者在运行时建立 Canvas、HUD、菜单、世界提示、小地图和效果池。界面对象目前由 C# 构建，不是手工拖入场景的 Prefab。

已实际接入：

- 出发选关：三章插画卡、锁定、章节任务预览、动态时限、开始、换种子、声音、帮助和设置。
- 暂停：继续、同种子重试、换种子、返回准备页、声音、帮助和设置。
- HUD：剩余时间、九任务状态、进度／数量、翻板目标与当前图案、扭蛋节拍、文件头像、冲刺冷却、稳步、步行／骑行／受击状态。
- 小地图：动态道路底图、真实玩家位置和朝向、任务／终点、离屏方向、文件大致区域、展开与收起。
- 世界提示：任务设施气泡、文件工位头像、翻板当前与目标、扭蛋节拍、过街信号、持杯同事提示、普通行人冲撞方向。
- 结算：成功／超时、按章节切换的目的地、独立人物插画、星星、三项任务结果、重试、主页及合法的下一章入口。
- 教学与设置：修车／翻板／送文件三组图解，其他可见任务的短说明；音量、静音和减弱动态效果。
- 动画：按钮按压补间、任务和星星弹跳、翻板压缩换面、三组 UI 序列帧反馈。

新界面存在时，`RushPlayV9.OnGUI()` 直接返回，旧 HUD 不再同时绘制。世界地图、角色、任务碰撞、移动、冷却、完成判定及星级规则继续由既有代码负责，UI 不写入任务结果。

## 2. 素材位置与导入

运行时资源位于 `Assets/Resources/UIV15/`，共 **110 张 PNG**：43 图标、5 头像、28 基础组件、6 插画、9 教学图、1 菜单背景和18张 FX 帧。清单为同目录的 `manifest.json`；`U15` 按清单 ID 加载 Sprite，缺少资源会显式报错。

28个基础组件的可编辑 SVG 源文件保留在 `Docs/V15-UI源文件/Vector/`，便于后续改色和尺寸调整。Unity 实际加载对应的 PNG；修改源文件后需重新导出 PNG，并同步清单中的尺寸、边界与 SHA256，再执行导入校验。

| 工程路径 | 内容／用途 |
| --- | --- |
| `Assets/Resources/UIV15/Icons/` | 任务、章节、动作、功能按钮、勾叉与星级图标 |
| `Assets/Resources/UIV15/Portraits/` | `portrait_00`～`portrait_04`，按领件人 ID 复用 |
| `Assets/Resources/UIV15/Components/` | 面板、按钮、环、轨道、气泡、填充与边框 |
| `Assets/Resources/UIV15/Illustrations/` | 三章卡片、开心／疲惫主角与地铁入口 |
| `Assets/Resources/UIV15/Tutorials/` | 修车、翻板、送文件各3张静态步骤图 |
| `Assets/Resources/UIV15/Backgrounds/` | 菜单氛围背景，不作为玩法地图 |
| `Assets/Resources/UIV15/FX/` | `success_sparkle`、`impact_puff`、`failure_spill`，每组6帧 |

`Assets/Editor/RushUIV15Import.cs` 限定处理清单中的资源，保留 PNG 源字节。菜单 `Early Rush > V15 > Import UI Art` 应用 Sprite/Single、PPU100、FullRect、自定义 pivot/border、原始透明通道、Bilinear、Clamp、无 Mipmap、非 Read/Write、无重缩放及无压缩；`Validate UI Art` 复核数量、哈希、尺寸、边界与导入参数。

组件 ID 带 `component.` 前缀，文件名省略此前缀；例如 `component.button_teal` 对应 `Components/button_teal.png`。`action_walk` 是 `stage_street` 的别名。FX ID 使用 `success_sparkle.frame_00` 等形式。数值、倒计时、小地图、填充进度和任务图标组合均由运行时生成，不烘焙在界面截图中。

## 3. 运行与操作

默认启动使用当前 V4 三章布局入口。该种子默认阶段时限为600秒；其他配置可使用420秒或自定义时限。准备页和 HUD 从当前 `run.limit` / `run.Remaining` 读取数据，按开始后才计时；暂停、失焦及准备页不推进模拟。

| 输入 | 行为 |
| --- | --- |
| WASD／方向键 | 移动与转向 |
| Space | 点按冲刺；按住不自动连发 |
| 左／右 Shift | 稳步抗撞；骑行时稳车减速 |
| R | 救回附近最近的未完成物体 |
| Tab | 展开／收起地图 |
| Esc | 优先关闭帮助、教学或设置；没有弹层时暂停／继续 |
| F1 | 打开／关闭帮助 |
| 鼠标 | 点击任务槽看说明，点击图标按钮执行功能，拖动设置音量 |

从游玩中打开帮助／教学／设置会先暂停；关闭时仅恢复由该弹层主动暂停的同一局。原本已经暂停时打开说明，关闭后仍保持暂停。弹层使用 CanvasGroup 隔离其下按钮，HUD 在暂停和菜单期间禁用交互。运行中关闭 EventSystem 导航事件；暂停和菜单恢复导航。菜单开始／继续会记录当前帧，抑制该帧的冲刺输入，避免 Space 确认菜单时同时起冲。

设置持久化键为 `V15_UiVolume`、`V15_UiMuted`、`V15_ReducedMotion`，实际音量通过 `AudioListener.volume` 应用。减弱动态效果保留状态更新，减少按钮回弹、任务脉冲、翻板挤压和装饰 FX。

## 4. 菜单、任务与存档规则

`RushMenusV15` 根据 Run 引用或 phase 变化重建主页面；`RushHudV15` 逐帧绑定连续状态，用前后快照触发一次性反馈。

| 任务 | 当前 UI 绑定 |
| --- | --- |
| S1 早餐 | 进食百分比／进度，碰撞已用次数与容忍上限，完成勾或失败叉 |
| S2 修车 | 两个轮子格与归位计数；只有模拟 `bicycle` 为真才切骑行图标 |
| S3 翻板 | 分别读取 `boardGoal`、`boards`；真实目标不固定为教学中的全太阳 |
| M1 隐藏挑战 | 准备预览与游玩 HUD 为中性槽；无进度、地图标记或具体帮助；失败最多显示通用叉，结果页才揭示内容 |
| M2 扭蛋 | 数量、进度、`BeatPhase` 指针；有效窗由 `beatWindow / beatPeriod` 决定 |
| M3 清洁 | 真实有效清理面积百分比，地面变化仍来自模拟 `dirt.cleaned` |
| O1 咖啡 | 打翻杯数／容忍上限，洒落 UI 反馈与失败状态 |
| O2 文件 | 实际数量与期限，最多5位对应头像，已送达头像降亮度并显示勾 |
| O3 椅子 | 真实稳定归位计数与空／满格，数量来自 task.limit |

仍遵循“准时抵达1星、完成至少1项加1星、完成3项再加1星”。超时可以有任务星，但不会解锁下一章或更新成功最高星。`SelectStageFromUI` 在准备页检查解锁；在结果页只允许成功后进入精确的下一章，末章回第0章。离开结果页的下一章、重试与主页入口先调用幂等的 `RecordCurrentResult()`，避免同帧点击跳过存档。

小地图复用 `RushMinimapProjectionV13`，底图和标记共享投影；玩家标记使用地面位置，受击升空不改变地图坐标。M1 不在地图中出现。文件提示仍量化到大致区域，具体工位通过相同头像识别。

## 5. 已接入的补间与序列帧

所有动画仅改变表现。奖励、翻板逻辑、椅子归位、文件完成、列车状态和水渍清除均由模拟决定。

| 实现 | 当前代码入口 | 时长／频率 | 触发与时间来源 |
| --- | --- | --- | --- |
| 按钮按下／释放 | `U15.Button` → `EarlyRushUITween.Press/Release` | 按下0.08秒、缩至0.94；释放0.14秒回弹 | 鼠标按压，Unscaled；视觉子节点缩放，命中区域不变 |
| 任务图标弹跳 | `RushHudV15.RefreshTask` | 0.26秒，峰值约1.12倍 | 任务完成／失败或归位、送达计数增加；以游戏时间增量推进 |
| 稳步抵挡强调 | `RushHudV15.Refresh` | 0.24秒颜色脉冲 | `BraceBlockCount` 增加 |
| HUD 翻板换面 | `RushHudV15.RefreshBoard` | 0.18秒，第0.09秒换图，最小宽度0.08倍 | 对应布尔值变化，读取真实状态后做视觉挤压 |
| 世界翻板提示换面 | `RushWorldHintsV15.Refresh` | 0.18秒，中点换图 | 世界任务板发生变化；暂停不推进 |
| 结算星星 | `RushMenusV15.UpdateResultStars` | 首星延迟0.25秒，每星间隔0.18秒；单星0.24秒弹入 | 按成功／任务完成数揭示，菜单时间；减弱动画时直接显示 |
| 完成闪光 | `RushUIV15.PlayFx("success_sparkle", …)` | 6帧，12fps，约0.5秒 | 可见任务完成、结算星星；一次播放 |
| 冲击烟团 | `PlayFx("impact_puff", …)` | 6帧，18fps，约0.333秒 | 玩家撞飞或被撞飞计数增加；一次播放 |
| UI 洒落 | `PlayFx("failure_spill", …)` | 6帧，12fps，约0.5秒 | 早餐／咖啡任务失败或新咖啡打翻；一次播放 |

三段帧都按 `frame_00`～`frame_05` 排列。`EarlyRushUISequence` 非循环完成后发一次 Completed，效果池隐藏并复用 Image；池上限12个。游戏 FX 使用 Manual 时钟，由 `run.elapsed` 正差值推进，位于菜单层下；菜单星光使用 Unscaled 并放在独立 Menu Effects 层。失焦会冻结显示反馈；切换 Run 会取消旧 FX，同一局进入 Results 也会取消游戏 Manual FX。打开帮助等弹层时菜单效果层透明，既有帧继续完成并回收。`failure_spill` 只是 UI 失败表现，不会判定真实咖啡洒落。

同一局的结算页因关闭帮助等原因重建时，星星不会重复播放入场。按钮辅助文字仅在鼠标悬停时出现，初始键盘选中不会自动显示文字。

教程的9张图按三步静态展示，保持原比例；它们不是世界人物的连续动作帧。现有世界行走、骑行、推物、受击、列车等仍使用原世界美术和状态驱动姿态，本轮没有新增四方向人物序列帧。

`EarlyRushUITween.Pop/FadeIn/FadeOut` 等通用接口已经随代码保留，但目前没有把全部弹窗和插画改成淡入／漂浮动画。已接入的效果以本表为准。

## 6. 已导入的预留素材

110张均导入并能通过 catalogue 访问，不代表每张都在当前屏幕常驻。以下资源已保留后续设计用途：

- `component.focus_outline`：独立焦点框；当前按钮焦点主要使用 Selectable 颜色反馈。
- `component.flip_tile_cream/teal`：独立翻板底；当前 HUD／气泡直接显示日月图标。
- `component.panel_teal`、`component.shadow_soft`、`component.divider`：深色面板、独立阴影与分隔线的扩展组合。
- `component.glyph_pause_cream`、`component.glyph_close_cream`：深色按钮上的浅色符号变体；当前暂停／关闭多用奶油底与深色图标。
- `unlock`、独立 `play` 图标：解锁过渡和浅色按钮变体；当前开始主按钮使用奶油播放符号，已解锁卡直接去掉锁。

头像共5个，默认3位领件人只显示其中对应部分；实际 fileCount 更大时使用其余资源。第三方补间库不是本实现的依赖。

## 7. 构建与旧界面对照

Unity 菜单 `Early Rush > V15 > Prepare Validate and Build Windows` 会依次准备 V15 场景、应用并校验110资源、执行专项规则检查，再生成 Windows x64 Development 构建：

`Builds/EarlyRush-V15/EarlyRush.exe`

准备步骤会重新创建 `Assets/Scenes/EarlyRushV15.unity`，并把它设置为构建入口；若正在手改 V15 场景，请先保存需要保留的内容。V13/V14 场景不被此构建器覆盖。

对应命令行入口为 `BuildEarlyRushV15.Build`。例如在工程根目录执行：

```powershell
$unityV15 = '<Unity 6000.0.26f1 的 Unity.exe 绝对路径>'
& $unityV15 -batchmode -quit -projectPath (Get-Location).Path -executeMethod BuildEarlyRushV15.Build -logFile Builds/v15-build.log
```

仅检查可用菜单 `Early Rush > V15 > Validate UI and Focused Mechanisms` 或方法 `BuildEarlyRushV15.Validate`。它覆盖资源、基础动作、冲刺节奏／缓冲、小地图投影、咖啡机制；不执行完整三章路线回放或所有种子检查。

运行 EXE 时附加 `--legacy-ui` 可使用旧 OnGUI 做对照。该参数只切换 UI 路径，不等于退回旧版地图或旧版玩法。历史 `--v13-runtime-qa`、`--v12-runtime-qa`、`--v11-runtime-qa` 保留其旧布局诊断；本轮新界面验证使用下面的 V15 参数。

## 8. V15 运行检查与证据范围

```powershell
& .\Builds\EarlyRush-V15\EarlyRush.exe --v15-ui-qa
```

默认输出在 EXE 同级 `QA/ui-v15/`，即通常为 `Builds/EarlyRush-V15/QA/ui-v15/`。可用 `--v15-qa-output=<绝对目录>` 指定独立输出位置。脚本结束后自动退出，成功返回0，失败返回1。

| 输出 | 内容 |
| --- | --- |
| `QA/ui-assets.json` | 导入数量、源哈希、Sprite尺寸、pivot、border、采样与压缩设置 |
| `QA/validation-focused.txt` | 资源与既有机制专项验证 |
| `QA/build-summary.txt` | BuildPlayer 结果、错误、警告及大小 |
| `QA/ui-v15/report.json` | GraphicRaycaster命中、按钮listener、状态、投影、节拍网格、帧播放及截图检查记录 |
| `QA/ui-v15/*.png` | 准备、暂停、设置、帮助、三种教学、三章HUD、全图与结果状态截图 |
| `QA/ui-v15/README.txt` | 本次运行检查范围与记录说明 |

运行脚本先用 GraphicRaycaster 检查按钮中心的最高命中目标和弹层遮挡，再调用实际 uGUI `Button.onClick` listener，并手动推进既有 `RunV9.Tick`；三章节、任务和结果状态中含显式测试夹具。覆盖1280×720、1920×1080、1024×768，比较新地图实际 RectTransform 屏幕位置与统一投影，检查隐藏 M1、节拍有效窗实际网格、超时权限、重试、下一章与三段序列帧。初始后台窗口会先切换尺寸预热渲染，黑截图最多重抓3次；仍无画面会使检查失败。

这些检查使用合成射线数据，不发送操作系统鼠标／键盘事件，也不证明完整关卡路线、真人节奏、性能或三章实际通关。脚本备份并在正常、异常及超时结束路径恢复相关 PlayerPrefs，记录恢复结果。

## 9. 当前验证状态

2026-11-11 最终交付已通过 Unity 编译与 Windows 构建：Succeeded、0 errors、1项既有警告（`RushJourneyQA.bestAdvance` 未使用字段）。110张资源的源哈希和导入设置全部通过；756条既有机制断言通过（基础动作43、冲刺80、小地图321、咖啡312）。

`V15-UI-runtime-3` 运行套件252项检查全部通过，无运行错误，相关 PlayerPrefs 已恢复。21张实际运行截图均在第一次捕获获得有效画面，覆盖三章和三种分辨率；45次小地图标记比较的最大屏幕误差约0.000016像素。已人工查看准备、暂停、设置、帮助、教学、各章HUD、地图和结算的代表截图，修正章节标记遮挡、音量条对比及节拍窗比例。

源码内的本次验证快照与截图见 [V15验收记录](V15-验收/README.md)。本轮没有执行完整三章路线或真人通关验收，运行夹具的范围见上一节。

## 10. 实现文件索引

- `Assets/Scripts/V15/U15.cs`：资源目录、组件构建、按钮与提示。
- `RushUIV15.cs`：Canvas、刷新、音量／动画设置、FX池、坐标转换。
- `RushMenusV15.cs`：准备、暂停、结算、帮助、教学与设置。
- `RushHudV15.cs`、`RushMinimapV15.cs`：连续状态绑定及地图投影。
- `RushWorldHintsV15.cs`：世界坐标提示与翻板／节拍反馈。
- `Helpers/EarlyRushUITween.cs`、`Helpers/EarlyRushUISequence.cs`：补间与帧播放器。
- `RushUIRuntimeCheckV15.cs`：运行状态夹具、按钮和画面检查。
- `Assets/Scripts/V9/RushPlayV9.cs`：新 UI 初始化、命令桥接与旧 OnGUI 排他开关。
- `Assets/Editor/RushUIV15Import.cs`、`BuildEarlyRushV15.cs`：导入、验证、独立构建入口。

V15 目录下的短文件名均相对 `Assets/Scripts/V15/`。
