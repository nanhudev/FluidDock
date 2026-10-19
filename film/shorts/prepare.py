"""Editorial data for seven distinct portrait films. Facts pinned to v0.1.1."""
import json
from pathlib import Path

HERE=Path(__file__).resolve().parent
def scene(duration,title,sub='',voice='',shot=None,start=0,kind='demo',tags=None):
    return dict(duration=duration,title=title,sub=sub,voice=voice,shot=shot,start=start,kind=kind,tags=tags or [])

FILMS=[
 dict(id='V1_GitHub_17KB',audience='技术发现、开源工具爱好者',title='17KB 的本地 AI，住进了 Windows Dock？',tags=['开源项目','Windows','桌面美化','本地AI'],scenes=[
  scene(1.2,'GitHub 又整出\n狠活了。',kind='hero'),
  scene(2.8,'17KB 的 AI？','离线意图模型','十七千字节的本地 AI？','empty',0),
  scene(3,'住进 Windows Dock','先看懂，再确认','把它放进 Windows 底座。','query',30),
  scene(6.5,'一句话，归进开发组','预览 → 确认 → 整理','一句话，预览、确认，再把应用归进开发分组。','group-carry',33),
  scene(3,'很小。也很实用。','规则优先 · 19 类基础意图','离线判断桌面意图。','group-result',39.5,tags=['离线运行','推理无需 GPU']),
  scene(1.5,'无需云端模型 API',kind='hero'),
  scene(3,'DockBot','免费开源','这就是 DockBot，免费开源。',kind='end')]),
 dict(id='V2_Technical_17KB',audience='程序员、AI 爱好者、开源社区',title='17KB 的模型怎么管理 Windows Dock？',tags=['小模型','AI工程','WPF','开源'],scenes=[
  scene(3,'AI 模型，\n一定要几个 GB 吗？',voice='AI 模型，一定要几个 G 吗？',kind='hero'),
  scene(3,'17KB。','17311 bytes · 压缩模型权重','这一个，约十七千字节。',kind='hero'),
  scene(4,'字符 n-gram','WPF 原生界面 · 规则优先','字符级意图模型，先匹配规则。',kind='code-intent'),
  scene(7,'意图 → 应用 → 计划','预览之后，由你确认','识别意图，找到应用，生成受限计划，由你确认。','group-carry',33),
  scene(4,'19 类基础意图','274 条合成训练短句','十九类意图，二百七十四条合成短句。','group-result',39.5),
  scene(3,'不是通用聊天大模型','只处理特定桌面任务','它不是通用聊天大模型。',kind='hero'),
  scene(4,'Small Model.\nUseful Actions.','无需云端模型 API','小模型，做具体的事。',kind='end')]),
 dict(id='V3_Visual_Dock',audience='桌面美化、动效设计、视觉审美用户',title='Windows Dock，也可以这么丝滑',tags=['桌面美化','交互动效','极简设计','Windows'],scenes=[
  scene(2,'Windows Dock','也可以这么丝滑。',shot='empty',start=0),
  scene(3,'Fluid Motion','',shot='setup',start=9),
  scene(3,'Native Windows','',shot='add-carry',start=15),
  scene(4,'Your Desktop.','',shot='overview',start=42),
  scene(2,'Your Rules.','',shot='group-result',start=40),
  scene(2,'DockBot','',kind='end')]),
 dict(id='V4_AI_Interaction',audience='普通 Windows 用户、效率软件用户',title='用一句话，把 Windows Dock 整理好',tags=['效率工具','桌面整理','Windows技巧','AI助手'],scenes=[
  scene(2,'还在逐个\n整理图标？',voice='还在整理图标？',kind='hero'),
  scene(3,'现在，直接告诉它','把 Blender 和 UE 放进开发分组','现在，直接告诉它。','query',30),
  scene(6.5,'先预览，再确认','把想法变成明确的操作','先预览，再确认。把应用放进开发分组。','group-carry',33),
  scene(2.5,'分组完成','入口各就各位',shot='group-result',start=39.5),
  scene(4,'微信，放到 QQ 前面','调整相对顺序','微信，放到 QQ 前面。','order',25),
  scene(4,'还可以继续添加','添加入口，不安装软件','还可以继续添加常用入口。','add-carry',18),
  scene(2,'只需要一句话','你的桌面，自己的规则','你只需要说一句。',kind='end')]),
 dict(id='V5_Desktop_Efficiency',audience='生产力工具、桌面整理、学生用户',title='从空 Dock 到自己的 Windows 工作台',tags=['生产力工具','桌面整理','学生党','开源软件'],scenes=[
  scene(2,'桌面太乱？','试试从零开始。','桌面乱？',kind='hero'),
  scene(2,'从三个系统入口开始',shot='empty',start=1),
  scene(5,'一键配置，自己选择','扫描结果逐条出现','一键配置，选出自己的常用软件。','setup',5),
  scene(2,'你的第一套工作台',shot='overview',start=11),
  scene(4,'想到什么，随时添加','不用一次配置完美','不用一次配置完美，想到什么，随时添加。','add-carry',18),
  scene(3,'顺序也能微调','微信，放到 QQ 前面','顺序也能微调。','order',25),
  scene(5,'再给创作留一个分组','应用与分类，自己做主','再给创作，留一个自己的分组。','group-carry',37),
  scene(3,'Less Clutter.\nMore Focus.','',voice='少一点杂乱，多一点专注。',kind='end')]),
 dict(id='V6_Indie_Developer',audience='独立开发者、程序员、学生开发者',title='我做了一个 Windows Dock，但不想只复刻 macOS',tags=['独立开发','学生开发者','开源项目','AI编程'],scenes=[
  scene(3,'我做了一个\nWindows Dock','', '我做了一个 Windows Dock。','overview',43),
  scene(2.5,'不只复刻 macOS','原生界面 · 可停下的动效驱动','还想多做一点。',kind='code-motion'),
  scene(4,'于是，放进了一个\n约 17KB 的模型','本地意图模型','放进约十七千字节的本地意图模型。',kind='hero'),
  scene(6.5,'告诉它，要添加什么','软件入口由本机目录解析','告诉它，要添加什么软件。','add-carry',15),
  scene(6.5,'怎么排序，如何分组','每一步都先预览，再执行','如何排序，怎么分组。每一步都先预览，再执行。','group-carry',33),
  scene(3.5,'不是万能助手','先把具体的小事做好','先把具体的小事做好。',kind='code-assistant'),
  scene(3,'Built with curiosity.','现在已经开源','现在，它已经开源了。',kind='end')]),
 dict(id='V7_10Second_Hook',audience='短内容快速发现、工具推荐用户',title='17KB 本地 AI，塞进 Windows Dock',tags=['本地AI','Windows','开源','桌面美化'],scenes=[
  scene(2,'17KB 的本地 AI？','离线意图模型','十七千字节？',kind='hero'),
  scene(2,'塞进 Windows Dock！',shot='empty',start=1,voice='塞进 Windows 底座。'),
  scene(2,'一句话，先看懂',shot='query',start=30,voice='一句话整理。'),
  scene(3,'19 类基础意图','离线运行 · 推理无需 GPU',shot='group-result',start=39.5),
  scene(2,'DockBot','免费开源','开源 DockBot。',kind='end')]),
]

SHOTS={
 'empty':dict(crop=[1220,1380,800,420],box=[70,760,940,494]),
 'query':dict(crop=[800,140,1600,960],box=[70,690,940,564]),
 'group-carry':dict(crop=[1100,450,1100,1350],box=[100,470,880,1080]),
 'group-result':dict(crop=[1600,1420,700,380],box=[70,780,940,510]),
 'order':dict(crop=[1140,1420,650,380],box=[70,780,940,550]),
 'add-carry':dict(crop=[850,500,1000,1300],box=[120,450,840,1092]),
 'overview':dict(crop=[500,1350,1400,450],box=[45,800,990,318],pan=800),
 'setup':dict(crop=[700,20,1800,1780],box=[95,490,890,880]),
}

def prepare():
    for film in FILMS:
        at=0
        for i,s in enumerate(film['scenes']):s.update(at=at,index=i);at=round(at+s['duration'],3)
        film['duration']=at;film['baseline']='v0.1.1-preview.1';film['voice']='Microsoft Huihui Desktop';film['disclosure']='独立演示配置，非真实用户操作录屏'
    out=HERE/'manifest.json';out.write_text(json.dumps(dict(films=FILMS,shots=SHOTS,fps=30,width=1080,height=1920),ensure_ascii=False,indent=2),encoding='utf-8')
    return out
if __name__=='__main__':print(prepare())
