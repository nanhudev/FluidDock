# 竖版卖点短片

1080 × 1920 · 30 fps · H.264 / AAC · 中文字幕与本地 SAPI 旁白。

| 版本 | 时长 | 内容 |
| --- | --- | --- |
| V1_GitHub_17KB | 21 秒 | 17 KB 卖点 |
| V2_Technical_17KB | 28 秒 | 小模型、规则与确认流程 |
| V3_Visual_Dock | 16 秒 | 纯音乐视觉版 |
| V4_AI_Interaction | 24 秒 | 添加、排序、分组 |
| V5_Desktop_Efficiency | 26 秒 | 空 Dock 到工作台 |
| V6_Indie_Developer | 29 秒 | 独立开发者视角 |
| V7_10Second_Hook | 11 秒 | 快速开场 |

在 `film` 目录运行，先按上级 README 准备字体、配乐和 `public/native-demo.mp4`：

```powershell
python shorts/prepare.py
python shorts/render.py --ids V1_GitHub_17KB V2_Technical_17KB V3_Visual_Dock
python shorts/verify.py --ids V1_GitHub_17KB V2_Technical_17KB V3_Visual_Dock
python shorts/render.py --ids V4_AI_Interaction V5_Desktop_Efficiency V6_Indie_Developer V7_10Second_Hook
python shorts/verify.py
```

输出在 `shorts/exports/`：MP4、封面、SRT、旁白、发布信息、验证结果及 `index.html`。`DOCKBOT_MEDIA_WORK` 可指定缓存目录。仅调整旁白后可用 `--compose-only` 重新合成。

原生素材取自 0.1.1 演示配置；17 KB 指分类模型文件，19 类指模型标签。当前软件的扩展指令见 [完整指令列表](../../docs/intents.md)。
