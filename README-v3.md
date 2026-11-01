# 早八冲刺 / Early Rush v3

v3 延续三段大地图通勤：骑车赶地铁、汇入车门、冲向公司打卡点。所有人仍在赶路，新增可见的卡通撞飞和果冻回弹。

普通接触轻推；按 Space 冲刺命中会让通勤者短暂压缩、腾空翻转、落地弹跳，再恢复前进。玩家保留前冲动量。连锁共用每次冲刺最多 6 人、最多 2 层的限制，避免无限传播。短暂停顿、冲击声和星圈强化命中；菜单和暂停页可关闭轻微震屏。

使用 WASD／方向键移动，Space 冲刺，Shift 稳住抗推，Esc 暂停，暂停或结算时 R 重开，M 切换声音。Shift 会取消冲刺。三关共享 110 秒，超时仍可完成并获得迟到结算。暂停冻结通勤计时；命中的短暂停顿仍计入时间。

Windows 成品需要完整解压后运行 `EarlyRush.exe`，保留旁边的 `EarlyRush_Data`、`UnityPlayer.dll`、`MonoBleedingEdge` 等全部依赖。两份普通 ZIP 必须“全部提取”到完全相同的新建文件夹；详细步骤随包提供。

- [操作说明](docs/操作说明-v3.md)
- [碰撞与表现设计](docs/v3-impact-design.md)
- [实际 Player 验证与限制](docs/v3-validation.md)

源码在 `EarlyRushUnityV3/`，使用 Unity 6000.0.26f1，Windows x64 / D3D11。`EarlyRushUnityV2/` 及 v2 包独立保留。构建命令：

```text
Unity.exe -batchmode -nographics -quit -projectPath <EarlyRushUnityV3绝对路径> -executeMethod BuildEarlyRush.Build -logFile <日志绝对路径>
```

输出为项目上一层 `artifacts/EarlyRush-V3-Windows/`。开发验收开关为 `--impact-qa --impact-qa-output=<目录>` 和 `--qa --qa-output=<目录>`。前者使用明确启用的碰撞测试布置；后者通过游戏输入通道走完整三关。它们是实际 Windows Player 内自动输入测试，不是人工试玩或系统键盘自动化。
