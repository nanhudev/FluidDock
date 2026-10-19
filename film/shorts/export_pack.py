"""Create publishing guide and tidy final sidecars from verified exports."""
import json
from pathlib import Path
from render import MANIFEST,EXPORT,stamp,index
rows=[]
for film in MANIFEST['films']:
    folder=EXPORT/film['id'];movie=folder/(film['id']+'.mp4')
    if not movie.exists():raise RuntimeError('Missing '+str(movie))
    meta=json.loads((folder/'发布信息.json').read_text(encoding='utf-8'))
    meta['voice']='Windows SAPI · Microsoft Huihui Desktop' if any(s['voice'] for s in film['scenes']) else '无旁白'
    meta['music']='ClearSpace · 原创配乐'
    (folder/'发布信息.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
    if not any(s['voice'] for s in film['scenes']):
        captions=[]
        for i,s in enumerate(film['scenes']):captions.append(f"{i+1}\n{stamp(s['at'])} --> {stamp(s['at']+s['duration'])}\n"+(s['title'].replace('\n',' ') if s['kind']!='end' else 'DockBot · Open Source · Windows')+'\n')
        (folder/(film['id']+'.srt')).write_text('\n'.join(captions),encoding='utf-8-sig')
    rows.append(f"| [{film['id']}]({film['id']}/{film['id']}.mp4) | {film['duration']:g} 秒 | {film['title']} | {film['audience']} |")
guide='''# DockBot 短片发布包

1080 × 1920，30 fps，H.264 / AAC。打开 index.html 可预览全部版本。

首发推荐 V1；技术社区用 V2；视觉展示用 V3；快速预热用 V7。

| 版本 | 时长 | 推荐标题 | 受众 |
| --- | --- | --- | --- |
'''+ '\n'.join(rows)+'''

每个目录包含 MP4、cover.png、SRT、旁白稿、发布信息和验证结果。V3 为纯音乐版，SRT 记录画面标题。

## 简介

DockBot，把常用软件、网站和工作空间收进一个 Windows Dock。内置约 17 KB 的本地意图模型，用一句话添加、排序、分组和管理应用。

源码与下载：https://github.com/nanhudev/dockbot

## 标签

#Windows #桌面美化 #开源软件 #本地AI #效率工具

画面取自 0.1.1 演示配置。17 KB 是模型文件大小；19 类为模型标签，当前扩展指令见项目文档。

配乐：ClearSpace 原创器乐。旁白：本机 Windows SAPI，Microsoft Huihui Desktop。
'''
(EXPORT/'PUBLISHING_GUIDE.md').write_text(guide,encoding='utf-8')
index()
print('Publishing pack ready: '+str(EXPORT))
