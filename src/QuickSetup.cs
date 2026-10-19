using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

public static class Recommendations {
    static string Rot13(string text){return new string(text.Select(c=>c>='a'&&c<='z'?(char)('a'+(c-'a'+13)%26):c>='A'&&c<='Z'?(char)('A'+(c-'A'+13)%26):c).ToArray());}
    // Call only after the optional checkbox is explicitly selected for this setup run.
    public static Dictionary<string,int> ReadUsage(bool consent){
        var result=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);if(!consent)return result;
        using(var root=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist"))if(root!=null)foreach(string sub in root.GetSubKeyNames())try{using(var count=root.OpenSubKey(sub+@"\Count"))if(count!=null)foreach(string name in count.GetValueNames()){var data=count.GetValue(name) as byte[];if(data==null||data.Length<72)continue;int n=BitConverter.ToInt32(data,4);if(n>0)result[Rot13(name)]=Math.Min(n,100000);}}catch{}
        return result;
    }
    public static bool HumanApp(Entry e){
        string q=(e.Name+" "+e.Target).ToLowerInvariant();
        if(new[]{"uninstall","卸载","安装器","安装程序","setup.exe","installer","redistributable","runtime","驱动","driver","steinberg download","steinberg activation","library manager","visual c++",".net","sdk","repair","crash","服务","更新器","updater","license","maintenance","帮助","文档","readme","声卡","asio","vstconnect","firmware"}.Any(q.Contains))return false;
        return File.Exists(e.Target)&&e.Target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||e.Arguments!=null&&e.Arguments.StartsWith("shell:AppsFolder\\",StringComparison.OrdinalIgnoreCase);
    }
    public static double Rank(Entry e,Dictionary<string,int> usage){
        bool packaged=e.Arguments!=null&&e.Arguments.StartsWith("shell:AppsFolder\\",StringComparison.OrdinalIgnoreCase);string id=packaged?e.Arguments.Substring("shell:AppsFolder\\".Length):e.Target;
        int count=usage.Where(p=>String.Equals(p.Key,id,StringComparison.OrdinalIgnoreCase)||(packaged&&p.Key.EndsWith("\\"+id,StringComparison.OrdinalIgnoreCase))||(!packaged&&p.Key.EndsWith("\\"+Path.GetFileName(e.Target),StringComparison.OrdinalIgnoreCase))||(!String.IsNullOrEmpty(e.SourceShortcut)&&p.Key.EndsWith("\\"+Path.GetFileName(e.SourceShortcut),StringComparison.OrdinalIgnoreCase))).Select(p=>p.Value).DefaultIfEmpty().Max();
        bool familiar=new[]{"wechat","qq","edge","chrome","cloudmusic","wemeet","feishu","steam","wegame","leigod","deltaforce","valorant","chatgpt","cursor","blender","unreal","cubase","uuremote","v2rayn"}.Contains(SoftwareResolver.Canonical(e));
        return Math.Log(1+count)*15+(familiar?40:0);
    }
}
public sealed partial class SetupWindow:GlassWindow {
    public bool UsageConsent {get{return logs.IsChecked==true;}}
    readonly BusyIndicator spinner=new BusyIndicator();readonly Dock dock;readonly CheckBox logs;readonly StackPanel list=new StackPanel();readonly TextBlock status;readonly Button apply,scan;readonly List<Tuple<CheckBox,Entry>> candidates=new List<Tuple<CheckBox,Entry>>();bool busy,closed;
    public SetupWindow(Dock owner){
        dock=owner;Title="一键配置";Width=600;Height=590;Background=Dock.B(owner.Settings.Appearance=="light"?"#E5ECF2":"#202630");Foreground=owner.Settings.Appearance=="light"?Dock.B("#253247"):Brushes.White;WindowStartupLocation=WindowStartupLocation.CenterScreen;Closed+=(s,e)=>closed=true;
        var root=new DockPanel{Margin=new Thickness(24)};Content=root;
        var top=new StackPanel();DockPanel.SetDock(top,System.Windows.Controls.Dock.Top);root.Children.Add(top);top.Children.Add(Dock.Text("一键配置",22,"#F1F5FA"));
        logs=new CheckBox{Content="同意读取本机应用使用记录，优先推荐常用软件",Foreground=Foreground,IsChecked=false,Margin=new Thickness(0,16,0,14)};top.Children.Add(logs);
        scan=new Button{Content="生成推荐",Padding=new Thickness(16,10,16,10),HorizontalAlignment=HorizontalAlignment.Left};Dock.ButtonStyle(scan);scan.Click+=(s,e)=>Discover();top.Children.Add(scan);
        status=Dock.Text("读取已安装应用。使用记录仅在勾选后读取，不上传。",12,"#AFC1D6");status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(0,14,0,14);var progress=new DockPanel();progress.Children.Add(spinner);progress.Children.Add(status);top.Children.Add(progress);
        apply=new Button{Content="加入 Dock",Padding=new Thickness(18,12,18,12),IsEnabled=false,Margin=new Thickness(0,14,0,0)};Dock.ButtonStyle(apply);DockPanel.SetDock(apply,System.Windows.Controls.Dock.Bottom);root.Children.Add(apply);apply.Click+=(s,e)=>{
            var selected=candidates.Where(p=>p.Item1.IsChecked==true).Select(p=>p.Item2).ToArray();if(selected.Length==0)return;
            dock.ApplyRecommendations(selected);Close();};root.Children.Add(new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});PanelChrome.Attach(this,root,owner);
    }
    object CandidateLabel(Entry entry){var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(dock.AppVisual(entry,24));row.Children.Add(new TextBlock{Text=entry.Name+" · "+entry.Group,Foreground=Foreground,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,0,0)});return row;}
    void Discover(){if(busy)return;busy=true;spinner.IsRunning=true;scan.IsEnabled=false;logs.IsEnabled=false;apply.IsEnabled=false;list.Children.Clear();candidates.Clear();bool consent=logs.IsChecked==true;status.Text="正在查找常用软件…";
        var worker=new Thread(()=>{try{var usage=Recommendations.ReadUsage(consent);var found=AppCatalog.Discover().Where(Recommendations.HumanApp).GroupBy(SoftwareResolver.Canonical).Select(g=>g.First()).Select(e=>new{entry=e,rank=Recommendations.Rank(e,usage)}).Where(x=>x.rank>0).OrderByDescending(x=>x.rank).Take(24).ToArray();if(closed)return;
            Dispatcher.BeginInvoke(new Action(()=>{busy=false;spinner.IsRunning=false;scan.IsEnabled=true;logs.IsEnabled=true;status.Text=found.Length+" 个推荐。勾选后加入。";foreach(var x in found){x.entry.Group=AppCatalog.GuessGroup(x.entry.Name+" "+SoftwareResolver.Canonical(x.entry));var cb=new CheckBox{Content=CandidateLabel(x.entry),Foreground=Foreground,IsChecked=false,Margin=new Thickness(3,10,3,10)};cb.Checked+=(a,b)=>apply.IsEnabled=candidates.Any(p=>p.Item1.IsChecked==true);cb.Unchecked+=(a,b)=>apply.IsEnabled=candidates.Any(p=>p.Item1.IsChecked==true);candidates.Add(Tuple.Create(cb,x.entry));list.Children.Add(cb);}}));
        }catch(Exception ex){if(!closed)Dispatcher.BeginInvoke(new Action(()=>{busy=false;spinner.IsRunning=false;scan.IsEnabled=true;logs.IsEnabled=true;status.Text=ex.Message;}));}});worker.IsBackground=true;worker.SetApartmentState(ApartmentState.STA);worker.Start();
    }
}
public partial class Dock {
    public void OpenSetup(){new SetupWindow(this).Show();}
    public void ApplyRecommendations(Entry[] apps){
        undo.Push(new DockSnapshot{Apps=Json.Deserialize<List<Entry>>(Json.Serialize(Entries)),Settings=Json.Deserialize<Prefs>(Json.Serialize(Settings))});
        foreach(var e in apps)CommandActions.Apply(new DockPlan{Intent="add",AppName=e.Name,ResolvedEntry=e},Entries,Settings,apps);
        SaveApps();Rebuild();if(bindings!=null){bindings.ResetWatchers();bindings.Request();}Toast("已加入 "+apps.Length+" 个入口");
    }
}
