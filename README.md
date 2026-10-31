# 早八冲刺 / Early Rush

Unity 6000.0.26f1 Windows x64 通勤游戏：骑车去地铁站 → 挤地铁 → 公司打卡。

## 工程与开发状态

`EarlyRushUnityV2/` 是独立的 v2 开发工程。本次工程基线提交导入已存在的 v1 源码、Unity 元数据、Packages 与 ProjectSettings，后续按功能提交大地图、相机和玩法、验收工具。原始 v1 工程及 Windows ZIP 保留在本地，不在此提交中覆盖。

开发分支：`dev/v2-large-maps`。当前基线仍是 v1 的小场景逻辑；`drafts/` 保存之前的大地图开发草稿，尚未整合为本次基线。v2 的目标为多屏地图、平滑跟随镜头、可预判人流和风险/安全路线。Windows 执行环境已恢复到能读取工程；v2 编译、Player 实测及最终包验收尚未完成，不能将此提交视为可运行 v2 交付。

## 运行与构建

1. 使用 Unity 6000.0.26f1 打开 `EarlyRushUnityV2/`。
2. 打开 `Assets/Scenes/EarlyRush.unity`，进入 Play。
3. Windows x64 构建入口为 `BuildEarlyRush.Build`。可通过 Unity 命令行传入 `-batchmode -quit -projectPath <工程绝对路径> -executeMethod BuildEarlyRush.Build`。

WASD / 方向键移动，Space 冲刺，Shift 稳住抗推，Esc 暂停，M 切换声音。三段共享计时，超时后仍可继续完成打卡并得到迟到结算。

## 源码范围

- `Assets/Scripts/RushGame.cs`：输入、运动、碰撞和流程。
- `RushWorld.cs`：程序化场景；`RushVisuals.cs`：角色和场景造型；`RushUI.cs`：中文界面。
- `RushQA.cs`：可选 Player 输入驱动验收；现有基线测试不代表 v2 已通过验收。

仅提交项目源码和必要配置；不提交 Library、Temp、Logs、UserSettings、构建包、缓存、密钥或无关数据。功能提交使用中文说明，不强制推送。最终 Windows 包、截图、实测报告和校验值须在完成验收后另行交付。
