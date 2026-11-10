# V12 成品运行截图

这些截图由 `RushRuntimeCheckV12` 在 Windows 成品中直接捕获，使用当前占位美术。

- `pedestrians-stage-0/1/2-t00.png / t05.png / t15.png`：街道、地铁、办公室第一个语义高峰节点的前后对照。使用默认完整人口（760/900/700），观察者起点安排在可走肩侧，随后零移动输入；人群与碰撞仍实际推进。保留原相机尺寸 7.5，实际相机中心、取景点和局部人数逐图记录在运行 JSON 的 `views` 中；真实接触仍可能推移观察者。
- `pedestrians-stage-*-task-*-entry/workspace-t15.png`：同一批 NPC 在同一 15 秒时刻的任务入口和工作区；`pedestrians-stage-*-quiet-loop-t15.png` 为旁路对照。仅调整观察者取景，期间不 Tick、不移动或清除 NPC，不作为玩家实际走到该处的证明。
- `fixture-player-dash-launch.png`：玩家输入冲刺后，真实接触使普通行人腾空。为便于观察，双方初始位置经过安排；截图不是最高点，也不表示自然遭遇率。
- `fixture-player-knockback.png`：受控单个赶路者冲撞触发玩家腾空，验证受击表现；自然行人的冲撞决策另由独立 AI 专项验证。
- `fixture-coffee-guard.png`、`fixture-coffee-spill.png`：受控端杯同事在真实冲刺靠近与接触中产生护杯、洒杯反应，没有手工设置结果状态。

运行检查每渲染帧推进四个模拟 Tick；它验证行为与画面，不是帧率基准或人工试玩。完整规则、检查结果和六条路线分别见上级目录的 `V12-独立行人AI与双向撞飞.md`、`V12-运行检查.json` 与 `V12-验证结果.txt`。原始前后个体样本、39 张截图与事件轨迹保存在本机成品的 `QA/runtime-v12-congestion`。另有三关 60 秒固定热点观察，见 `V12-高峰密度持续观察.txt`。
