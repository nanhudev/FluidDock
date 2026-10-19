using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Diagnostics;
using Forms=System.Windows.Forms;

public partial class Prefs {
    public double DockScale{get;set;}
    public string Appearance{get;set;}
    public string[] GroupOrder{get;set;}
    public bool TrackUsage{get;set;}
    public int CategoryVersion{get;set;}
    public bool HideDesktopIcons{get;set;}
    public bool StartWithWindows{get;set;}
}
public static class ProductData {
    public static string InstanceSuffix(){if(!Environment.GetCommandLineArgs().Contains("--data-dir"))return "";using(var sha=System.Security.Cryptography.SHA256.Create())return "."+BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Location().ToLowerInvariant()))).Replace("-","").Substring(0,12);}
    public static string Location(){
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--data-dir");if(i<0)i=Array.IndexOf(args,"--profile-dir");
        if(i>=0&&(i+1>=args.Length||args[i+1].StartsWith("--")))throw new ArgumentException("--data-dir requires a directory.");
        string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string folder=i>=0&&i+1<args.Length?Path.GetFullPath(args[i+1]):DurableState.ResolveHome(local);
        Directory.CreateDirectory(folder);Directory.CreateDirectory(Path.Combine(folder,"icons"));return folder;
    }
    public static void Ensure(string home){
        var path=Path.Combine(home,"apps.json");
        DurableState.Load<List<Entry>>(path,AppCatalog.Defaults);
    }
}
public sealed class DockSnapshot {public List<Entry> Apps;public Prefs Settings;public string RenamedFrom,RenamedTo;}
public static class CommandActions {
    public static string Apply(DockPlan plan,List<Entry> entries,Prefs settings,IEnumerable<Entry> catalog){
        if(plan.Intent.StartsWith("workspace_"))return WorkspaceStore.Apply(plan,entries,settings,catalog);
        if(plan.Intent=="batch"){
            if(plan.Steps==null||plan.Steps.Count==0||plan.Steps.Count>8||plan.Steps.Any(s=>!new[]{"add","group"}.Contains(s.Intent)))throw new InvalidOperationException("不支持组合执行这些操作。");
            var draft=Dock.Json.Deserialize<List<Entry>>(Dock.Json.Serialize(entries));foreach(var step in plan.Steps)Apply(step,draft,settings,catalog);entries.Clear();entries.AddRange(draft);return "已应用";
        }
        var app=entries.FirstOrDefault(e=>String.Equals(e.Name,plan.AppName,StringComparison.OrdinalIgnoreCase));
        if(new[]{"rename","hide","show"}.Contains(plan.Intent)){
            if(app==null&&plan.Intent=="show")return Apply(new DockPlan{Intent="add",AppName=plan.AppName,ResolvedEntry=plan.ResolvedEntry,TargetKey=plan.TargetKey},entries,settings,catalog);
            if(app==null||WorkspaceStore.SystemEntry(app))throw new InvalidOperationException("请选择已保存的软件入口。");
            if(plan.Intent=="rename"){string name=WorkspaceStore.Name(plan.NewName);if(entries.Any(e=>e!=app&&String.Equals(e.Name,name,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("已有同名入口。");app.Name=name;return "已改名 · "+name;}
            app.Hidden=plan.Intent=="hide";if(!app.Hidden)app.Pinned=true;return app.Hidden?"已隐藏入口 · "+app.Name:"已恢复入口 · "+app.Name;
        }
        if(plan.Intent=="add"){
            var candidate=plan.ResolvedEntry??catalog.FirstOrDefault(e=>plan.TargetKey!=""?AppCatalog.Key(e)==plan.TargetKey:String.Equals(e.Name,plan.AppName,StringComparison.OrdinalIgnoreCase));
            if(candidate==null)throw new InvalidOperationException("没有找到此软件，请选择快捷方式或 EXE。");
            if(app==null)app=entries.FirstOrDefault(e=>AppCatalog.Key(e)==AppCatalog.Key(candidate)||(!File.Exists(e.Target)&&e.Group!="系统"&&SoftwareResolver.Canonical(e)==SoftwareResolver.Canonical(candidate)));
            if(app==null){app=Dock.Json.Deserialize<Entry>(Dock.Json.Serialize(candidate));entries.Add(app);}
            else if(plan.ResolvedEntry!=null||!File.Exists(app.Target)){app.Target=candidate.Target;app.Arguments=candidate.Arguments;app.WorkDir=candidate.WorkDir;app.Icon=candidate.Icon;app.SourceShortcut=candidate.SourceShortcut;app.Fingerprint="";app.BindingStatus="ready";}
            if(!String.IsNullOrEmpty(plan.Group)){if(!new[]{"AI","办公","开发","音乐","游戏","常用网站","其他工具"}.Contains(plan.Group))throw new InvalidOperationException("未知分类。");app.Group=plan.Group;}
            app.Pinned=true;app.Hidden=false;return "已添加 "+app.Name;
        }
        if(plan.Intent=="remove"){
            if(app==null)throw new InvalidOperationException("此入口没有保存在 Dock 中。");
            if(app.Group=="系统")throw new InvalidOperationException("系统入口保持在系统区。");
            app.Pinned=false;return "已移出 Dock："+app.Name+"。软件仍保留。";
        }
        if(plan.Intent=="group"){
            if(app==null)throw new InvalidOperationException("先把此软件添加到 Dock。");
            if(app.Group=="系统")throw new InvalidOperationException("系统入口保持在系统区。");
            if(!new[]{"AI","办公","开发","音乐","游戏","常用网站","其他工具"}.Contains(plan.Group))throw new InvalidOperationException("未知分类。");
            app.Group=plan.Group;return "已将 "+app.Name+" 放入 "+plan.Group;
        }
        if(plan.Intent=="move"){
            settings.Adaptive=false;
            if(app==null||app.Group=="系统")throw new InvalidOperationException("请选择一个已固定的软件入口。");
            var other=entries.FirstOrDefault(e=>e.Name==plan.OtherName);
            if(other!=null&&other.Group!="系统")app.Group=other.Group;
            entries.Remove(app);
            int index=plan.Direction=="first"?entries.FindIndex(e=>e.Group==app.Group):plan.Direction=="last"?entries.FindLastIndex(e=>e.Group==app.Group)+1:other!=null?entries.IndexOf(other)+(plan.Direction=="after"?1:0):-1;
            if(other==null&&plan.Direction!="first"&&plan.Direction!="last"){
                var group=entries.Where(e=>e.Group==app.Group).ToList();index=plan.Direction=="after"?entries.FindLastIndex(e=>e.Group==app.Group)+1:entries.FindIndex(e=>e.Group==app.Group);
            }
            entries.Insert(index<0?entries.Count:Math.Min(index,entries.Count),app);return "已调整 "+app.Name+" 的位置";
        }
        if(plan.Intent=="sort"){
            if(plan.Group!=""&&plan.OtherGroup!=""){
                var order=(settings.GroupOrder??new[]{"系统","AI","办公","开发","音乐","游戏","常用网站","其他工具"}).ToList();
                if(plan.Group=="系统"||plan.OtherGroup=="系统")throw new InvalidOperationException("系统区固定在前面。");
                order.Remove(plan.Group);int index=order.IndexOf(plan.OtherGroup)+(plan.Direction=="after"?1:0);order.Insert(Math.Max(1,index),plan.Group);settings.GroupOrder=order.ToArray();return "已调整分类顺序";
            }
            return "已按使用习惯整理";
        }
        if(plan.Intent=="theme"){
            if(!new[]{"glass","light","graphite"}.Contains(plan.Theme))throw new InvalidOperationException("未知风格。");
            settings.Appearance=plan.Theme;return "风格已切换";
        }
        if(plan.Intent=="size"){
            double scale=plan.Scale>0?plan.Scale:settings.DockScale+plan.Delta;
            if(scale<.65||scale>1.4)throw new InvalidOperationException("大小支持 65%–140%。");
            settings.DockScale=scale;return "Dock 大小："+Math.Round(scale*100)+"%";
        }
        if(plan.Intent=="autohide"){settings.AutoHide=plan.Enable;return plan.Enable?"已开启自动收起":"已关闭自动收起";}
        if(plan.Intent=="startup"){settings.StartWithWindows=plan.Enable;return plan.Enable?"已开启登录自启动":"已关闭登录自启动";}
        if(plan.Intent=="widget"){settings.Widget=plan.Enable;return plan.Enable?"已显示时钟":"已隐藏时钟";}
        if(plan.Intent=="tracking"){settings.TrackUsage=plan.Enable;return plan.Enable?"已开启本地使用统计":"已关闭使用统计";}
        throw new InvalidOperationException("无法执行这个操作。");
    }
}
public partial class Dock {
    TranslateTransform botSlide;double botY=18,botVelocity,botTarget=18;
    bool AnimateBot(double dt){
        if(Settings.ReducedMotion){botY=botTarget;botVelocity=0;}else for(int i=0;i<4;i++){botVelocity+=(520*(botTarget-botY)-40*botVelocity)*dt/4;botY+=botVelocity*dt/4;}
        if(botSlide!=null)botSlide.Y=botY;return Math.Abs(botTarget-botY)>.03||Math.Abs(botVelocity)>.04;
    }
    void MigrateCategories(){if(Settings.CategoryVersion>=1)return;foreach(var e in Entries)if(e.Group!="系统"){string group=AppCatalog.GuessGroup(e.Name+" "+SoftwareResolver.Canonical(e));if(group=="开发"||group=="音乐"||SoftwareResolver.Canonical(e)=="uuremote")e.Group=group;}Settings.GroupOrder=new[]{"系统","AI","办公","开发","音乐","游戏","常用网站","其他工具"};Settings.CategoryVersion=1;SaveApps();}
    readonly DockIntent intent=new DockIntent();
    readonly Stack<DockSnapshot> undo=new Stack<DockSnapshot>();
    List<Entry> installed=new List<Entry>();
    public bool CatalogReady{get;private set;}
    public string ShortcutLabel="Ctrl + 空格";
    EventWaitHandle toggleEvent;RegisteredWaitHandle toggleWait;
    public IEnumerable<Entry> AllApps(){return Entries.Concat(installed).GroupBy(AppCatalog.Key,StringComparer.OrdinalIgnoreCase).Select(g=>g.First());}
    public DockPlan Interpret(string query){var plan=intent.Parse(query,AllApps());if(plan.Intent=="workspace_switch"||plan.Intent=="workspace_open"){
            if(WorkspaceStore.Find(Settings,plan.WorkspaceName)==null&&!WorkspaceStore.Template(plan.WorkspaceName)&&plan.WorkspaceName!="默认")return new DockPlan{Message="还没有“"+plan.WorkspaceName+"”。先保存当前布局为这个工作空间。"};
        }return plan;}
    void InitializeProduct(){
        try{string cache=Path.Combine(Home,"catalog-cache.json");if(File.Exists(cache)||File.Exists(cache+".bak"))installed=DurableState.Load<List<Entry>>(cache,()=>new List<Entry>());}catch{}
        toggleEvent=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\FluidDock.Toggle"+ProductData.InstanceSuffix());
        toggleWait=ThreadPool.RegisterWaitForSingleObject(toggleEvent,(s,t)=>Dispatcher.BeginInvoke(new Action(ToggleSearch)),null,Timeout.Infinite,false);
        Closed+=(s,e)=>{if(toggleWait!=null)toggleWait.Unregister(null);toggleEvent.Dispose();};
        if(!HotkeyRegistered){var handle=new WindowInteropHelper(this).Handle;HotkeyRegistered=Native.RegisterHotKey(handle,71,0x4003,0x20);ShortcutLabel="Ctrl + Alt + 空格";}
        if(SmokeUi||RecordingDemo)Settings.Widget=false;tray.Text="DockBot · "+ShortcutLabel;Loaded+=(s,e)=>{ApplyAppearance();if(!RecordingDemo&&Settings.StartWithWindows)SyncStartup();if(!RecordingDemo)bindings=new BindingMonitor(this);StartCatalog();if(RecordingDemo)BeginDemoRecording();if(SmokeUi){var start=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};start.Tick+=(a,b)=>{start.Stop();RunUiSmoke();};start.Start();}};Closed+=(s,e)=>{if(bindings!=null)bindings.Dispose();};
    }
    DateTime lastCatalogScan=DateTime.MinValue;bool indexing;
    internal void StartCatalog(){
        if(RecordingDemo){CatalogReady=true;return;}if(SmokeUi){string system=Environment.GetFolderPath(Environment.SpecialFolder.System);installed=new[]{"notepad.exe","calc.exe"}.Select(f=>AppCatalog.FromFile(Path.Combine(system,f))).ToList();CatalogReady=true;return;}
        if(indexing||(DateTime.UtcNow-lastCatalogScan).TotalSeconds<10)return;indexing=true;lastCatalogScan=DateTime.UtcNow;
        var roots=Settings.SearchRoots;
        var thread=new Thread(()=>{
            List<Entry> found;
            try{found=AppCatalog.Discover();if(roots!=null)foreach(var root in roots)found.AddRange(AppCatalog.ScanFolder(root,3,300));}
            catch(Exception ex){try{File.AppendAllText(Path.Combine(Home,"error.log"),DateTime.UtcNow+" catalog: "+ex+Environment.NewLine);Dispatcher.BeginInvoke(new Action(()=>{indexing=false;CatalogReady=true;}));}catch{}return;}
            try{DurableState.Save(Path.Combine(Home,"catalog-cache.json"),found);}catch{}
            Dispatcher.BeginInvoke(new Action(()=>{indexing=false;if(closing)return;installed=found;CatalogReady=true;if(search!=null)search.InvalidateCatalog();if(bindings!=null)bindings.Request();}));
        });thread.IsBackground=true;thread.SetApartmentState(ApartmentState.STA);thread.Start();
    }
    public void OpenManager(string mode){if(search==null){search=new SearchPanel(this);search.Prepare();}search.SetMode(mode);search.Reveal();}
    public string Execute(DockPlan plan,bool animate=true){
        if(new[]{"power","app_restart","app_focus","app_minimize","app_maximize","system_settings","system_folder"}.Contains(plan.Intent))return ExecuteSystem(plan);
        if(plan.Intent=="workspace_list"){OpenWorkspaces();return "选择工作空间。";}
        if(plan.Intent=="workspace_open"){
            var members=WorkspaceStore.Members(Settings,plan.WorkspaceName,Entries);if(members.Count==0)return "这个工作空间没有应用。";if(members.Count>12)throw new InvalidOperationException("一次最多启动 12 个入口，请分批打开。");
            int launched=0;foreach(var saved in members){var current=AllApps().FirstOrDefault(e=>AppCatalog.Key(e)==AppCatalog.Key(saved)||(!String.IsNullOrEmpty(saved.Id)&&saved.Id==e.Id))??saved;try{DispatchLaunch(current);launched++;}catch{}}
            return "已请求打开 "+launched+" / "+members.Count+" 个入口";
        }
        if(plan.Intent=="locate"){var target=AllApps().FirstOrDefault(e=>AppCatalog.Key(e)==plan.TargetKey);if(target==null||!File.Exists(target.Target))throw new InvalidOperationException("没有可定位的本地文件。");Process.Start("explorer.exe","/select,\""+target.Target+"\"");return "已显示文件位置";}
        if(plan.Intent=="usage"){OpenCategory("统计");return "本机使用统计";}
        if(plan.Intent=="care"){OpenCare();return "勾选后处理。";}
        if(plan.Intent=="configure"){OpenSetup();return "选择配置。";}
        if(plan.Intent=="open"||plan.Intent=="close"){
            var target=AllApps().FirstOrDefault(e=>AppCatalog.Key(e)==plan.TargetKey);if(target==null)throw new InvalidOperationException("没有找到软件。");
            if(plan.Intent=="open"){Launch(target);return "正在打开 "+target.Name;}
            if(target.Group=="系统"||!target.Target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||CareScan.Within(target.Target,Environment.GetFolderPath(Environment.SpecialFolder.Windows))||String.Equals(target.Target,System.Reflection.Assembly.GetExecutingAssembly().Location,StringComparison.OrdinalIgnoreCase))return "此入口无法关闭。";
            int closed=0;foreach(var process in Process.GetProcesses())using(process)try{if(process.Id==Process.GetCurrentProcess().Id||process.MainWindowHandle==IntPtr.Zero)continue;if(String.Equals(process.MainModule.FileName,target.Target,StringComparison.OrdinalIgnoreCase)&&process.CloseMainWindow())closed++;}catch{}
            return closed>0?"已请求关闭 "+target.Name:"未找到可关闭的窗口。";
        }
        if(plan.Intent=="undo"){
            if(undo.Count==0)return "暂时没有可撤销的改动。";
            var state=undo.Pop();if(state.RenamedFrom!=null&&state.RenamedTo!=null&&Usage.ContainsKey(state.RenamedTo)){Usage[state.RenamedFrom]=Usage[state.RenamedTo];Usage.Remove(state.RenamedTo);dirty=true;}activeEntry=null;lastPid=0;Entries=state.Apps;Settings=state.Settings;SaveApps();Persist();ApplyAppearance();Rebuild();SyncWidget();SyncStartup();return "已恢复上一步";
        }
        var snapshot=new DockSnapshot{Apps=Json.Deserialize<List<Entry>>(Json.Serialize(Entries)),Settings=Json.Deserialize<Prefs>(Json.Serialize(Settings))};
        var before=Entries.FirstOrDefault(e=>e.Name==plan.AppName);Point? from=before==null?(Point?)null:IconScreenPoint(before);
        string message;
        try{message=CommandActions.Apply(plan,Entries,Settings,AllApps().ToArray());}
        catch{Entries=snapshot.Apps;Settings=snapshot.Settings;throw;}
        undo.Push(snapshot);
        if(plan.Intent=="sort"&&plan.Group=="")SortSmart(false);
        if(animate&&!Settings.ReducedMotion){if(plan.Intent=="batch")foreach(var item in plan.Steps)carryingNames.Add(item.AppName);else if(new[]{"add","move","group"}.Contains(plan.Intent))carryingNames.Add(plan.AppName);}
        if(plan.Intent=="rename"&&before!=null&&Usage.ContainsKey(plan.AppName)){snapshot.RenamedFrom=plan.AppName;snapshot.RenamedTo=before.Name;Usage[before.Name]=Usage[plan.AppName];if(before.Name!=plan.AppName)Usage.Remove(plan.AppName);dirty=true;}
        SaveApps();Persist();ApplyAppearance();Rebuild();SyncWidget();if(bindings!=null){bindings.ResetWatchers();bindings.Request();}if(plan.Intent=="startup")SyncStartup();
        var changed=Entries.FirstOrDefault(e=>e.Name==plan.AppName);
        if(animate&&!Settings.ReducedMotion&&plan.Intent=="batch"){
            int index=0;foreach(var name in plan.Steps.Select(p=>p.AppName).Distinct()){
                var entry=Entries.FirstOrDefault(e=>e.Name==name);if(entry==null)continue;var copy=entry;if(RecordingDemo){int delay=index++*96;DemoSchedule(delay,()=>{var start=search.PointToScreen(new Point(search.Width*.52,search.Height*.65));CarryOverlay.Animate(this,copy,start,IconDestination(copy),false);});continue;}var tick=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1+index++*160)};tick.Tick+=(s,e)=>{tick.Stop();if(closing)return;var start=search!=null&&search.IsVisible?search.PointToScreen(new Point(search.Width*.52,search.Height*.65)):PointToScreen(new Point(Width/2,20));CarryOverlay.Animate(this,copy,start,IconDestination(copy),false);};tick.Start();
            }
        }
        if(animate&&!Settings.ReducedMotion&&changed!=null&&plan.Intent!="batch"){
            var start=from??(search!=null&&search.IsVisible?search.PointToScreen(new Point(search.Width/2,search.Height*.7)):PointToScreen(new Point(Width/2,30)));
            var finish=plan.Intent=="remove"?TrashScreenPoint():IconDestination(changed);
            CarryOverlay.Animate(this,changed,start,finish,plan.Intent=="remove");
        }
        return message;
    }
    void SyncStartup(){
        // Alternate data directories are isolated previews/tests, never the user's startup entry.
        if(ProductData.InstanceSuffix()!="")return;
        string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"DockBot.lnk");
        if(!Settings.StartWithWindows){if(File.Exists(path))File.Delete(path);return;}
        string legacy=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Fluid Dock.lnk");if(File.Exists(legacy))File.Delete(legacy);
        dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));dynamic link=shell.CreateShortcut(path);
        try{link.TargetPath=System.Reflection.Assembly.GetExecutingAssembly().Location;link.Arguments="--profile-dir \""+Home+"\"";link.WorkingDirectory=Path.GetDirectoryName(link.TargetPath);link.Description="DockBot";link.Save();}
        finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
    }
    void SyncWidget(){if(Settings.Widget&&widget==null){widget=new ClockWidget(this);widget.Closed+=(s,e)=>widget=null;widget.Show();}else if(!Settings.Widget&&widget!=null){widget.Close();widget=null;}}
    public Point IconScreenPoint(Entry e){var tile=tiles.FirstOrDefault(t=>AppCatalog.Key(t.App)==AppCatalog.Key(e));return tile==null?PointToScreen(new Point(Width/2,100)):tile.Button.PointToScreen(new Point(tile.Button.Width/2,48));}
    public Point IconDestination(Entry e){var tile=tiles.FirstOrDefault(t=>AppCatalog.Key(t.App)==AppCatalog.Key(e));return tile==null?PointToScreen(new Point(Width/2,100)):canvas.PointToScreen(new Point(tile.Center,100));}
    public Point TrashScreenPoint(){var e=Entries.FirstOrDefault(x=>x.Name=="回收站");return e==null?PointToScreen(new Point(Width/2,100)):IconScreenPoint(e);}
    public Rect PlateScreenRect(){var a=rim.PointToScreen(new Point(0,0));var b=rim.PointToScreen(new Point(rim.Width,rim.Height));return new Rect(a,b);}
    public Entry NearestEntry(Point screen){return tiles.Where(t=>!Utility(t.App)&&t.App.Target!="fluid://search"&&t.App.Target!="fluid://assistant").OrderBy(t=>(t.Button.PointToScreen(new Point(t.Button.Width/2,44))-screen).LengthSquared).Select(t=>t.App).FirstOrDefault();}
    void ApplyAppearance(){
        double scale=Math.Max(.65,Math.Min(1.4,Settings.DockScale));Settings.DockScale=scale;
        canvas.Height=156;canvas.VerticalAlignment=VerticalAlignment.Bottom;canvas.RenderTransformOrigin=new Point(.5,1);canvas.RenderTransform=new ScaleTransform(scale,scale);Height=156*Math.Max(1,scale);
        bool light=Settings.Appearance=="light",solid=Settings.Appearance=="graphite";ThemeState.Light=light;ThemeState.ReducedMotion=Settings.ReducedMotion;caption.Background=B(light?"#EEF0F4FA":"#E5252932");captionText.Foreground=B(light?"#253247":"#FFFFFF");
        rim.Background=solid?B("#FF202630"):PanelChrome.Material(light);Native.SetAppearance(glass,Settings.Appearance);
        if(search!=null)search.ApplyTheme(Settings.Appearance);if(widget!=null)Native.SetAppearance(widget,Settings.Appearance);Place();
    }
}
