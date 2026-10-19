using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;

public sealed class BindingResult {public Entry Entry;public string Status;public bool Repaired;}
public static class EntryBindings {
    public static string Fingerprint(string path){
        try{var version=FileVersionInfo.GetVersionInfo(path);string metadata=(version.CompanyName??"")+"|"+(version.ProductName??"")+"|"+(version.OriginalFilename??"");if(metadata.Replace("|","").Length>4)return metadata.ToLowerInvariant();}catch{}
        try{using(var f=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var sha=SHA256.Create()){byte[] head=new byte[Math.Min(65536,(int)Math.Min(f.Length,65536))];int read=f.Read(head,0,head.Length);return f.Length+":"+Convert.ToBase64String(sha.ComputeHash(head,0,read));}}catch{return "";}
    }
    static Entry Copy(Entry e){return Dock.Json.Deserialize<Entry>(Dock.Json.Serialize(e));}
    static bool ValidIdentity(Entry e,string path){return File.Exists(path)&&!String.IsNullOrEmpty(e.Fingerprint)&&e.Fingerprint==Fingerprint(path);}
    public static BindingResult Resolve(Entry original,IEnumerable<Entry> catalog,string renameHint,string[] roots){
        var e=Copy(original);if(String.IsNullOrEmpty(e.Id))e.Id=Guid.NewGuid().ToString("N");e.CanonicalId=SoftwareResolver.Canonical(e);
        bool exists=File.Exists(e.Target);string currentFingerprint=exists?Fingerprint(e.Target):"";
        if(e.Target.Contains("://")||Directory.Exists(e.Target)||(exists&&(String.IsNullOrEmpty(e.Fingerprint)||e.Fingerprint==currentFingerprint))){e.BindingStatus="ready";if(exists&&String.IsNullOrEmpty(e.Fingerprint))e.Fingerprint=currentFingerprint;return new BindingResult{Entry=e,Status="ready"};}
        Entry found=null;
        if(renameHint!=null&&ValidIdentity(e,renameHint)){found=Copy(e);found.Target=renameHint;if(!Directory.Exists(e.WorkDir)||String.Equals(e.WorkDir,Path.GetDirectoryName(e.Target),StringComparison.OrdinalIgnoreCase))found.WorkDir=Path.GetDirectoryName(renameHint);}
        if(found==null&&!String.IsNullOrEmpty(e.SourceShortcut)&&File.Exists(e.SourceShortcut))try{
            var linked=AppCatalog.FromFile(e.SourceShortcut);if(File.Exists(linked.Target)&&SoftwareResolver.Canonical(linked)==e.CanonicalId&&(String.IsNullOrEmpty(e.Fingerprint)||ValidIdentity(e,linked.Target)))found=linked;
        }catch{}
        if(found==null)found=catalog.Where(c=>SoftwareResolver.Canonical(c)==e.CanonicalId&&ValidIdentity(e,c.Target)).OrderByDescending(c=>c.Pinned).FirstOrDefault();
        if(found==null&&roots!=null)foreach(string root in roots){foreach(var c in AppCatalog.ScanFolder(root,4,400))if(SoftwareResolver.Canonical(c)==e.CanonicalId&&ValidIdentity(e,c.Target)){found=c;break;}if(found!=null)break;}
        if(found==null){e.BindingStatus="missing";return new BindingResult{Entry=e,Status="missing"};}
        e.Target=found.Target;e.Arguments=found.Arguments;e.WorkDir=found.WorkDir;if(!String.IsNullOrEmpty(found.Icon))e.Icon=found.Icon;if(!String.IsNullOrEmpty(found.SourceShortcut))e.SourceShortcut=found.SourceShortcut;e.BindingStatus="repaired";
        return new BindingResult{Entry=e,Status="repaired",Repaired=true};
    }
}
public sealed class BindingMonitor:IDisposable {
    Dock dock;List<FileSystemWatcher> watchers=new List<FileSystemWatcher>();DispatcherTimer timer=new DispatcherTimer();Dictionary<string,string> hints=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);bool dirty=true,busy;int count;
    public BindingMonitor(Dock owner){dock=owner;ResetWatchers();timer.Interval=TimeSpan.FromSeconds(2);timer.Tick+=(s,e)=>{count++;if(count%30==0)dirty=true;if(dirty&&!busy)Validate();};timer.Start();}
    public void Request(){dirty=true;}
    public void ResetWatchers(){
        foreach(var watcher in watchers)watcher.Dispose();watchers.Clear();
        var folders=dock.Entries.Where(e=>e.Pinned&&e.Group!="系统"&&File.Exists(e.Target)).SelectMany(e=>new[]{Path.GetDirectoryName(e.Target),Path.GetDirectoryName(Path.GetDirectoryName(e.Target)??""),String.IsNullOrEmpty(e.SourceShortcut)?null:Path.GetDirectoryName(e.SourceShortcut)}).Where(p=>!String.IsNullOrEmpty(p)&&Directory.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(40);
        foreach(var folder in folders)try{
            var watcher=new FileSystemWatcher(folder){NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName,IncludeSubdirectories=false};
            watcher.Created+=(s,e)=>dock.Dispatcher.BeginInvoke(new Action(()=>dirty=true));watcher.Deleted+=(s,e)=>dock.Dispatcher.BeginInvoke(new Action(()=>dirty=true));
            watcher.Renamed+=(s,e)=>dock.Dispatcher.BeginInvoke(new Action(()=>{foreach(var app in dock.Entries){if(String.Equals(app.Target,e.OldFullPath,StringComparison.OrdinalIgnoreCase))hints[app.Id??app.Name]=e.FullPath;else if(app.Target.StartsWith(e.OldFullPath+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))hints[app.Id??app.Name]=e.FullPath+app.Target.Substring(e.OldFullPath.Length);}dirty=true;}));
            watcher.Error+=(s,e)=>dock.Dispatcher.BeginInvoke(new Action(()=>dirty=true));watcher.EnableRaisingEvents=true;watchers.Add(watcher);
        }catch{}
    }
    void Validate(){
        dirty=false;busy=true;var originals=dock.Entries.Where(e=>!e.Target.StartsWith("fluid://")).ToArray();var snapshot=Dock.Json.Deserialize<List<Entry>>(Dock.Json.Serialize(originals));var catalog=dock.AllApps().ToArray();var renameHints=new Dictionary<string,string>(hints);hints.Clear();var roots=dock.Settings.SearchRoots;
        Task.Run(()=>{var results=new List<BindingResult>();foreach(var e in snapshot){string hint;renameHints.TryGetValue(e.Id??e.Name,out hint);results.Add(EntryBindings.Resolve(e,catalog,hint,roots));}return results;}).ContinueWith(task=>dock.Dispatcher.BeginInvoke(new Action(()=>{
            busy=false;if(task.IsFaulted)return;bool changed=false;
            for(int i=0;i<originals.Length;i++){var old=originals[i];var result=task.Result[i];if(old.Target!=snapshot[i].Target)continue;if(old.Target!=result.Entry.Target||old.BindingStatus!=result.Status||old.Id!=result.Entry.Id||old.Fingerprint!=result.Entry.Fingerprint){dock.ApplyBinding(old,result);changed=true;}}
            if(changed){dock.SaveApps();ResetWatchers();}if(task.Result.Any(r=>r.Status=="missing"))dock.StartCatalog();
        })));
    }
    public void Dispose(){timer.Stop();foreach(var watcher in watchers)watcher.Dispose();watchers.Clear();}
}
public partial class Prefs {public string[] SearchRoots{get;set;}}
public partial class Dock {
    BindingMonitor bindings;
    public void ApplyBinding(Entry original,BindingResult result){original.Id=result.Entry.Id;original.CanonicalId=result.Entry.CanonicalId;original.Target=result.Entry.Target;original.Arguments=result.Entry.Arguments;original.WorkDir=result.Entry.WorkDir;original.Fingerprint=result.Entry.Fingerprint;original.BindingStatus=result.Status;original.SourceShortcut=result.Entry.SourceShortcut;original.Icon=result.Entry.Icon;}
    void DispatchLaunch(Entry entry){
        var snapshot=AllApps().ToArray();var roots=Settings.SearchRoots;
        Task.Run(()=>{try{
            var binding=EntryBindings.Resolve(entry,snapshot,null,roots);if(binding.Status=="missing")throw new FileNotFoundException("入口失效。可以重新定位快捷方式 / EXE，或添加新的搜索目录。");
            if(binding.Repaired)Dispatcher.BeginInvoke(new Action(()=>{ApplyBinding(entry,binding);SaveApps();}));var app=binding.Entry;
            bool exe=app.Target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase);var p=new ProcessStartInfo(app.Target,app.Arguments??""){UseShellExecute=!exe};if(!String.IsNullOrEmpty(app.WorkDir)&&Directory.Exists(app.WorkDir))p.WorkingDirectory=app.WorkDir;
            if(exe&&(app.Target.EndsWith("powershell.exe",StringComparison.OrdinalIgnoreCase)||app.Target.EndsWith("pythonw.exe",StringComparison.OrdinalIgnoreCase))){p.CreateNoWindow=true;p.WindowStyle=ProcessWindowStyle.Hidden;}
            Process.Start(p);
        }catch(Exception ex){Dispatcher.BeginInvoke(new Action(()=>Toast(entry.Name+" · "+ex.Message)));}});
    }
    public void ChooseSearchRoot(){
        if(search!=null)search.Hold=true;
        try{using(var folder=new System.Windows.Forms.FolderBrowserDialog{Description="选择软件所在目录，仅在这个目录内查找程序"})if(folder.ShowDialog()==System.Windows.Forms.DialogResult.OK){Settings.SearchRoots=(Settings.SearchRoots??new string[0]).Concat(new[]{folder.SelectedPath}).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();Persist();StartCatalog();}}
        finally{if(search!=null)search.Hold=false;}
    }
}
