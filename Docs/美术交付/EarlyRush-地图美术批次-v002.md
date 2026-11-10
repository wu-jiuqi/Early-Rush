# Early Rush 地图美术批次 v002

日期：2026-11-11。依据：同目录《EarlyRush-美术素材清单与接入规范-v1.md》《EarlyRush-美术生产清单-v1.csv》，以及用户确认的“沿用现有关卡，补齐地图美术”。

交付根目录：`EarlyRush_Art_v002/`。本批共 **34 项候选资源：33 项新生成、1 项沿用 v001 沥青；26 项 A 类、8 项 B 类**。完成当前地图使用的基础纹理、建筑设施和地面标记，未覆盖原 CSV 全部 63 个素材/动作包。生成工具为内置 image_gen。

## 资源分组

| 分组 | 数量 | 内容 | A／B |
| --- | ---: | --- | --- |
| common | 8 | 沥青、主瓷砖、辅助瓷砖、地毯、人行道、早餐摊、地面箭头、中性选择框 | 5／3 |
| street | 10 | 店面、地铁入口、长椅、灌木花箱、长花箱、自行车架、纸箱、水平路缘、横道、停车标记 | 7／3 |
| metro | 9 | 列车闭门/开门两态、盲道、出口设施、站台椅、售货亭、柱子、扶梯、扭蛋机外观 | 8／1 |
| office | 7 | 文件柜、空工位、会议桌、前台、沙发、植物、中性地垫 | 6／1 |

实际源图、选定修订版、输出路径、尺寸、锚点、世界宽度与原清单编号，以 `metadata/export-config.json` 为准。文件柜和空工位选用 v2；`office/desk-chair` 沿用历史资源键，但图中不含可推椅子。`office/clock-mat` 图中无钟或打卡符号。原稿及未选首稿保存在 `source/generated/{common,street,metro,office}/`，不计入34项输出数量。

## 接入边界

**本批未写入 Assets，未完成 Unity 实机验收。** A 类候选按既有完整资源键组织在 `runtime-static/V8Art/`，复核后可映射到 `Assets/Resources/V8Art/`；不能将候选包存在视为游戏内已生效。

B 类位于 `environment-source/`：

| 输出相对路径 | 需要的程序配合 |
| --- | --- |
| `floors-v3/sidewalk.png` | 新增人行道材质与地图分区映射 |
| `street/curb-horizontal.png` | 地面中心锚点映射 |
| `corrections/crosswalk-flat.png` | 地面中心锚点映射，并对齐真实过街区域 |
| `street/bike-parking-marker.png` | 地面中心锚点映射 |
| `metro/tactile-strip.png` | 地面中心锚点映射，并核对拼接间距 |
| `office/clock-mat.png` | 地面中心锚点映射 |
| `fx-decal/arrows.png` | 地面中心锚点映射，旋转仍由程序驱动 |
| `ui/selection.png` | 地面中心锚点映射；仍为多用途共用中性框 |

其中7项贴花虽已有旧资源键，本批采用 `(0.5,0.5)` 中心锚点；旧通用加载器是 `(0.5,0.1)`，因此本批明确归为 B 类，不直接覆盖后宣称正确接入。地面 Tile 原本就使用中心点，人行道的 B 类原因是新增材质映射。

## 真实布局与预览

本批沿用 seed `91026` 的三阶段真实快照，文件为 `metadata/layouts/layout-stage-{0,1,2}.json`，来源见 `metadata/layout-provenance.json`。不改路线、任务位置、碰撞与可走范围；美术不会定义新的入口或通道。

预览输出：

- `previews/map-stage-{0,1,2}-{overview,detail,exit}.png`：三阶段全览、局部与出口，共9张。
- `previews/assets-{common,street,metro,office}.png`：四组资源总览。
- `previews/floors-repeat.png`：地面重复检查。
- `previews/review.html`：本地审片入口。

这些是**离线美术拼装预览，不是 Unity 截图或玩法验证证据**。地面使用真实 `cells/zones`，结构物使用真实 `structures.position/width`；边界装饰按既有规则及候选完整画布重新推导，数量可能随新图宽高比变化。当前外围8格环境地面未绘制，使用奶油审片底；动态玩法对象未重现，列车固定展示开门态，主角仅作角落尺寸参照。路线与任务文字属于审片叠加，不烘焙进运行素材。中心锚点贴花展示的是拟接入效果。

## 校验与来源

`metadata/manifest.json`记录资源清单与哈希；`metadata/validation.json`记录最终技术检查结果：34项导出完成，技术告警0，输出与配置哈希复核一致。检查范围是尺寸、Alpha、包围盒、边缘与哈希，不替代美术或实机验收，也不证明严格无缝。静态预览已复核，详见`metadata/review-notes.md`。

完整提示词与来源保存在 `metadata/root-prompts.json`、`street-prompts.json`、`metro-prompts.json`、`office-prompts.json`。原始图为单层PNG，不能视为已交付PSD/KRA分层母版。技术导出仅等比缩放及锚点平移，不通过非均匀拉伸改变物体比例。

## 后续行动与未完成范围

先复核列车开闭两态轮廓与门位置、俯视角一致性、地面接缝、任务设施辨识和完整画布占比；随后接入26项A候选，补齐8项B映射，在三种窗口尺寸下验证遮挡、接地点及通行边界。旧文件若存在须保留 `.meta`，运行资源缓存需重新加载。

原规范中的 EN-07 信号灯、EN-11 独立可动门叶、EN-18 低墙/转角/前景层，以及独立会议门、闸机、路缘转角与任务专用共享键拆分等新接口扩展，仍未量产。当前交付不宣称完整地图玩法对象、全部环境事件、分层母版或全63项清单已经完成。
