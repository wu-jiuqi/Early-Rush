# 早八冲刺 / Early Rush

Unity 6000.0.26f1 Windows x64 通勤游戏：骑车去地铁站 → 挤地铁 → 公司打卡。

## 当前状态

开发分支 `dev/v2-large-maps` 已同步独立工程 `EarlyRushUnityV2/`，并按工程基线、关卡、玩法、验收代码分组提交。原始 v1 工程与旧 Windows ZIP 保留在本地。

**v2 源码尚未完成 Unity 编译和 Windows Player 实测验收；目前没有已验收的 v2 可运行包。** 下列功能是已写入源码的实现范围，不表示测试已经通过。`drafts/` 是之前的开发草稿，实际工程使用 `EarlyRushUnityV2/Assets/`。

## v2 实现范围

- 三段多屏俯视地图：道路交叉人流、地铁站台与入门瓶颈、公司大厅和走廊。地图提供风险短路与安全绕行路线。
- 平滑跟随镜头、方向预看、边界约束和离屏目标指引；保留可辨认的人物尺寸。
- 冲刺与稳住操作、碰撞恢复、共享计时、暂停和重开；超时后仍可完成打卡并得到迟到结算。
- 可选的真实 Windows Player 输入驱动验收代码：通过正常移动、冲刺和稳住输入走完路线，记录风险/安全路线、准时/迟到、镜头、暂停、重开、碰撞和截图等证据。

验收代码使用 Player 内输入通道，不是系统键盘自动化，也不能代替真人体验评价。必须实际运行并检查报告与截图后，才能报告通过、用时、难度差异或最终性能。

## 运行与构建

1. 使用 Unity 6000.0.26f1 打开 `EarlyRushUnityV2/`。
2. 打开 `Assets/Scenes/EarlyRush.unity`，进入 Play。
3. Windows x64 构建入口为 `BuildEarlyRush.Build`：

```text
Unity.exe -batchmode -quit -projectPath <工程绝对路径> -executeMethod BuildEarlyRush.Build -logFile <构建日志路径>
```

构建脚本输出到工程上一级的 `artifacts/EarlyRush-V2-Windows/EarlyRush.exe`。构建成功后可使用 `EarlyRush.exe --qa --qa-output=<绝对输出目录>` 运行输入驱动验收，报告不随源码自动生成。不要仅凭工具代码存在就认定游戏通过测试。

## 操作

WASD / 方向键移动，Space 冲刺，Shift 稳住抗推，Esc 暂停，M 切换声音。Enter 用于开始或确认教学，R 用于暂停或结算时重开。

## 源码

- `RushGame.cs`：输入、运动、碰撞、镜头和流程。
- `RushWorld.cs`：程序化关卡与路线；`RushVisuals.cs`：角色与场景造型。
- `RushUI.cs`、`RushRouteHUD.cs`：中文界面和目标指引。
- `RushQA.cs`：可选 Player 输入驱动验收。
- `Assets/Editor/BuildEarlyRush.cs`：Windows x64 构建入口。

仅提交源码与必要配置；排除 Library、Temp、Logs、UserSettings、构建二进制、缓存、密钥和无关数据。不强制推送。最终包、实测对比、截图、校验值和已知限制须在完成验收后另行交付。
