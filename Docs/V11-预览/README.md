# V11 成品运行预览

PNG 来自 Windows 成品的 `--v11-runtime-qa`，直接截取实际游戏窗口，包含 HUD。

- `population-stage-*-local-input.png`：保留生成的人群，把观察者初始位置布置在高峰段后，以真实 Tick 和移动输入推进两秒；不是从出生点完成整关的证据。
- `fixture-unbraced-npc-windup.png`：受控单个路人场景，真实模拟触发蓄力与锁定方向。
- `fixture-player-knockback.png`：同一受控场景中未稳住，实际命中产生视觉腾空。
- `fixture-brace-block.png`：受控场景按住稳步，实际接触阻止击飞，HUD 显示抵挡结果。

受控场景隐藏其他路人，仅用于隔离动作表现。完整主线/三任务输入回放和默认时限结果见 `../V11-验证结果.txt`；运行检查见 `../V11-运行检查.json`。占位美术不代表最终视觉质量。
