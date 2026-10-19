using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

public static class AppCatalog {
    public static Entry FromFile(string file){
        file=Path.GetFullPath(Environment.ExpandEnvironmentVariables(file));
        if(!File.Exists(file)&&!Directory.Exists(file))throw new FileNotFoundException("文件不存在。",file);
        var e=new Entry{Name=Path.GetFileNameWithoutExtension(file),Target=file,Arguments="",WorkDir=Path.GetDirectoryName(file),Icon="",Group="其他工具",Pinned=false};
        if(file.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)){
            dynamic shell=null,link=null;
            try{shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));link=shell.CreateShortcut(file);e.Target=(string)link.TargetPath;e.Arguments=(string)link.Arguments;e.WorkDir=(string)link.WorkingDirectory;e.Icon=(string)link.IconLocation;}
            finally{if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
            if(String.IsNullOrWhiteSpace(e.Target))throw new InvalidDataException("此快捷方式没有可解析的启动目标。");
        }else if(file.EndsWith(".url",StringComparison.OrdinalIgnoreCase)){
            foreach(string line in File.ReadAllLines(file)){if(line.StartsWith("URL="))e.Target=line.Substring(4);if(line.StartsWith("IconFile="))e.Icon=line.Substring(9);}
        }
        e.CanonicalId=SoftwareResolver.Canonical(e);e.Id=Guid.NewGuid().ToString("N");e.SourceShortcut=file.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)?file:"";e.Group=e.Target.StartsWith("https://",StringComparison.OrdinalIgnoreCase)||e.Target.StartsWith("http://",StringComparison.OrdinalIgnoreCase)?"常用网站":GuessGroup(e.Name+" "+e.CanonicalId);return e;
    }
    public static string GuessGroup(string name){
        string n=name.ToLowerInvariant();
        if(new[]{"gpt","cursor","codex","workbuddy","codebuddy","agent","copilot","deepseek","dsh","ollama"}.Any(n.Contains))return "AI";
        if(new[]{"steam","wegame","epic","游戏","三角洲","无畏","雷神","sekai","deltaforce","valorant"}.Any(n.Contains))return "游戏";
        if(new[]{"blender","unreal","ue5","visual studio","unity","虚幻"}.Any(n.Contains))return "开发";
        if(new[]{"cubase","网易云","cloudmusic","spotify","音乐","fl studio","ableton"}.Any(n.Contains))return "音乐";
        if(new[]{"office","word","excel","powerpoint","wps","会议","微信","qq","edge","chrome","firefox","obs","飞书","feishu","lark","uu远程","uuremote","uudesktop","有道云","wechat","weixin","wemeet"}.Any(n.Contains))return "办公";
        return "其他工具";
    }
    static bool Eligible(Entry e){return !String.IsNullOrWhiteSpace(e.Target)&&!new[]{"卸载","uninstall","unins000","help","帮助","readme"}.Any(s=>e.Name.ToLowerInvariant().Contains(s));}
    public static List<Entry> Discover(Action<Entry> onFound=null){
        var list=new List<Entry>();Action<Entry> accept=e=>{list.Add(e);if(onFound!=null)onFound(e);};
        foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)}){
            if(!Directory.Exists(root))continue;
            try{foreach(string file in Directory.EnumerateFiles(root,"*.lnk",SearchOption.AllDirectories))try{var e=FromFile(file);if(Eligible(e))accept(e);}catch{}}catch{}
        }
        foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)}){
            if(!Directory.Exists(root))continue;
            try{foreach(string file in Directory.EnumerateFiles(root,"*.lnk"))try{var e=FromFile(file);if(Eligible(e))accept(e);}catch{}}catch{}
        }
        foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})try{
            using(var paths=hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths"))if(paths!=null)foreach(string name in paths.GetSubKeyNames())try{
                using(var key=paths.OpenSubKey(name)){string path=Environment.ExpandEnvironmentVariables(Convert.ToString(key.GetValue(""))).Trim('"');if(File.Exists(path)){var e=FromFile(path);if(Eligible(e))accept(e);}}
            }catch{}
        }catch{}
        foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})foreach(string uninstall in new[]{@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"})try{
            using(var root=hive.OpenSubKey(uninstall))if(root!=null)foreach(string sub in root.GetSubKeyNames())try{using(var key=root.OpenSubKey(sub)){
                string name=Convert.ToString(key.GetValue("DisplayName")),location=Environment.ExpandEnvironmentVariables(Convert.ToString(key.GetValue("InstallLocation"))).Trim('"'),icon=Environment.ExpandEnvironmentVariables(Convert.ToString(key.GetValue("DisplayIcon")));
                if(name=="")continue;string path=icon.StartsWith("\"")&&icon.IndexOf('"',1)>0?icon.Substring(1,icon.IndexOf('"',1)-1):icon.Split(',')[0].Trim('"');
                if(Directory.Exists(location)){string launcher=Path.Combine(location,"WeGameLauncher","launcher.exe");if(File.Exists(launcher))path=launcher;}
                if(!File.Exists(path)||!path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(path).ToLowerInvariant().Contains("unins"))continue;
                var e=FromFile(path);e.Name=name;e.CanonicalId=SoftwareResolver.Canonical(new Entry{Name=name,Target=path});e.Group=GuessGroup(name+" "+e.CanonicalId);if(Eligible(e))accept(e);
            }}catch{}
        }catch{}
        try{
            dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));dynamic folder=shell.NameSpace("shell:AppsFolder");dynamic items=folder.Items();
            foreach(dynamic item in items)try{string id=(string)item.Path;string name=(string)item.Name;if(id.Contains("!")&&!String.IsNullOrWhiteSpace(name))accept(new Entry{Name=name,Target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),Arguments="shell:AppsFolder\\"+id,WorkDir=Environment.GetFolderPath(Environment.SpecialFolder.Windows),Group=GuessGroup(name),Icon=""});}catch{}
            Marshal.FinalReleaseComObject(items);Marshal.FinalReleaseComObject(folder);Marshal.FinalReleaseComObject(shell);
        }catch{}
        return list.GroupBy(Key,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).OrderBy(e=>e.Name).ToList();
    }
    public static string Key(Entry e){return (e.Target??"")+"|"+(e.Arguments??"");}
    public static IEnumerable<Entry> ScanFolder(string root,int depth,int limit){
        var found=new List<Entry>();if(!Directory.Exists(root))return found;var queue=new Queue<Tuple<string,int>>();queue.Enqueue(Tuple.Create(root,0));int visited=0;
        while(queue.Count>0&&found.Count<limit&&visited++<2000){var item=queue.Dequeue();try{
            foreach(string file in Directory.EnumerateFiles(item.Item1).Where(f=>new[]{".lnk",".exe"}.Contains(Path.GetExtension(f).ToLowerInvariant())))try{var e=FromFile(file);if(Eligible(e))found.Add(e);if(found.Count>=limit)break;}catch{}
            if(item.Item2<depth)foreach(string child in Directory.EnumerateDirectories(item.Item1)){var info=new DirectoryInfo(child);if((info.Attributes&FileAttributes.ReparsePoint)!=0||new[]{"node_modules",".git",".venv","cache","saved","intermediate"}.Contains(info.Name.ToLowerInvariant()))continue;queue.Enqueue(Tuple.Create(child,item.Item2+1));}
        }catch{}}
        return found;
    }
    public static List<Entry> Defaults(){
        string win=Environment.GetFolderPath(Environment.SpecialFolder.Windows),desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return new List<Entry>{
            new Entry{Name="此电脑",Target=Path.Combine(win,"explorer.exe"),Arguments="shell:MyComputerFolder",WorkDir=win,Icon=Path.Combine(win,"System32","shell32.dll")+",15",Group="系统",Pinned=true},
            new Entry{Name="文件",Target=Path.Combine(win,"explorer.exe"),Arguments="\""+desktop+"\"",WorkDir=win,Icon=Path.Combine(win,"System32","shell32.dll")+",4",Group="系统",Pinned=true},
            new Entry{Name="回收站",Target=Path.Combine(win,"explorer.exe"),Arguments="shell:RecycleBinFolder",WorkDir=win,Icon=Path.Combine(win,"System32","imageres.dll")+",-55",Group="系统",Pinned=true}
        };
    }
}
