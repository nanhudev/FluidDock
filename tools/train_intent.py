"""Train the tiny, offline character n-gram intent model. Python stdlib only."""
import collections
import json
import math
import pathlib
import re
import gzip

ROOT = pathlib.Path(__file__).resolve().parents[1]
PHRASES = {
    "open": ["打开微信", "开一下应用", "启动软件", "运行应用", "open app", "launch app", "帮我打开软件", "把程序打开", "启动游戏", "开一下浏览器", "我要打开应用", "应用启动一下"],
    "close": ["关闭微信", "关掉应用", "退出软件", "close app", "quit app", "把程序关上", "帮我关掉软件", "退出游戏", "结束应用窗口", "应用关一下", "关闭浏览器", "请求关闭程序"],
    "configure": ["一键配置dock", "自动配置底栏", "推荐常用软件", "生成dock配置", "配置我的dock", "常用软件推荐", "初始配置", "帮我配置启动栏", "推荐我的常用软件", "自动选择常用入口", "一键配置", "dock setup"],
    "care": ["清理c盘", "整理桌面", "清理大文件", "扫描临时文件", "电脑整理", "清理电脑", "磁盘空间不够", "管理电脑文件", "清理下载文件", "查看大文件", "电脑管理", "cleanup disk"],
    "add": ["把应用添加到dock", "固定应用", "加入应用", "把应用放进启动栏", "应用也要放上来", "把这个软件加进去", "pin app", "add app to dock", "我想把应用放在底栏", "应用留在这里", "增加一个应用入口", "把程序钉住", "将软件加入栏中", "应用放上去", "把应用收进来", "这程序要常驻", "加个应用", "把应用加上", "加入这个快捷方式", "放一个应用在栏里"],
    "remove": ["把应用从dock移除", "不要应用入口", "把应用拿掉", "删除应用快捷方式", "从栏里去掉应用", "应用不要放这了", "unpin app", "remove app from dock", "应用先撤下", "把这个图标扔掉", "移走应用图标", "隐藏应用入口", "这个程序从底栏撤掉", "应用别显示了", "清掉应用入口", "移除软件", "把应用丢出去", "不保留应用", "取消固定应用", "删掉应用"],
    "move": ["把应用挪到最前面", "应用移到最后", "应用往左移", "应用放到另一个应用右边", "应用排在另一个应用之前", "应用拖到前面", "把图标调换位置", "调整应用顺序", "应用放在另一个应用后边", "move app left", "move app to end", "应用跟另一个应用交换", "把这个入口放左边", "应用向右挪一位", "把应用搬过去", "把应用排到第一位"],
    "group": ["把应用归到办公", "应用放到游戏分类", "应用属于AI", "应用分到其他工具", "修改应用的分类", "归类应用", "应用算办公软件", "将应用归入游戏组", "把应用移入AI分组", "分类为工具", "group app as office", "应用换到办公类别", "把应用重新分组", "应用归到创作"],
    "sort": ["按使用频率排序", "智能整理一下dock", "常用应用放前面", "按使用时长排列", "最常用的排左边", "重新整理图标", "分类排序", "游戏分类放办公前面", "AI分组排在游戏前面", "先办公再游戏", "sort by usage", "把所有图标排整齐", "帮我整理底栏", "按常用程度自动排序", "按使用习惯整理", "重新排一下分类"],
    "theme": ["换成深色风格", "我想要浅色主题", "改成磨砂玻璃", "不要毛玻璃改石墨", "换个简约外观", "切换暗色", "改成亮色样式", "theme dark", "theme light", "石墨风格", "把颜色调浅", "更换皮肤", "把外观换成磨砂", "我喜欢黑色主题", "给底座换一个风格", "透明一些"],
    "size": ["图标大一点", "dock小一点", "大小调到百分之八十", "把底栏放大", "图标缩小", "尺寸调整为100%", "图标有点大缩一缩", "把它变大一些", "size 120%", "make dock smaller", "放大到110%", "调整dock大小", "底栏太小了", "我想要大号图标", "把尺寸调小", "变得紧凑一点"],
    "search": ["查找应用在哪里", "帮我找一下软件", "搜索应用", "定位应用的exe", "应用装在哪个目录", "读取这个快捷方式的目标", "找程序原文件", "这个快捷方式指向哪里", "应用可执行文件在哪里", "search app", "find app executable", "搜索已安装的软件", "打开应用的位置", "查看快捷方式指向", "寻找软件安装位置", "帮我看看应用路径"],
    "autohide": ["开启自动隐藏", "dock自动收起", "不要自动收起", "关闭自动隐藏", "鼠标离开后隐藏", "一直显示dock", "autohide on", "autohide off", "取消自动收起", "让dock自动藏起来", "离开底栏就收起来", "总是显示底栏"],
    "startup": ["开机自动启动", "取消开机自启", "登录后运行dock", "不要随系统启动", "打开开机启动", "关闭自启动", "start with windows", "disable startup", "开机就显示底栏", "让它跟系统一起启动", "不要自动运行", "开机不启动dock"],
    "widget": ["显示时钟小组件", "隐藏桌面时钟", "关闭小组件", "打开小组件", "不要时钟", "show widget", "hide clock", "显示桌面组件", "桌面那个时钟关掉", "把时钟打开", "小组件先不显示", "我要桌面时钟"],
    "tracking": ["开启使用统计", "关闭点击统计", "记录使用时长", "不要记录使用习惯", "暂停统计", "开启点击次数记录", "usage tracking on", "disable analytics", "别统计了", "开始记录应用活跃时间", "把本地统计关掉", "启用使用习惯学习"],
    "undo": ["撤销刚才的操作", "恢复上一步", "刚才的改动不要了", "撤销", "undo", "回到之前的状态", "还原刚才的设置", "把刚才删掉的恢复", "我点错了还原一下", "返回上一步", "恢复刚才的排序", "还原上一次改动"],
    "help": ["你能做什么", "教我怎么用", "有什么功能", "帮助", "how to use", "help", "你支持哪些操作", "给几个例子", "操作指南", "怎么添加软件", "你是什么助手", "能帮我做哪些事"],
    "unknown": ["今天天气怎么样", "帮我写一封邮件", "下载并安装游戏", "删除磁盘上所有文件", "卸载应用", "格式化硬盘", "执行powershell命令", "运行一段脚本", "发消息给同事", "购买一个软件", "帮我写代码", "登录我的账户", "清理系统注册表", "关机", "重启电脑", "你好", "讲一个笑话", "打开终端执行命令", "把文件上传服务器", "你是通用聊天模型吗"],
}

def features(text):
    s = ''.join(c for c in text.lower() if c.isalnum())[:240]
    return collections.Counter(s[i:i+n] for n in (1, 2, 3) for i in range(len(s)-n+1))

def train():
    classes = list(PHRASES)
    counts = {label: collections.Counter() for label in classes}
    rows = []
    for label, phrases in PHRASES.items():
        for text in phrases:
            counts[label].update(features(text))
            rows.append({"intent": label, "text": text})
    vocabulary = sorted(set().union(*(c.keys() for c in counts.values())))
    alpha = .35
    weights = {}
    for token in vocabulary:
        weights[token] = [round(math.log((counts[c][token]+alpha)/(sum(counts[c].values())+alpha*len(vocabulary))), 6) for c in classes]
    model = {"version": 1, "algorithm": "multinomial-naive-bayes-char-ngrams", "classes": classes,
             "prior": [round(math.log(len(PHRASES[c])/len(rows)), 6) for c in classes], "weights": weights,
             "temperature": 3.5, "trainingSamples": len(rows), "vocabularySize": len(vocabulary)}
    out = ROOT / "models" / "intent-model.json"
    out.write_text(json.dumps(model, ensure_ascii=False, separators=(',', ':')), encoding="utf-8")
    (ROOT / "models" / "intent-model.json.gz").write_bytes(gzip.compress(out.read_bytes(), mtime=0))
    (ROOT / "models" / "training.jsonl").write_text('\n'.join(json.dumps(r, ensure_ascii=False) for r in rows)+'\n', encoding="utf-8")
    print(json.dumps({"modelBytes": out.stat().st_size, "samples": len(rows), "features": len(vocabulary), "intents": len(classes)}))

if __name__ == '__main__':
    train()
