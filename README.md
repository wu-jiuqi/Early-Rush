# Early Rush · 三阶段通勤

本分支基于 `dev/v9-review`，按《Early Rush 三阶段关卡设计方案 v3》继续开发。Unity **6000.0.26f1**；当前入口是 `Assets/Scenes/EarlyRushV11.unity`。

## 运行

用 Unity Hub 打开仓库根目录，打开 V11 场景，按 Play。每阶段独立 7 分钟，准备页按“出发”才计时。WASD / 方向键移动，Space 短冲刺，按住任一 Shift 稳步抗撞（骑车时稳车减速），Esc 暂停，R 回位附近未完成物件，Tab 展开地图，F1 操作提示。任务可选，按时抵达即可推进。

三个阶段分别有早餐/修车/翻板、候车/扭蛋/清洁、咖啡/文件/椅子任务。完成一项与完成三项各加一星；超时的任务星只记挑战结果，不解锁下一阶段。

## 构建与验收

Unity 菜单 `Early Rush > V11 > Validate and Build Windows` 会先验证规则、动作、人流、镜头、100 个种子的三阶段地图、主线与默认 420 秒下的完整任务输入回放，再构建到 `Builds/EarlyRush-V11/EarlyRush.exe`。

也可运行 `Tools/Build-V11.ps1 -UnityPath <Unity.exe路径>`，加 `-ValidateOnly` 仅验证，加 `-Quick` 只做快速专项、不证明路线完成。运行成品附加 `--v11-runtime-qa` 会自动生成三阶段人流样本与冲刺/冲撞/稳步运行检查到成品目录的 `QA/runtime-v11`，随后退出。

仓库缺少的 V8 美术以确定性占位图显示，可直接构建。将正式 PNG 放入 `Assets/Resources/V8Art/<资源键>.png` 后会优先使用原图。占位美术不代表最终视觉品质。

实现范围与后续工作见 [V11 动作与早高峰人流](Docs/V11-动作与早高峰人流.md)，原九任务实现见 [V10 制作记录](Docs/V10-设计v3优化.md)。历史脚本与构建入口仍保留，当前构建应使用 V11。

美术制作见 [素材清单与接入规范](Docs/美术交付/EarlyRush-美术素材清单与接入规范-v1.md) 和 [63项生产清单](Docs/美术交付/EarlyRush-美术生产清单-v1.csv)。新增动作已有状态驱动的占位姿态和地面阴影，正式序列帧播放器与美术仍待接入。
