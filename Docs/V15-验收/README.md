# V15 UI 交付验证

日期：2026-11-11。基线提交：`847668c`。Unity：`6000.0.26f1`。Windows x64 Development 构建。

| 项目 | 实际结果 | 原始记录 |
| --- | --- | --- |
| 编译与构建 | Succeeded，0错误；1项既有未使用字段警告 | [构建摘要](build-summary.txt) |
| 素材源文件与导入 | 110 / 110通过 | [资源检查](ui-assets.json) |
| 既有机制 | 756项通过：动作43、冲刺80、地图321、咖啡312 | [专项检查](validation-focused.txt) |
| 运行界面 | 252项通过，0运行错误，PlayerPrefs已恢复 | [运行报告](report.json) |
| 实际渲染 | 21张有效截图，均首次捕获成功 | 运行报告的screenshots数组 |
| 小地图 | 45组对比，最大屏幕误差约0.000016像素 | 运行报告的maps数组 |

按钮检查使用真实 GraphicRaycaster 与合成指针坐标，随后调用实际 Button listener。动画检查包含六帧播放、暂停、一次完成、回池、同局进入结算取消游戏效果及菜单效果继续推进。三章HUD与结果含显式状态夹具，不能据此声称完成整关路线、真人节奏或性能验收；未发送操作系统鼠标／键盘事件。

以下是成品运行截图，不是设计参考图：

- [准备页](ready-1280x720.png)、[暂停](paused-1280x720.png)、[设置](settings-1280x720.png)
- [街道HUD](hud-stage-0-1280x720.png)、[地铁HUD](hud-stage-1-1280x720.png)、[办公室HUD](hud-stage-2-1280x720.png)
- [全图](map-full-1280x720.png)、[1920×1080](hud-stage-0-1920x1080.png)、[1024×768](hud-stage-2-1024x768.png)
- [帮助](help-1280x720.png)、[推轮修车](tutorial-repair.png)、[联动翻板](tutorial-flip.png)、[文件派送](tutorial-delivery.png)
- [成功结果夹具](result-success.png)、[超时结果夹具](result-timeout.png)

复现入口：`BuildEarlyRushV15.Build`，然后运行 `EarlyRush.exe --v15-ui-qa`。完整接入与动画说明见 [V15 UI接入说明](../V15-UI接入说明.md)。
