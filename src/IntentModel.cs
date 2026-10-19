using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

public sealed class IntentPrediction {
    public string Intent; public double Confidence; public double Margin;
}
public sealed class IntentModel {
    sealed class Model { public string[] classes{get;set;} public double[] prior{get;set;} public Dictionary<string,double[]> weights{get;set;} public double temperature{get;set;} }
    Model model;
    public bool Available {get{return model!=null;}}
    public IntentModel(){
        using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("FluidDock.IntentModel.gz")){
            if(resource==null)return;
            using(var gzip=new GZipStream(resource,CompressionMode.Decompress))using(var reader=new StreamReader(gzip))
                model=new JavaScriptSerializer{MaxJsonLength=2000000}.Deserialize<Model>(reader.ReadToEnd());
        }
    }
    public static string Normalize(string text){return Regex.Replace((text??"").ToLowerInvariant(),@"[^\p{L}\p{N}]","");}
    public IntentPrediction Predict(string text){
        if(model==null)return new IntentPrediction{Intent="unknown"};
        string s=Normalize(text);if(s.Length>240)s=s.Substring(0,240);
        var scores=(double[])model.prior.Clone();var tokens=new Dictionary<string,int>();
        for(int n=1;n<=3;n++)for(int i=0;i+n<=s.Length;i++){string token=s.Substring(i,n);int count;tokens.TryGetValue(token,out count);tokens[token]=Math.Min(3,count+1);}
        int matched=0;
        foreach(var pair in tokens){double[] weights;if(!model.weights.TryGetValue(pair.Key,out weights))continue;matched++;for(int c=0;c<scores.Length;c++)scores[c]+=weights[c]*pair.Value;}
        if(matched<2)return new IntentPrediction{Intent="unknown"};
        double max=scores.Max(),sum=0;for(int c=0;c<scores.Length;c++){scores[c]=Math.Exp((scores[c]-max)/model.temperature);sum+=scores[c];}
        var order=Enumerable.Range(0,scores.Length).OrderByDescending(i=>scores[i]).ToArray();
        return new IntentPrediction{Intent=model.classes[order[0]],Confidence=scores[order[0]]/sum,Margin=(scores[order[0]]-scores[order[1]])/sum};
    }
}
public sealed class DockPlan {
    public string WorkspaceName="",NewName="",SystemAction="";
    public string Intent="unknown",AppName="",OtherName="",TargetKey="",Group="",OtherGroup="",Theme="",Direction="",Message="",Source="rules";
    public bool Enable=true;public double Scale,Delta,Confidence=1;
    public Entry ResolvedEntry;
    public List<DockPlan> Steps;
    public bool Actionable {get{return new[]{"add","remove","move","group","sort","theme","size","autohide","startup","widget","tracking","undo","open","close","configure","care","batch","power","app_restart","app_focus","app_minimize","app_maximize","system_settings","system_folder","workspace_switch","workspace_save","workspace_create","workspace_delete","workspace_rename","workspace_list","workspace_open","workspace_add","rename","hide","show","locate","usage"}.Contains(Intent);}}
}
public sealed class DockIntent {
    readonly IntentModel model=new IntentModel();
    public bool ModelAvailable{get{return model.Available;}}
    static bool Has(string s,string pattern){return Regex.IsMatch(s,pattern,RegexOptions.IgnoreCase);}
    public DockPlan Parse(string text,IEnumerable<Entry> catalog){
        string q=(text??"").Trim();var p=new DockPlan();
        if(q.Length==0||q.Length>240){p.Message="请用一句话描述一个 Dock 操作。";return p;}
        var systemPlan=SystemIntents.Parse(q,catalog);if(systemPlan!=null)return systemPlan;
        if(Has(q,@"工作区|工作空间|workspace|模式")&&Has(q,@"清理|格式化|卸载|执行|powershell|上传|关机|重启")){p.Message="请把工作空间切换和电脑维护分开操作。";return p;}
        if(Has(q,@"不要|别|暂不|不想")&&Has(q,@"清理|整理桌面|关闭|打开|一键配置")){p.Message="没有改动。";return p;}
        if(Has(q,@"清理|整理桌面|桌面整理|大文件|磁盘空间|电脑管理")){p.Intent="care";return p;}
        if(Has(q,@"一键配置|自动配置|配置dock|推荐软件|常用软件推荐")){p.Intent="configure";return p;}
        if(Has(q,@"卸载|格式化|执行|清空回收站|删除.{0,5}(文件|磁盘|目录)|下载安装|帮我安装|上传|powershell|cmd\.exe")){p.Message="我只管理 Dock 入口，不卸载软件、删除文件或执行命令。";return p;}
        var extended=ExtendedIntent.Parse(q,catalog);if(extended!=null)return extended;
        if(Has(q,@"^(怎么|如何)|你能|帮助|help|能做什么|功能|例子")){p.Intent="help";p.Message="试试：添加微信、移除 QQ、把 GPT 放到最前面、图标小一点、换成石墨风格。";return p;}
        if(Has(q,@"不要.{0,3}(删|移除|添加|固定|缩小|放大|移动|排序|更换|调整)|别.{0,3}(删|移除|添加|固定|缩小|放大|移动|排序|更换|调整)|不要关闭|别关闭|不要取消")){p.Message="没有改动。需要整理时直接告诉我。";return p;}
        p.Enable=!Has(q,@"关闭|不要|取消|停用|禁用|暂停|不显示|不启动|不记录|别统计|别记录|disable|\boff\b");
        if(Has(q,@"撤销|还原|恢复上一步|undo"))p.Intent="undo";
        else if(Has(q,@"自动隐藏|自动收起|autohide|一直显示|总是显示"))p.Intent="autohide";
        else if(Has(q,@"开机|自启|登录后|startup|with windows"))p.Intent="startup";
                else if(Has(q,@"关掉|关闭|退出|\bclose\b|\bquit\b")&&!Has(q,@"统计|记录|小组件|时钟|自动隐藏|自动收起|tracking|widget"))p.Intent="close";
        else if(Has(q,@"统计|记录.{0,8}(使用|时长|点击)|使用习惯学习|analytics|tracking"))p.Intent="tracking";
        else if(Has(q,@"小组件|时钟|widget|clock"))p.Intent="widget";
        else if(Has(q,@"风格|主题|皮肤|毛玻璃|磨砂|石墨|浅色|深色|暗色|亮色|外观|theme"))p.Intent="theme";
        else if(Has(q,@"大小|尺寸|大一点|小一点|放大|缩小|变大|变小|太大|太小|大号|紧凑|size|smaller|bigger|\d+\s*%"))p.Intent="size";
        else if(Has(q,@"智能整理|使用频率|常用.{0,6}(前|左)|使用时长.{0,5}(排|序)|分类.{0,4}(排序|先|前)|分组.{0,8}(前|后)|先.+再|sort|排整齐"))p.Intent="sort";
        else if(Has(q,@"打开|开一下|启动|运行|\bopen\b|\blaunch\b")&&!Has(q,@"位置|路径|指向|哪里|在哪"))p.Intent="open";
        else if(Has(q,@"移除|取消固定|删掉|删除|拿掉|去掉|撤下|扔掉|丢出去|别显示|不保留|unpin|remove"))p.Intent="remove";
        else if(Has(q,@"添加|固定|加入|加一个|加个|加上|加一下|再加|帮我加|给我加|^加|钉住|放进|放上来|收进|常驻|\bpin\b|\badd\b"))p.Intent="add";
        else if(Has(q,@"分类|归到|归入|分到|属于|分组|算办公|group"))p.Intent="group";
        else if(Has(q,@"最前|最后|往左|往右|向左|向右|左边|右边|之前|之后|前面|后面|前边|后边|挪|搬|交换|顺序|\bmove\b"))p.Intent="move";
        else if(Has(q,@"搜索|查找|定位|路径|哪里|在哪|指向|快捷方式|\bexe\b|\bfind\b|\bsearch\b"))p.Intent="search";
        else{var prediction=model.Predict(q);p.Intent=prediction.Confidence>=.62&&prediction.Margin>=.45?prediction.Intent:"unknown";p.Confidence=prediction.Confidence;p.Source="ngram-model";}
        if(p.Intent.StartsWith("workspace_")){p.Intent="unknown";p.Message="请指定工作空间名称，例如“切换到开发工作区”。";return p;}
        if(p.Intent=="rename"){p.Intent="unknown";p.Message="请同时给出入口和新名称。";return p;}
        if(p.Intent=="widget"&&Has(q,"隐藏|藏起来"))p.Enable=false;
        if(p.Intent=="autohide"&&Has(q,"一直显示|总是显示"))p.Enable=false;
        if(p.Intent=="add"){
            var website=Websites.Resolve(q);if(website!=null){p.AppName=website.Name;p.TargetKey=AppCatalog.Key(website);p.ResolvedEntry=website;p.Group="常用网站";return p;}
            if(Has(q,"moodle")){p.Intent="unknown";p.Message="哪所学校的 Moodle？也可以粘贴网址。";return p;}
        }
        var resolved=SoftwareResolver.Resolve(q,catalog);string normalized=IntentModel.Normalize(q);
        if(resolved.Ambiguous){p.Intent="unknown";p.Message=resolved.Message;return p;}
        if(resolved.Matches.Count>0){p.AppName=resolved.Matches[0].Name;p.TargetKey=AppCatalog.Key(resolved.Matches[0]);p.Confidence=Math.Min(p.Confidence,resolved.Confidence);}
        if(resolved.Matches.Count>1)p.OtherName=resolved.Matches[1].Name;
        if(resolved.Matches.Count>1&&!new[]{"move","add","group"}.Contains(p.Intent)){p.Intent="unknown";p.Message="请逐个操作。";return p;}
        foreach(var app in resolved.Matches)foreach(string alias in SoftwareResolver.Aliases(app))normalized=normalized.Replace(IntentModel.Normalize(alias),"");
        var groups=new Dictionary<string,string>{{"ai","AI"},{"人工智能","AI"},{"办公","办公"},{"开发","开发"},{"音乐","音乐"},{"网站","常用网站"},{"游戏","游戏"},{"其他工具","其他工具"},{"工具","其他工具"},{"系统","系统"}};
        var groupMatches=groups.Where(g=>normalized.Contains(g.Key)).OrderBy(g=>normalized.IndexOf(g.Key,StringComparison.Ordinal)).Select(g=>g.Value).Distinct().ToArray();
        if(groupMatches.Length>0)p.Group=groupMatches[0];if(groupMatches.Length>1)p.OtherGroup=groupMatches[1];
        p.Direction=Has(q,"最后|末尾|end|last")?"last":Has(q,"最前|首位|第一|first")?"first":Has(q,"右|之后|后面|后边|after|right")?"after":"before";
        if(p.Intent=="move"&&p.AppName==""&&p.Group!="")p.Intent="sort";
        if(resolved.Matches.Count>1&&(p.Intent=="add"||p.Intent=="group")){
            if(resolved.Matches.Count>4||(p.Intent=="group"&&p.Group=="")){p.Intent="unknown";p.Message="请指定分类，最多四个软件。";return p;}
            var steps=new List<DockPlan>();foreach(var entry in resolved.Matches){if(p.Intent=="add"||!entry.Pinned)steps.Add(new DockPlan{Intent="add",AppName=entry.Name,TargetKey=AppCatalog.Key(entry),ResolvedEntry=entry,Group=p.Group});if(p.Intent=="group")steps.Add(new DockPlan{Intent="group",AppName=entry.Name,TargetKey=AppCatalog.Key(entry),Group=p.Group});}
            p.Intent="batch";p.Steps=steps;return p;
        }
        if(p.Intent=="theme"){
            p.Theme=Has(q,"石墨|纯色|不透明|graphite|不要毛玻璃")?"graphite":Has(q,"浅色|亮色|light")?"light":Has(q,"深色|暗色|黑色|磨砂|毛玻璃|透明|dark|glass")?"glass":"";
            if(p.Theme==""){p.Intent="help";p.Message="可以选择：深色磨砂、浅色玻璃、石墨。";}
        }
        if(p.Intent=="size"){
            var n=Regex.Match(q,@"(\d+(?:\.\d+)?)\s*(%|％|倍)?");
            if(n.Success){double v=double.Parse(n.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);p.Scale=n.Groups[2].Value=="倍"?v:v>2?v/100:v;if(p.Scale<.65||p.Scale>1.4){p.Intent="unknown";p.Message="大小支持 65%–140%。";}}
            else p.Delta=Has(q,"小一点|缩小|变小|太大|紧凑|smaller")?-.1:.1;
        }
        if(new[]{"add","remove","move","group","open","close","hide","show","locate"}.Contains(p.Intent)&&p.AppName==""){p.Message="没有找到软件。搜索或拖入入口。";p.Intent="unknown";}
        if(p.Intent=="group"&&p.Group==""){p.Intent="help";p.Message="可用分类：AI、办公、游戏、其他工具。";}
        if(p.Intent=="unknown"&&p.Message=="")p.Message="我只处理 Dock 操作。试试“添加微信”“图标小一点”或“换成石墨”。";
        return p;
    }
    public static IEnumerable<string> Aliases(string name){
        yield return name;
        if(Regex.IsMatch(name,"GPT",RegexOptions.IgnoreCase)){yield return "GPT";yield return "ChatGPT";}
        if(name.Contains("微信")){yield return "微信";yield return "wechat";yield return "wx";}
        if(name.Contains("浏览器")||name.Contains("Edge")){yield return "浏览器";yield return "edge";}
        if(name.Contains("网易云")){yield return "网易云";yield return "wyy";}
        if(name.Contains("三角洲")){yield return "三角洲";yield return "sjz";}
        if(name.IndexOf("v2ray",StringComparison.OrdinalIgnoreCase)>=0){yield return "v2RayN";yield return "v2ray";}
    }
}
