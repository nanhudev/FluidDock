using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

public static class ExtendedIntent {
    static Match Match(string value,string pattern){return Regex.Match(value,pattern,RegexOptions.IgnoreCase);}
    static string Clean(string value){return value.Trim().Trim('“','”','\"','\'').Trim();}
    public static DockPlan Parse(string text,IEnumerable<Entry> catalog){
        string q=Regex.Replace(text.Trim(),@"[。！？!?.]+$","");for(int polite=0;polite<3;polite++)q=Regex.Replace(q,@"^(请|麻烦|帮我|帮忙|能不能)\s*","");q=Regex.Replace(q,@"工作空间|工作區|workspace", "工作区",RegexOptions.IgnoreCase);
        bool context=Regex.IsMatch(q,@"工作区|模式|开始开发|开始办公|开始工作|开始娱乐|开始听歌|回到默认|回到日常");
        bool extra=Regex.IsMatch(q,@"重命名|改名|^(隐藏|显示|恢复|藏起|找回)|所在文件夹|使用统计|使用情况|rename|hide|unhide|reveal",RegexOptions.IgnoreCase);
        if((context||extra)&&Regex.IsMatch(q,@"不要|暂不|别|不想|don't|do not",RegexOptions.IgnoreCase))return new DockPlan{Message="没有改动。"};
        if(Regex.IsMatch(q,@"^(查看|列出|显示|管理|看看)?(我的|全部|所有)?工作区(列表)?$"))return new DockPlan{Intent="workspace_list"};
        if(Regex.IsMatch(q,@"^(我要)?(开始|进入)(开发|办公|工作|娱乐|听歌)(了|吧)?$")){string name=Regex.Match(q,"开发|办公|工作|娱乐|听歌").Value;return new DockPlan{Intent="workspace_switch",WorkspaceName=name=="工作"?"办公":name=="听歌"?"音乐":name};}
        if(Regex.IsMatch(q,@"^(回到|恢复|切回)(默认|日常|原来)(布局|模式|工作区)?$"))return new DockPlan{Intent="workspace_switch",WorkspaceName="默认"};
        var english=Match(q,@"^(?:switch to|enter|activate|use)\s+(.+?)\s+(?:工作区|mode)$");if(english.Success)return Named("workspace_switch",english.Groups[1].Value);
        english=Match(q,@"^save\s+(?:current\s+(?:layout|dock)\s+)?as\s+(.+?)(?:\s+工作区)?$");if(english.Success)return Named("workspace_save",english.Groups[1].Value);
        var m=Match(q,@"^(?:将|把)?(?:当前(?:布局|配置|Dock|dock|工作区)?|现在的布局)?(?:保存|另存)(?:为|成|到)\s*(.+?)(?:工作区|模式)$");
        if(!m.Success)m=Match(q,@"^保存(?:当前(?:布局|配置|工作区))?(?:为|成)?\s*[“""']?(.+?)[”""']?\s*工作区$");
        if(m.Success)return Named("workspace_save",m.Groups[1].Value);
        m=Match(q,@"^(?:切换到|切换|切到|进入|启用|使用|打开)\s*(.+?)(?:工作区|模式)$");if(m.Success)return Named("workspace_switch",m.Groups[1].Value);
        m=Match(q,@"^(?:工作区|模式)(?:切换到|切到|切换为)\s*(.+)$");if(m.Success)return Named("workspace_switch",m.Groups[1].Value);
        m=Match(q,@"^(?:新建|创建)(?:一个)?\s*(.+?)(?:工作区|模式)$");if(m.Success)return Named("workspace_create",m.Groups[1].Value);
        m=Match(q,@"^(?:删除|移除)\s*(.+?)工作区$");if(m.Success)return Named("workspace_delete",m.Groups[1].Value);
        m=Match(q,@"^(?:将|把)?(.+?)工作区(?:重命名|改名)(?:为|成)\s*(.+?)(?:工作区)?$");if(m.Success){var p=Named("workspace_rename",m.Groups[1].Value);p.NewName=Clean(m.Groups[2].Value);return p;}
        m=Match(q,@"^(?:启动|打开)(.+?)工作区(?:的)?(?:全部|所有)?(?:软件|应用|入口)$");if(m.Success)return Named("workspace_open",m.Groups[1].Value);
        m=Match(q,@"^(?:将|把)?(.+?)(?:加入|添加到|放进|放到)(.+?)工作区$");if(m.Success){var p=Resolve("workspace_add",m.Groups[1].Value,catalog);p.WorkspaceName=Clean(m.Groups[2].Value);return p;}
        m=Match(q,@"^(?:把|将)?(.+?)(?:重命名|改名)(?:为|成)(.+)$");if(m.Success){var p=Resolve("rename",m.Groups[1].Value,catalog);p.NewName=Clean(m.Groups[2].Value);return p;}
        m=Match(q,@"^(?:隐藏|藏起)(.+?)(?:的)?(?:入口|图标)$");if(m.Success)return Resolve("hide",m.Groups[1].Value,catalog);
        m=Match(q,@"^(?:显示|恢复|找回)(.+?)(?:的)?(?:入口|图标)$");if(m.Success)return Resolve("show",m.Groups[1].Value,catalog);
        if(!Regex.IsMatch(q,@"自动|时钟|小组件|底栏|dock",RegexOptions.IgnoreCase)){
            m=Match(q,@"^(?:隐藏|藏起)\s*(.+)$");if(m.Success)return Resolve("hide",m.Groups[1].Value,catalog);
            m=Match(q,@"^(?:显示|找回|恢复)\s*(.+)$");if(m.Success)return Resolve("show",m.Groups[1].Value,catalog);
        }
        m=Match(q,@"^(?:hide|unhide|reveal)\s+(.+?)(?:\s+icon)?$");if(m.Success)return Resolve(q.StartsWith("hide",StringComparison.OrdinalIgnoreCase)?"hide":"show",m.Groups[1].Value,catalog);
        m=Match(q,@"^rename\s+(.+?)\s+to\s+(.+)$");if(m.Success){var p=Resolve("rename",m.Groups[1].Value,catalog);p.NewName=Clean(m.Groups[2].Value);return p;}
        m=Match(q,@"^(?:打开|查看|显示)(.+?)(?:的)?(?:所在文件夹|安装目录|文件位置)$");if(m.Success)return Resolve("locate",m.Groups[1].Value,catalog);
        m=Match(q,@"^(?:查看|看看|显示)(.+?)(?:的)?(?:使用统计|使用情况)$");if(m.Success)return Resolve("usage",m.Groups[1].Value,catalog);
        if(q=="使用统计"||q=="查看统计")return new DockPlan{Intent="usage"};
        if(context)return new DockPlan{Message="试试“切换到开发工作区”或“将当前布局保存为学习工作区”。"};
        return null;
    }
    static DockPlan Named(string intent,string value){string name=Clean(value);switch(name.ToLowerInvariant()){case "development":case "dev":name="开发";break;case "office":case "work":name="办公";break;case "entertainment":case "gaming":name="娱乐";break;case "music":name="音乐";break;case "default":name="默认";break;}if(name.Length<1||name.Length>32)return new DockPlan{Message="请输入 1–32 个字符的工作空间名称。"};return new DockPlan{Intent=intent,WorkspaceName=name};}
    static DockPlan Resolve(string intent,string query,IEnumerable<Entry> catalog){var match=SoftwareResolver.Resolve(query,catalog);if(match.Ambiguous||match.Matches.Count!=1)return new DockPlan{Message=match.Ambiguous?match.Message:"请指定一个明确的软件入口。"};var app=match.Matches[0];return new DockPlan{Intent=intent,AppName=app.Name,TargetKey=AppCatalog.Key(app),ResolvedEntry=app};}
}
