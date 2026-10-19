using System;
using System.Collections.Generic;
using System.Linq;
public static class SystemIntentTests {
    public static void Run(Action<string,bool,string> check){
        var apps=new[]{new Entry{Name="微信",Target=@"Z:\Weixin.exe"},new Entry{Name="QQ",Target=@"Z:\QQ.exe"},new Entry{Name="Cursor",Target=@"Z:\Cursor.exe"}};var engine=new DockIntent();
        foreach(var phrase in new Dictionary<string,string>{{"关机","shutdown"},{"帮我关闭电脑","shutdown"},{"重启电脑","restart"},{"请重新启动电脑","restart"},{"重启","restart"},{"更新并关机","update_shutdown"},{"更新后重启","update_restart"},{"锁屏","lock"},{"睡眠","sleep"},{"休眠","hibernate"},{"注销","signout"},{"取消关机","cancel"},{"打开电源菜单","menu"},{"restart computer","restart"},{"lock screen","lock"}}){var p=engine.Parse(phrase.Key,apps);check("power-intent:"+phrase.Key,p.Intent=="power"&&p.SystemAction==phrase.Value&&p.Actionable,p.Intent+":"+p.SystemAction);}
        foreach(var phrase in new Dictionary<string,string>{{"帮我重启微信","app_restart"},{"重新启动QQ","app_restart"},{"restart Cursor","app_restart"},{"最小化微信","app_minimize"},{"最大化QQ","app_maximize"},{"切换到微信","app_focus"},{"唤起Cursor","app_focus"},{"聚焦QQ","app_focus"},{"打开声音设置","system_settings"},{"打开Windows 更新设置","system_settings"},{"查看存储设置","system_settings"},{"打开下载文件夹","system_folder"},{"查看文档目录","system_folder"}}){var p=engine.Parse(phrase.Key,apps);check("system-intent:"+phrase.Key,p.Intent==phrase.Value&&p.Actionable,p.Intent);}
        foreach(string phrase in new[]{"不要重启电脑","别关机","暂不锁屏","不要睡眠","别重启微信","don't restart Cursor","重启不存在的xyz","重启微信和QQ","重启微信然后关机","重启QQ并清理磁盘","十分钟后关机","shutdown /f /t 1","重启电脑并格式化硬盘","切换到开发工作区然后关机","不要最大化微信"}){var p=engine.Parse(phrase,apps);check("system-negative:"+phrase,p.Intent=="unknown",p.Intent);}
        foreach(string action in new[]{"shutdown","restart","hibernate","signout","cancel"}){string args=SystemIntents.PowerArguments(action);check("power-no-force:"+action,!args.Contains("/f")&&!args.Contains("/m")&&(!args.Contains("/t")||args.EndsWith("/t 0")),args);}
        bool bad=false;try{SystemIntents.PowerArguments("/f /s");}catch(InvalidOperationException){bad=true;}check("power-action-whitelist",bad,"untrusted command string rejected");
        bad=false;try{SystemIntents.SettingsUri("cmd.exe");}catch(InvalidOperationException){bad=true;}check("settings-whitelist",bad,"only fixed settings endpoints");
        bad=false;try{SystemIntents.FolderArgument("C:\\secret");}catch(InvalidOperationException){bad=true;}check("folder-whitelist",bad,"only known shell locations");
    }
}
