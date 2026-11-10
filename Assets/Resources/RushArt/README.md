# Early Rush V7 raster art

五张素材图均由 OpenAI 内置 `image_gen` 于 2026-11-10 生成。以用户提供的奶油白/薄荷青/金黄色俯视车站图为风格参考。没有 SVG 或手绘程序图形替代这些素材。

## 切片

- `props.png`、`people.png`、`icons.png`、`extras.png`：1254 × 1254，4 × 4 等分；每格 313.5 × 313.5。不要按请求中的 1536 尺寸硬编码。
- `floors.png`：2172 × 724，3 × 1 等分；每格 724 × 724，无透明边。
- `manifest.json` 列出从左到右、从上到下的名称、索引、Unity 左下原点 Rect、SHA-256。
- sprites 使用整个格子保留角色一致尺度；地面使用 `floors` 而非 `extras` 的小型地板展示块。
- 透明图需要正确 alpha blending；不可当 RGB 读取，也不能把低 alpha 像素当实色。原始 RGBA 的透明边缘含少量 alpha=1 的隐藏颜色，在正确合成中不构成可见红绿噪点。
- 建议颜色空间 sRGB、保原尺寸、无 mipmap、Clamp，主图不压缩。实际平台应根据性能验收决定后续压缩，不视本建议为通用最佳配置。

## 生成提示词规范

通用风格：`polished cute rounded toy 2.5D; mint teal and ivory, sunny yellow accents, soft compact shadows; overhead orthographic, no diagonal isometric; game-ready raster sprites; no text or captions; exactly 4 columns x 4 rows, objects centered and contained, actual alpha transparency`。

各图完整内容约束：

- props：树盆、三株灌木花坛、木条长椅、路灯 / 交通锥、施工护栏、车站柱、蓝色安全栏 / 售票机、玻璃翼闸机、服务亭、地铁车门模块 / 电脑桌、沙发、打印机、打卡机。俯视 60–65 度，前面朝画面下方。
- people：橙衣主角步行背面、正面、骑车背面、正面 / 蓝紫绿黄四色步行背面 / 蓝紫绿黄四色骑车背面 / 主角步行左、步行右、骑车左、骑车右。背包、深发、奶油色鞋；整个人物和车轮必须完整入格。
- icons：自行车、地铁、时钟、播放 / 暂停、重试、主页、声音 / 震动、退出、四向移动、闪电 / 盾牌、雨云、楼梯、扶梯。粗轮廓奶油色边缘，功能图标而非文字按钮。
- extras：咖啡、包子早餐、遗失包、咖啡车 / 早餐车、楼梯、扶梯、纸箱 / 地砖展示块、马路展示块、地毯展示块、办公室门 / 水洼、方向箭头、对勾、星星。首稿透明失败，使用第二次 image_gen 去除白色背景与灰色格线；保留物体内部奶油色和纹理展示块。
- floors：3:1 横幅，三个完全不透明正方形等分，左为奶油色细缝地砖，中为无道路标线的蓝灰沥青，右为薄荷绿织物地毯；全部正上方平面、均匀光照、无边框、无留白、无阴影、无透视。

extras 修正提示词：`Remove ALL the white backdrop and gray grid lines and replace them with ACTUAL ALPHA TRANSPARENCY. Keep all sixteen objects shapes, colors, sizes, positions unchanged. Preserve opaque cream materials INSIDE objects and all three square texture swatches. Only remove background OUTSIDE each object. No checkerboard painted into image.`

## 已做检查与边界

- 检查 PNG 尺寸、RGBA/RGB 模式、alpha 分布，生成清单及校验值。
- 将四张透明图分别以正常 alpha 合成方式在奶油色底上预览，逐格人工查看：角色完整、顺序对应、无白色网格、没有可见红绿噪点、没有明显跨格主体。
- QA 合成预览只用于检查，未覆盖素材，也没有程序重绘或抠图。所有美术修改均通过 image_gen 完成。
- 地面图不是数学意义上无缝贴图测试的保证。需要在 Unity 平铺后继续确认边界、比例、遮挡、屏幕上的人物可读性。
- 文件提供 64 个主要角色/道具/图标格和 3 张地面材质；不能据此宣称运行工程的所有视觉已完成替换，最终覆盖范围由接入及实机验收确定。
