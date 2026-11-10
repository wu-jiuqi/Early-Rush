# Early Rush · 三阶段通勤

本分支基于 `dev/v9-review`，按《Early Rush 三阶段关卡设计方案 v3》继续开发。Unity **6000.0.26f1**；当前入口是 `Assets/Scenes/EarlyRushV10.unity`。

## 运行

用 Unity Hub 打开仓库根目录，打开 V10 场景，按 Play。每阶段独立 7 分钟，准备页按“出发”才计时。WASD / 方向键移动，Esc 暂停，R 回位附近未完成物件，Tab 展开地图，F1 操作提示。任务可选，按时抵达即可推进。

三个阶段分别有早餐/修车/翻板、候车/扭蛋/清洁、咖啡/文件/椅子任务。完成一项与完成三项各加一星；超时的任务星只记挑战结果，不解锁下一阶段。

## 构建与验收

Unity 菜单 `Early Rush > V10 > Build Windows` 会先验证规则、镜头、100 个种子的三阶段地图、主线与默认 420 秒下的完整任务输入回放，再构建到 `Builds/EarlyRush-V10/EarlyRush.exe`。

也可运行 `Tools/Build-V10.ps1 -UnityPath <Unity.exe路径>`。运行成品附加 `--v10-runtime-qa` 会自动生成三阶段静态截图和运行检查到成品目录的 `QA/runtime`，随后退出。

仓库缺少的 V8 美术以确定性占位图显示，可直接构建。将正式 PNG 放入 `Assets/Resources/V8Art/<资源键>.png` 后会优先使用原图。占位美术不代表最终视觉品质。

实现范围、验证证据与后续工作见 [V10 制作记录](Docs/V10-设计v3优化.md)。历史 V7/V8/V9 脚本与构建入口仍保留，当前构建应使用 V10。

美术制作见 [素材清单与接入规范](Docs/美术交付/EarlyRush-美术素材清单与接入规范-v1.md) 和 [63项生产清单](Docs/美术交付/EarlyRush-美术生产清单-v1.csv)。2026-11-11新增的空格冲刺、Shift稳步、路人主动冲撞与人流扩展已列入美术需求，当前仅完成文档，程序仍待下一轮实现。
