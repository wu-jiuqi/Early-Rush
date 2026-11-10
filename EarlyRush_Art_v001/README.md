# Early Rush 美术样张 v001

2026-11-11。按 `Docs/美术交付` 两份交付文档制作的首轮 **16 张候选 PNG**，用于视角、比例、锚点和辨识度评审。生成工具为内置 **image_gen**。

## 已交付

| 内容 | 数量 | 导出尺寸 | 位置 |
| --- | ---: | --- | --- |
| 主角四向静帧 | 4 | 256×384 | `runtime-static/V8Art/people/boy-*.png` |
| 普通 NPC 四向静帧 | 4 | 256×384 | `runtime-static/V8Art/people/man-*.png` |
| 车轮、办公椅、厚折叠抹布 | 3 | 256×256 | `runtime-static/V8Art/tasks/` |
| 沥青地面 | 1 | 512×512 | `runtime-static/V8Art/floors-v3/asphalt.png` |
| 领导头像 | 1 | 128×128 | `ui-source/portraits/leader-01.png` |
| 朝下冲撞前摇姿态 | 3 | 256×384 | `animation-source/crowd/rusher/windup/down/` |

先查看 `previews/contact-sheet.png`；比例图为 `previews/scale-calibration-1280x720.png`，透明边缘预览为 `previews/alpha-backgrounds.png`，地面平铺为 `previews/asphalt-2x2.png`。打开 `previews/review.html` 可切换背景和逐帧查看前摇。预览是审片资料，不是游戏截图；本轮静态复核结论见 `metadata/review-notes.md`。

## 接入状态

**本批尚未写入 Assets，尚未完成 Unity 实机验收。** 12 张 A 类图按已有完整资源键组织在 `runtime-static/V8Art/`，评审后可复制到 `Assets/Resources/V8Art/`。本次读取源码时该目标目录尚不存在。进入 Play 前需重载，运行中的资源缓存不会自动刷新。

头像与前摇属于 B 类候选，不放入 Resources。文件与对应工位必须复用同一个 `leader-01.png`；当前编号只是样张名称，未绑定最终 leaderId。前摇 JSON 是交付元数据，不能当成已实现的播放器；其 previewFps 只用于审片。项目另有并行 V11 开发，本批未验证其最新动作接口兼容性。

## 源图与导出

- `source/generated/` 保留 AI 原始单层 PNG。椅子和车轮选用 `chair-v2.png`、`wheel-v2.png`；首版保留用于追溯，不参与 runtime 导出。原始生成图不是可编辑分层 PSD/KRA 母版。
- `metadata/prompts-characters-ground.json`、`prompts-revisions.json`、`prompts-props.json` 保存完整提示词、参考图和生成路径。项目自身生成图用于角色一致性参考，无新增外部素材下载。
- `metadata/export-config.json` 记录源图、统一缩放和人工估定地面根点。所有人物及三帧前摇共用比例 0.32，仅平移根点到统一画布锚点，不逐帧自动裁边。侧面轮廓自然比正背面窄，不单独横向拉宽。
- `metadata/manifest.json` 记录导出路径、尺寸、锚点、世界宽度与 SHA256；`validation.json` 记录透明度、包围盒与边缘检查。
- 在项目根运行 `pwsh -File Tools/Export-ArtV001.ps1` 重做技术导出；运行 `pwsh -File Tools/Preview-ArtV001.ps1` 重建预览。脚本不负责美术重绘或抠图。

## 验收边界

16 张导出已完成尺寸、透明角、外边缘和哈希检查，0 项技术告警。源图透明区有低 Alpha 隐藏颜色，须正确 Alpha 混合；不把忽略 Alpha 后的彩色底当成实际显示效果。全部主体均留在画布内。

人工根点与高俯视视角仍是样张参数，不能替代灰盒测量。地面相对边 RGB 平均差约 2.66 / 3.20（0–255），这只是统计，并非数学无缝保证。还须检查实际切片采样和远近缩放。四向转动的头部轮廓、左右光照一致性、三帧前摇衔接、手持挂点、动态遮挡和高密度辨识仍需游戏中验证。

比例图中的桌子与门口来自项目旧图集，仅作尺度参照，不算新素材。独立地面阴影、可编辑分层母版、骑行、完整走路及其他任务美术不在本批16图中，按原清单后续生产。

## 下一步

将本批 A 类候选接入灰盒，在 1280×720、1920×1080、960×540 下验证比例与接地点；头像检查 24/32/48px；视角和比例定稿后再补齐第一批其余静态件，并开展四向走路和动作量产。
