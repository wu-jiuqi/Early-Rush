# Early Rush v2 交付清单

本清单是两个 ZIP 之外的发布记录，不写回已生成的包，避免 ZIP 自身哈希循环变化。游戏源码与最终 build5 保持一致，没有为封包重新修改或构建游戏。

## 发行文件

工作区根目录：`D:\CodexWorkspace\2026-10-31\task-4`

| 文件 | 字节数 | SHA-256 |
|---|---:|---|
| `artifacts/EarlyRush-V2-Windows-x64.zip` | 30,851,387 | `3096F251A8EE62627087F1A351E6C7E70A8D9E39C52A6D35696E8B232027FF84` |
| `artifacts/EarlyRush-V2-Unity-Source.zip` | 90,683 | `5293488B0A7EE3CE93B1CA526768DB4CF835E52A78D088B119EB29B015D85EC6` |

Windows ZIP 包含完整 EXE、Data、DLL、运行依赖及说明。运行时须完整解压，不能只复制 EXE。源码 ZIP 保留 Unity Assets、Packages 与 ProjectSettings 和交付文档，不包含 Unity 缓存或游戏构建二进制。

## 完整性核验

- `package_v2.py` 实际执行成功，退出码 0。
- 两个 ZIP 的完整性／CRC 检查通过。
- Windows ZIP 解压至 `artifacts/unpacked-v2-20261101/`；归档含 137 个条目，`FILES.sha256` 中列出的 136 个文件逐项校验通过。
- 解压后 `Assembly-CSharp.dll` 的 SHA-256 为 `D05E037A152B0A0EF97FA97B7E2283DBCE05130A124A4825BAE9379F51F1DD85`，与通过 run4 验收的 build5 一致。
- 原始 Player 的 `EarlyRush.exe`：672,256 字节，SHA-256 `0CA33352BA3F994CE52EFF1BE97A4C6CAF023B64B917168A05EC09DBD3B28B41`；该 EXE 是 Unity 启动壳，游戏逻辑版本由上述程序集及完整文件清单一并确认。
- 51 个源码、配置与 Unity meta 文件的字节级 Git blob SHA 与发布前仓库 `160a49d54c9950f1a21ebf0bdfa9f327c84c3780` 的源码一致；封包没有修改源码。
- v1 原始工程与两个旧 ZIP 的 51 项基线校验均未变化。

## 功能验收与解压启动

最终 build5 在实际 Windows Player 中完成 run4：**240 项通过、0 项失败**。报告见 [v2-validation.md](v2-validation.md) 和 [规范化 JSON 副本](qa-final.json)。本地 Unity 原始 JSON 的 SHA-256 为 `19BE9BB891FF19F1C835B21ED041759658DC40E3253F55E66F894F4D7A684308`；规范化副本的 SHA-256 为 `0FC2BA69BC977D5FF3001739A3528E3206794EA9C3709F8799EED6ECF8DF25F2`，两种字节格式不能混用。

解压后的 EXE 已实际启动，菜单截图已查看，并由 Player 内输入驱动完成 risk 路线全部三关：**95.00186 秒、138 次碰撞、准时打卡**；该次局部检查为 **75 PASS / 0 FAIL**。随后正常关闭窗口，进程退出码 0。这是解压包的启动与单路线输入冒烟，**没有第二次执行完整四策略 run4**。

Player 验收属于游戏内输入驱动自动化，不是操作系统键盘自动化，也不是真人试玩。解压启动检查是发行完整性检查，不冒称重新执行了整套 run4。多设备兼容性、真人手感和长期稳定性仍未充分验证。

## 包外核验记录

以下文本记录在 ZIP 生成后单独归档，不重新纳入现有包：

- [构建与源码清单](release/build-manifest.json)：发行文件、最终程序集和 51 个源码文件的校验。
- [SHA-256 清单](release/SHA256.txt)：两个 ZIP、EXE、程序集和原始 QA 报告。
- [解压核验记录](release/extraction-verification.json)：CRC、137 个条目及 136 个文件逐项验证。
- [解压包启动与输入冒烟记录](release/package-smoke.json)：75 项通过、三关完成、菜单截图和正常关闭。

## 使用说明与发布范围

运行与操作见 [操作说明-v2.md](操作说明-v2.md)。本清单、外部 `artifacts/EarlyRush-V2-SHA256.txt` 与交付记录用于核对文件；ZIP 自身哈希不写进包内。历史试跑失败摘要和原始报告路径继续保留在验收报告中。

仓库只保存源码和文本证据，ZIP 没有提交至 Git。分享或上传结果另以实际工具回执为准。
