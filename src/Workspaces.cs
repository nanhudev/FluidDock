using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public sealed class WorkspaceProfile {
    public string Name{get;set;}public List<Entry> Apps{get;set;}public string Appearance{get;set;}public double DockScale{get;set;}public string[] GroupOrder{get;set;}
}
public partial class Prefs {public List<WorkspaceProfile> Workspaces{get;set;}public string ActiveWorkspace{get;set;}}
public static class WorkspaceStore {
    static T Clone<T>(T value){return Dock.Json.Deserialize<T>(Dock.Json.Serialize(value));}
    public static bool SystemEntry(Entry entry){return entry.Group=="系统"||new[]{"此电脑","文件","回收站","桌面收纳"}.Contains(entry.Name);}
    public static string Name(string value){string name=(value??"").Trim().Trim('“','”','\"','\'');if(name.Length<1||name.Length>32||name.Any(Char.IsControl))throw new InvalidOperationException("工作空间名称为 1–32 个字符。");return name;}
    static WorkspaceProfile Snapshot(string name,List<Entry> entries,Prefs settings){return new WorkspaceProfile{Name=name,Apps=Clone(entries.Where(e=>e.Pinned&&!e.Hidden&&!SystemEntry(e)&&!e.Target.StartsWith("fluid://")).ToList()),Appearance=settings.Appearance,DockScale=settings.DockScale,GroupOrder=Clone(settings.GroupOrder)};}
    public static WorkspaceProfile Find(Prefs settings,string name){return (settings.Workspaces??new List<WorkspaceProfile>()).FirstOrDefault(w=>String.Equals(w.Name,name,StringComparison.OrdinalIgnoreCase));}
    public static bool Template(string name){return new[]{"开发","办公","娱乐","音乐"}.Contains(name);}
    public static List<Entry> TemplateApps(string name,IEnumerable<Entry> entries){return entries.Where(e=>e.Pinned&&!e.Hidden&&!SystemEntry(e)&&
        (name=="开发"?(e.Group=="开发"||e.Group=="AI"||new[]{"edge","chrome"}.Contains(SoftwareResolver.Canonical(e))):name=="娱乐"?(e.Group=="游戏"||e.Group=="音乐"):e.Group==name)).ToList();}
    public static List<Entry> Members(Prefs settings,string name,IEnumerable<Entry> entries){var profile=Find(settings,name);if(profile!=null)return profile.Apps??new List<Entry>();if(Template(name))return TemplateApps(name,entries);throw new InvalidOperationException("还没有“"+name+"”工作空间。先保存当前布局。");}
    static void Put(Prefs settings,WorkspaceProfile profile){var old=Find(settings,profile.Name);if(old!=null)settings.Workspaces.Remove(old);if(settings.Workspaces.Count>=32)throw new InvalidOperationException("最多保存 32 个工作空间。");settings.Workspaces.Add(profile);}
    public static string Apply(DockPlan plan,List<Entry> entries,Prefs settings,IEnumerable<Entry> catalog){
        var draft=Clone(entries);var prefs=Clone(settings);if(prefs.Workspaces==null)prefs.Workspaces=new List<WorkspaceProfile>();
        string result=ApplyCore(plan,draft,prefs,catalog.ToArray());
        entries.Clear();entries.AddRange(draft);settings.Workspaces=prefs.Workspaces;settings.ActiveWorkspace=prefs.ActiveWorkspace;settings.Appearance=prefs.Appearance;settings.DockScale=prefs.DockScale;settings.GroupOrder=prefs.GroupOrder;settings.Adaptive=prefs.Adaptive;return result;
    }
    static string ApplyCore(DockPlan plan,List<Entry> entries,Prefs settings,Entry[] catalog){
        string name=Name(plan.WorkspaceName);var profile=Find(settings,name);
        if(plan.Intent=="workspace_create"){if(profile!=null)throw new InvalidOperationException("这个工作空间已经存在。");if(Find(settings,"默认")==null)Put(settings,Snapshot("默认",entries,settings));Put(settings,Snapshot(name,new List<Entry>(),settings));return "已创建空工作空间 · "+name;}
        if(plan.Intent=="workspace_save"){
            if(Find(settings,"默认")==null)Put(settings,Snapshot("默认",entries,settings));Put(settings,Snapshot(name,entries,settings));settings.ActiveWorkspace=name;return "已保存工作空间 · "+name;
        }
        if(plan.Intent=="workspace_rename"){
            if(profile==null)throw new InvalidOperationException("没有这个工作空间。");if(name=="默认")throw new InvalidOperationException("默认工作空间保留原名。");string next=Name(plan.NewName);if(Find(settings,next)!=null)throw new InvalidOperationException("这个工作空间名称已存在。");profile.Name=next;if(settings.ActiveWorkspace==name)settings.ActiveWorkspace=next;return "已改名 · "+next;
        }
        if(plan.Intent=="workspace_delete"){
            if(profile==null)throw new InvalidOperationException("没有这个工作空间。");if(name=="默认")throw new InvalidOperationException("默认工作空间保留。");if(String.Equals(settings.ActiveWorkspace,name,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("先切换到其他工作空间，再删除它。");settings.Workspaces.Remove(profile);return "已删除工作空间 · "+name;
        }
        if(plan.Intent=="workspace_add"){
            if(profile==null)throw new InvalidOperationException("先保存这个工作空间。");var candidate=plan.ResolvedEntry??catalog.FirstOrDefault(e=>AppCatalog.Key(e)==plan.TargetKey);if(candidate==null||SystemEntry(candidate))throw new InvalidOperationException("请选择软件或网站入口。");
            if(profile.Apps==null)profile.Apps=new List<Entry>();if(!profile.Apps.Any(e=>AppCatalog.Key(e)==AppCatalog.Key(candidate))){var copy=Clone(candidate);copy.Pinned=true;copy.Hidden=false;profile.Apps.Add(copy);}
            if(settings.ActiveWorkspace==name){CommandActions.Apply(new DockPlan{Intent="add",AppName=candidate.Name,ResolvedEntry=candidate},entries,settings,catalog);}return "已加入“"+name+"” · "+candidate.Name;
        }
        if(plan.Intent!="workspace_switch")throw new InvalidOperationException("不支持这个工作空间操作。");
        if(profile==null){if(name=="默认")profile=Snapshot("默认",entries,settings);else if(Template(name)){var apps=TemplateApps(name,entries);if(apps.Count==0)throw new InvalidOperationException("没有适合这个场景的已固定入口。先配置并保存工作空间。");profile=Snapshot(name,apps,settings);}else throw new InvalidOperationException("还没有“"+name+"”工作空间。先保存当前布局。");}
        if(Find(settings,"默认")==null)Put(settings,Snapshot("默认",entries,settings));
        if(!String.IsNullOrEmpty(settings.ActiveWorkspace))Put(settings,Snapshot(settings.ActiveWorkspace,entries,settings));
        // Read the stored destination after saving the source (switching to itself is safe).
        var saved=Find(settings,name);if(saved!=null)profile=saved;else Put(settings,profile);
        var selected=new List<Entry>();foreach(var item in profile.Apps??new List<Entry>()){
            var current=entries.FirstOrDefault(e=>(!String.IsNullOrEmpty(item.Id)&&item.Id==e.Id)||AppCatalog.Key(item)==AppCatalog.Key(e));
            if(current==null){var matches=entries.Where(e=>SoftwareResolver.Canonical(e)==SoftwareResolver.Canonical(item)).ToArray();if(matches.Length>1)throw new InvalidOperationException("“"+item.Name+"”有多个入口，请重新定位。");current=matches.FirstOrDefault();}
            if(current==null){current=Clone(item);entries.Add(current);}if(SystemEntry(current))continue;current.Pinned=true;current.Hidden=false;current.Group=item.Group;selected.Add(current);
        }
        foreach(var item in entries.Where(e=>!SystemEntry(e)&&!selected.Contains(e)))item.Pinned=false;
        var rest=entries.Where(e=>!selected.Contains(e)).ToList();entries.Clear();entries.AddRange(selected);entries.AddRange(rest);
        settings.ActiveWorkspace=name;settings.Adaptive=false;settings.Appearance=new[]{"glass","light","graphite"}.Contains(profile.Appearance)?profile.Appearance:settings.Appearance;settings.DockScale=profile.DockScale>=.65&&profile.DockScale<=1.4?profile.DockScale:settings.DockScale;if(profile.GroupOrder!=null)settings.GroupOrder=Clone(profile.GroupOrder);
        return "已切换 · "+name+" · "+selected.Count+" 个入口";
    }
}
