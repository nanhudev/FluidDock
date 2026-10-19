<div align="center">
<img src="assets/buddy.svg" width="90" alt="dockbot" />

# dockbot

免费开源的 Windows 桌面 Dock 与本地 AI 助手 · 圆角玻璃 · 约 17 KB 意图模型

Free, open-source Windows dock with a tiny offline intent assistant, workspaces and global search.

[下载 EXE](https://github.com/nanhudev/dockbot/releases/download/v0.2.0-preview.2/DockBot.exe) · [使用指令](docs/intents.md)
</div>

![dockbot](docs/media/overview.png)

## 安装

下载 [DockBot.exe](https://github.com/nanhudev/dockbot/releases/download/v0.2.0-preview.2/DockBot.exe)，双击运行。Windows 10 / 11，.NET Framework 4.8。

PowerShell 一行安装：

```powershell
irm https://raw.githubusercontent.com/nanhudev/dockbot/main/Install-Online.ps1 | iex
```

程序安装至 `%LOCALAPPDATA%\Programs\DockBot`，升级保留已有配置。

## 功能

| 功能 | 交互 |
| --- | --- |
| 玻璃 Dock | 悬停放大、弹簧反馈、Bot 搬运图标 |
| 应用与网站 | 拖入添加、拖动排序、拖出移除、独立分类 |
| 一键配置 | 流式扫描，选择常用软件后加入 |
| 本地助手 | 离线识别，预览后应用 |
| 工作空间 | 保存与切换开发、办公、娱乐、自定义布局 |
| 全局搜索 | Ctrl + 空格；应用、索引文件、指定目录 |
| 应用控制 | 打开、关闭、重启、聚焦、最小化、最大化 |
| 电源入口 | 锁屏、睡眠、休眠、关机、重启、注销 |
| 更新电源 | 转到 Windows 选择更新并关机／重启 |
| 电脑整理 | 桌面归档、临时文件、大文件，勾选并确认 |
| 个性化 | 分类、顺序、尺寸、主题、自启动、使用统计 |
| 配置保存 | 原子写入、历史备份、异常恢复、搜索缓存 |

使用记录可选授权，保存在本机。电源操作单独确认。

## 一句话操作

```text
将当前布局保存为学习工作区
我要开始开发了
回到默认布局
把应用 A 放到应用 B 前面
把应用 A 和应用 B 加入开发分组
重启应用 A
最小化应用 A
打开下载文件夹
打开声音设置
更新并关机
```

[完整指令列表](docs/intents.md) · [助手实现](docs/assistant-design.md)

## 界面

![一键配置](docs/media/setup.png)
![本地助手](docs/media/assistant.png)
![电脑整理](docs/media/cleanup.png)

## 构建

```powershell
npm run build
npm test
npm run package
```

WPF / Win32 · C# 5 · 约 17 KB 字符 n-gram 模型，19 个模型标签与扩展规则。

```text
src/       应用源码
tests/     回归与界面检查
models/    意图模型与训练数据
tools/     构建打包与模型训练工具
assets/    产品图标
docs/      使用指令、设计文档与截图
website/   官网页面与搜索元数据
```

## License

[MIT](LICENSE)
