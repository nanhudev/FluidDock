using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static class WorkspaceTests {
    public static void Run(Action<string,bool,string> check){
        var apps=AppCatalog.Defaults();apps.AddRange(new[]{new Entry{Name="Blender",Target=@"Z:\Apps\blender.exe",Group="开发",Pinned=true,Id="blend"},new Entry{Name="UE5",Target=@"Z:\Apps\UnrealEditor.exe",Group="开发",Pinned=true},new Entry{Name="GPT",Target=@"Z:\Apps\ChatGPT.exe",Group="AI",Pinned=true},new Entry{Name="微信",Target=@"Z:\Apps\weixin.exe",Group="办公",Pinned=true},new Entry{Name="QQ",Target=@"Z:\Apps\qq.exe",Group="办公",Pinned=true},new Entry{Name="Steam",Target=@"Z:\Apps\steam.exe",Group="游戏",Pinned=true}});
        var engine=new DockIntent();var phrases=new Dictionary<string,string>{
            {"我要开始开发了","workspace_switch"},{"切换到娱乐模式","workspace_switch"},{"进入开发工作空间","workspace_switch"},{"回到默认布局","workspace_switch"},{"将当前布局保存为学习工作区","workspace_save"},{"保存当前为论文工作区","workspace_save"},{"创建研究工作区","workspace_create"},{"删除研究工作区","workspace_delete"},{"把研究工作区改名为论文","workspace_rename"},{"打开开发工作区的全部应用","workspace_open"},{"把微信加入开发工作区","workspace_add"},{"列出我的工作区","workspace_list"},{"管理工作空间","workspace_list"},{"switch to development workspace","workspace_switch"},{"save current layout as Research workspace","workspace_save"},
            {"把微信改名为聊天","rename"},{"rename QQ to Chat","rename"},{"隐藏QQ入口","hide"},{"隐藏微信","hide"},{"恢复QQ图标","show"},{"显示微信","show"},{"查看Blender所在文件夹","locate"},{"看看微信的使用统计","usage"},
            {"不要切换到开发工作区","unknown"},{"别隐藏QQ入口","unknown"},{"不要把微信改名为聊天","unknown"},{"创建工作区","unknown"},{"将当前布局保存为工作区","unknown"},{"把QQ和微信加入开发工作区","unknown"},{"切换到开发工作区然后格式化硬盘","unknown"}
        };
        foreach(var pair in phrases){var plan=engine.Parse(pair.Key,apps);check("expanded-intent: "+pair.Key,plan.Intent==pair.Value,plan.Intent+" · "+plan.Message);}
        var prefs=new Prefs();string original=Dock.Json.Serialize(apps);
        CommandActions.Apply(new DockPlan{Intent="workspace_switch",WorkspaceName="开发"},apps,prefs,apps.ToArray());
        check("workspace-template",prefs.ActiveWorkspace=="开发"&&apps.Where(e=>e.Pinned&&!WorkspaceStore.SystemEntry(e)).All(e=>e.Group=="开发"||e.Group=="AI"),"development narrows dock only");
        check("workspace-default-preserved",WorkspaceStore.Find(prefs,"默认").Apps.Count==6,"first switch keeps the complete previous layout");
        check("workspace-system-entries",apps.Where(WorkspaceStore.SystemEntry).Count()==3&&apps.Where(WorkspaceStore.SystemEntry).All(e=>e.Pinned),"system entries remain");
        apps.Single(e=>e.Name=="GPT").Pinned=false;CommandActions.Apply(new DockPlan{Intent="workspace_switch",WorkspaceName="默认"},apps,prefs,apps.ToArray());
        check("workspace-restores-default",apps.Count(e=>e.Pinned&&!WorkspaceStore.SystemEntry(e))==6,"switch back restores entries");
        CommandActions.Apply(new DockPlan{Intent="workspace_switch",WorkspaceName="开发"},apps,prefs,apps.ToArray());
        check("workspace-auto-saves-source",!apps.Single(e=>e.Name=="GPT").Pinned&&apps.Single(e=>e.Name=="Blender").Pinned,"edits stay with the source workspace");
        apps.Single(e=>e.Name=="Blender").Target=@"Y:\Moved\blender.exe";CommandActions.Apply(new DockPlan{Intent="workspace_switch",WorkspaceName="默认"},apps,prefs,apps.ToArray());
        check("workspace-retains-repaired-binding",apps.Single(e=>e.Id=="blend").Target.StartsWith("Y:"),"saved layout does not revert repaired executable paths");
        CommandActions.Apply(new DockPlan{Intent="workspace_create",WorkspaceName="研究"},apps,prefs,apps.ToArray());
        check("workspace-create-empty",WorkspaceStore.Find(prefs,"研究").Apps.Count==0,"empty profile retains no implicit application launches");
        CommandActions.Apply(new DockPlan{Intent="workspace_add",WorkspaceName="研究",AppName="微信",ResolvedEntry=apps.Single(e=>e.Name=="微信")},apps,prefs,apps.ToArray());
        CommandActions.Apply(new DockPlan{Intent="workspace_switch",WorkspaceName="研究"},apps,prefs,apps.ToArray());
        check("workspace-add-target",apps.Where(e=>e.Pinned&&!WorkspaceStore.SystemEntry(e)).Select(e=>e.Name).SequenceEqual(new[]{"微信"}),"only the selected profile gains the app");
        CommandActions.Apply(new DockPlan{Intent="workspace_rename",WorkspaceName="研究",NewName="论文"},apps,prefs,apps.ToArray());
        check("workspace-rename-active",prefs.ActiveWorkspace=="论文"&&WorkspaceStore.Find(prefs,"论文")!=null,"rename follows active profile");
        string before=Dock.Json.Serialize(new{apps=apps,prefs=prefs});bool rejected=false;try{CommandActions.Apply(new DockPlan{Intent="workspace_delete",WorkspaceName="论文"},apps,prefs,apps.ToArray());}catch{rejected=true;}
        check("workspace-delete-active-atomic",rejected&&before==Dock.Json.Serialize(new{apps=apps,prefs=prefs}),"active profile cannot be removed and state stays unchanged");
        rejected=false;try{CommandActions.Apply(new DockPlan{Intent="workspace_switch",WorkspaceName="不存在"},apps,prefs,apps.ToArray());}catch{rejected=true;}
        check("workspace-unknown-atomic",rejected&&before==Dock.Json.Serialize(new{apps=apps,prefs=prefs}),"unknown profile never changes settings");
        var restored=Dock.Json.Deserialize<Prefs>(Dock.Json.Serialize(prefs));check("workspace-persistence",WorkspaceStore.Find(restored,"论文").Apps.Single().Name=="微信"&&restored.ActiveWorkspace=="论文","profiles roundtrip through preference JSON");
        CommandActions.Apply(new DockPlan{Intent="workspace_switch",WorkspaceName="默认"},apps,prefs,apps.ToArray());CommandActions.Apply(new DockPlan{Intent="workspace_delete",WorkspaceName="论文"},apps,prefs,apps.ToArray());
        check("workspace-delete-inactive",WorkspaceStore.Find(prefs,"论文")==null&&apps.Any(e=>e.Name=="微信"),"delete removes profile, retains app");
        rejected=false;try{CommandActions.Apply(new DockPlan{Intent="workspace_delete",WorkspaceName="默认"},apps,prefs,apps.ToArray());}catch{rejected=true;}check("workspace-default-protected",rejected,"default baseline stays available");
        CommandActions.Apply(new DockPlan{Intent="hide",AppName="QQ"},apps,prefs,apps.ToArray());check("entry-hide",apps.Single(e=>e.Name=="QQ").Hidden,"hide is not uninstallation");
        CommandActions.Apply(new DockPlan{Intent="show",AppName="QQ"},apps,prefs,apps.ToArray());check("entry-show",!apps.Single(e=>e.Name=="QQ").Hidden&&apps.Single(e=>e.Name=="QQ").Pinned,"restore visible pin");
        string path=apps.Single(e=>e.Name=="QQ").Target;CommandActions.Apply(new DockPlan{Intent="rename",AppName="QQ",NewName="聊天"},apps,prefs,apps.ToArray());check("entry-rename-retains-target",apps.Single(e=>e.Name=="聊天").Target==path,"display name does not rewrite program path");
        rejected=false;try{CommandActions.Apply(new DockPlan{Intent="rename",AppName="聊天",NewName="微信"},apps,prefs,apps.ToArray());}catch{rejected=true;}check("entry-rename-collision",rejected,"duplicate visible names require correction");
        rejected=false;try{CommandActions.Apply(new DockPlan{Intent="hide",AppName="此电脑"},apps,prefs,apps.ToArray());}catch{rejected=true;}check("entry-system-protection",rejected,"system utility remains accessible");
    }
}
