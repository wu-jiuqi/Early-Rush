# v3 Windows 发行清单

源码功能提交：`f5f8157e2fb39fa138c11f850dbfadfde8c33f9a`；独立 v3 工程保留 v2 基线。最终构建为 build6，实际 Player 专项 189/0，四路线 260/0，详见 `v3-validation.md`。报告副本与原文件 SHA 一致，未把旧版通过数计入新版本。

| 文件 | 字节 | SHA-256 |
|---|---:|---|
| EarlyRush-V3-Windows-x64.zip | 32366320 | `54FBBB85559788EEF900503021F2F4877A42E6E3744A307E3AEAAB0B320992E7` |
| EarlyRush-V3-Unity-Source.zip | 139047 | `02F348C86BFBFCA79621487CB1F4B443550A3DC101606E5C89617290FDBE905C` |
| EarlyRush-V3-1-of-2-Engine.zip | 14736216 | `2CC076182C2A5C793A9BE118D7324209371273F85D1056B086ACB5D38197F4BA` |
| EarlyRush-V3-2-of-2-Game.zip | 17630945 | `426857C5F6A570117EC1E29A47E3B41BD301D5B8D45FB2419335655A828DCAC4` |

文件位于 `D:/CodexWorkspace/2026-10-31/task-4/artifacts/`。完整 Windows ZIP 和源码 ZIP 均已完成 CRC 检查。聊天附件使用两份各小于 19,000,000 字节的普通 ZIP，按不重叠文件划分，不是需要拼接命令的分卷。

下载两份附件，新建一个空目录，分别使用 Windows“全部提取”并明确选择同一个目标目录。两次都选择该新建目录，不要第二次选择其内部的 `EarlyRush-V3-Windows` 子目录。完成后运行其中的 `EarlyRush-V3-Windows/EarlyRush.exe`。

合并解压后，全部 142 个原包文件逐一匹配 SHA，`FILES.sha256` 的 141 项全部通过。EXE、Data、UnityPlayer.dll、MonoBleedingEdge 和其他原始依赖均在包内。包内也提供操作说明、验收说明、两个原始 QA 报告和三张实际 Player 截图。

合并后的真实 EXE 已启动，实际查看菜单截图；24 项基础输入/菜单/教学/暂停与重开检查通过、0 失败，然后正常关闭，退出码 0。此项是解压启动 smoke，不是又一次完整四路线验收。启动后再次验证了 141 个清单文件，原 ZIP 哈希也未变。

v1 已记录的 51 项、v2 已记录的 54 项基线均通过保留检查。仓库不提交运行缓存、EXE/DLL、ZIP、凭据或 Library 身份信息；本清单仅记录本地成品与验证事实。附件保存与发送状态以实际 Library 返回和父端交付结果为准，不在清单中预先声称发送成功。

恢复审计把“阻挡玩家的地面角色”与恢复中仍参与身体分离的 NPC 分开；完整路线全状态 0.25 米邻域采样最大为 3，不能据此声称逐帧无视觉重叠。性能、静态截图与真人手感的范围限制见验收和视觉复核文档。
