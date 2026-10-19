# DockBot 宣传片与原生演示

40 秒，1920 × 1080，30 fps。主体为原生 WPF 控件录制，配基础推拉运镜；没有大段说明、编号卡片或装饰图层。

故事：空 Dock → 一键配置 → 添加应用 → 调整相对顺序 → 建立开发分组 → 查看底座 → 扫描和勾选清理项。

录制使用隔离的数据目录。软件名称和图标是经用户选择的演示素材，入口可能是演示用配置；不实际启动软件、不读取 UserAssist、不清理个人文件。录制器对添加、排序和分组结果做检查，失败则停止输出成功记录。渲染源来自 WPF 视觉树，桌面、其他进程及 DWM 背景不录入。

## 原生录制

构建后运行：

```powershell
.\bin\DockBot.exe --data-dir <独立演示目录> --record-demo
```

演示目录须预先提供 `demo-catalog.json`，包含选择的应用入口与图标。该文件是个人配置，不提交仓库。录制器输出 `native-demo.mp4`、关键截图和检查结果。需要 FFmpeg 位于 PATH 中。帧通过管道直接编码，不在磁盘积累图片序列。

## 剪辑与配乐

```powershell
npm ci
python tools/score.py
ffmpeg -i public/clear-space.wav -af loudnorm=I=-16:TP=-1.2:LRA=9 -ar 48000 public/score-master.wav
node node_modules/@remotion/cli/remotion-cli.js render src/index.tsx DockBotPromo ../artifacts/DockBot-Promo.mp4 --codec h264 --crf 17 --concurrency 2
```

把原生录制放在 `public/native-demo.mp4`。渲染使用 Windows 本机的微软雅黑字体：将系统已有 `msyh.ttc`、`msyhbd.ttc` 分别复制为 `public/fonts/ui.ttc`、`public/fonts/ui-bold.ttc`。字体不随仓库或发布包分发。

`tools/score.py` 合成原创电子器乐 **Clear Space**：120 BPM、40 秒、立体声，包含和弦、拨弦音色、贝斯与鼓组，没有外部采样或下载曲目。源代码按项目 MIT 许可提供。主要镜头与交互变化落在半秒节拍上。

最终交付前检查开头、配置、添加、排序、分组、扫描和结尾关键帧，并检查音视频时长、解码、响度与非静音。原生演示使用本机固定的示例数据，不代表软件发现率或所有用户表达的识别准确率。
