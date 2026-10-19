using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public partial class Entry {
    public string Id{get;set;}
    public string CanonicalId{get;set;}
    public string SourceShortcut{get;set;}
    public string Fingerprint{get;set;}
    public string BindingStatus{get;set;}
}
public sealed class SoftwareMatch {
    public List<Entry> Matches=new List<Entry>();public bool Ambiguous;public double Confidence;public string Message="";
}
public static class SoftwareResolver {
    static readonly Dictionary<string,string[]> aliases=new Dictionary<string,string[]>{
        {"deltaforce",new[]{"三角洲","三角洲行动","deltaforce","deltaforceclient","sanjiaozhou","sjz"}},
        {"valorant",new[]{"无畏契约","valorant","wuweiqiyue","wwqy"}},
        {"chatgpt",new[]{"GPT","ChatGPT","OpenAI Codex"}},
        {"cursor",new[]{"Cursor","cursor编辑器"}},
        {"edge",new[]{"Microsoft Edge","浏览器","edge","llq"}},
        {"chrome",new[]{"Google Chrome","Chrome","谷歌浏览器"}},
        {"wechat",new[]{"微信","wechat","weixin","wx"}},
        {"qq",new[]{"QQ","腾讯QQ","tengxunqq"}},
        {"cloudmusic",new[]{"网易云音乐","网易云","cloudmusic","wangyiyun","wyy"}},
        {"v2rayn",new[]{"v2RayN","v2ray","v二rayn"}},
        {"steam",new[]{"Steam","蒸汽平台"}},
        {"wegame",new[]{"WeGame","腾讯游戏平台"}},
        {"blender",new[]{"Blender","blenderlauncher","布兰德"}},
        {"unreal",new[]{"UE5","Unreal Engine","UnrealEditor","虚幻引擎"}},
        {"cubase",new[]{"Cubase","Cubase15","库贝斯"}},
        {"wemeet",new[]{"腾讯会议","WeMeet","tengxunhuiyi","txhy"}},
        {"workbuddy",new[]{"WorkBuddy","work buddy"}},
        {"feishu",new[]{"飞书","Feishu","Lark","feishuapp"}},
        {"uuremote",new[]{"UU远程","UU远程控制","uuremote","uudesktop"}},
        {"leigod",new[]{"雷神加速器","雷神","leigod","leishen"}}
    };
    public static string Canonical(Entry entry){
        if(!String.IsNullOrEmpty(entry.CanonicalId))return entry.CanonicalId;
        string name=IntentModel.Normalize(entry.Name),target=IntentModel.Normalize(entry.Target);
        foreach(var pair in aliases)if(pair.Value.Any(a=>name==IntentModel.Normalize(a)))return pair.Key;
        foreach(var pair in aliases)if(pair.Value.Where(a=>a.Length>3).Any(a=>target.Contains(IntentModel.Normalize(a))))return pair.Key;
        return name;
    }
    public static IEnumerable<string> Aliases(Entry entry){
        yield return entry.Name;string[] list;if(aliases.TryGetValue(Canonical(entry),out list))foreach(var a in list)yield return a;
        string file="";try{file=Path.GetFileNameWithoutExtension(entry.Target);}catch{}
        if(file.Length>=4&&!new[]{"launcher","explorer","powershell","pythonw","setup"}.Contains(file.ToLowerInvariant()))yield return file;
    }
    public static int Distance(string a,string b){
        if(Math.Abs(a.Length-b.Length)>3)return 99;int[] prev=Enumerable.Range(0,b.Length+1).ToArray();
        for(int i=1;i<=a.Length;i++){var next=new int[b.Length+1];next[0]=i;for(int j=1;j<=b.Length;j++)next[j]=Math.Min(Math.Min(next[j-1]+1,prev[j]+1),prev[j-1]+(a[i-1]==b[j-1]?0:1));prev=next;}return prev[b.Length];
    }
    static Entry Prefer(IEnumerable<Entry> list){return list.OrderByDescending(e=>e.Pinned&&File.Exists(e.Target)).ThenByDescending(e=>e.Target.IndexOf("WeGameLauncher",StringComparison.OrdinalIgnoreCase)>=0).ThenByDescending(e=>File.Exists(e.Target)).First();}
    public static SoftwareMatch Resolve(string query,IEnumerable<Entry> catalog){
        var result=new SoftwareMatch();string q=IntentModel.Normalize(query);
        var apps=catalog.Where(e=>!String.IsNullOrWhiteSpace(e.Name)).GroupBy(Canonical).Select(Prefer).ToArray();
        var exact=new List<Tuple<Entry,int,int>>();
        foreach(var e in apps){var best=Aliases(e).Select(IntentModel.Normalize).Where(a=>a.Length>=2&&q.Contains(a)).OrderByDescending(a=>a.Length).FirstOrDefault();if(best!=null)exact.Add(Tuple.Create(e,q.IndexOf(best,StringComparison.Ordinal),best.Length));}
        if(exact.Count>0){
            result.Matches=exact.OrderByDescending(x=>x.Item3).GroupBy(x=>x.Item2).Select(g=>g.First()).OrderBy(x=>x.Item2).Select(x=>x.Item1).ToList();result.Confidence=1;
            foreach(var match in result.Matches.ToArray()){
                var viable=catalog.Where(e=>Canonical(e)==Canonical(match)&&File.Exists(e.Target)).GroupBy(AppCatalog.Key).Select(g=>g.First()).ToList();
                if(viable.Any(e=>e.Pinned))continue;
                if(Canonical(match)=="deltaforce"||Canonical(match)=="valorant"){var launchers=viable.Where(e=>e.Target.IndexOf("WeGameLauncher",StringComparison.OrdinalIgnoreCase)>=0).ToList();if(launchers.Count==1)continue;}
                if(viable.Count>1){result.Ambiguous=true;result.Matches=viable.Take(3).ToList();result.Message="找到多个安装入口，请通过加号选择具体位置：\n"+String.Join("\n",result.Matches.Select(e=>e.Name+" · "+e.Target));break;}
            }return result;
        }
        string phrase=query.Trim().ToLowerInvariant();
        phrase=Regex.Replace(phrase,@"^(?:(?:麻烦|请|帮我|给我|我想|想|能不能|能否|把|将|再|也|顺便|帮忙|添加|加一个|加个|加上|加入|固定|移除|删除|删掉|取消固定|找一下|搜索|查找|加|个|一个|软件|应用|程序|入口|快捷方式)\s*)+","");
        phrase=Regex.Replace(phrase,@"(?:到|进|放到|放进)?(?:dock|底栏|启动栏)(?:上|里|里面)?$","");phrase=IntentModel.Normalize(phrase);
        if(phrase.Length<3||phrase.Length>32)return result;
        var scores=new List<Tuple<Entry,double>>();
        foreach(var e in apps){double best=0;foreach(string alias in Aliases(e)){
            string a=IntentModel.Normalize(alias);if(a.Length<3)continue;int distance=Distance(phrase,a);if(distance>Math.Max(1,a.Length/7))continue;
            double score=1.0-distance/(double)Math.Max(a.Length,phrase.Length);if(distance==1&&a.Length==3)score=.87;best=Math.Max(best,score);
        }if(best>=.82)scores.Add(Tuple.Create(e,best));}
        scores=scores.OrderByDescending(x=>x.Item2).ToList();if(scores.Count==0)return result;
        result.Confidence=scores[0].Item2;
        if(scores.Count>1&&scores[0].Item2-scores[1].Item2<.1){result.Ambiguous=true;result.Matches=scores.Take(3).Select(x=>x.Item1).ToList();result.Message="找到多个相近的软件，请选择具体入口："+String.Join("、",result.Matches.Select(x=>x.Name));}
        else result.Matches.Add(scores[0].Item1);
        return result;
    }
}
