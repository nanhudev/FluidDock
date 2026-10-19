using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public static class SystemIntents {
    public static readonly Dictionary<string,string> PowerNames=new Dictionary<string,string>{{"shutdown","关机"},{"restart","重启电脑"},{"sleep","睡眠"},{"hibernate","休眠"},{"lock","锁屏"},{"signout","注销"},{"update_shutdown","更新并关机"},{"update_restart","更新并重启"},{"cancel","取消待执行的关机"},{"menu","电源"}};
    static readonly Dictionary<string,string> settings=new Dictionary<string,string>{{"更新","windowsupdate"},{"声音","sound"},{"显示","display"},{"网络","network"},{"蓝牙","bluetooth"},{"存储","storagesense"},{"应用","appsfeatures"},{"电源","powersleep"}};
    static readonly Dictionary<string,string> folders=new Dictionary<string,string>{{"下载","Downloads"},{"文档","Personal"},{"桌面","Desktop"},{"图片","My Pictures"},{"视频","My Video"}};
    public static DockPlan Parse(string text,IEnumerable<Entry> catalog){
        string q=text.Trim().TrimEnd('。','！','!','?','？');for(int i=0;i<3;i++)q=Regex.Replace(q,@"^(请|帮我|麻烦|帮忙|能不能|我想)\s*","");
        if(Regex.IsMatch(q,@"不要|暂不|别|不想|don't|do not",RegexOptions.IgnoreCase)&&Regex.IsMatch(q,@"重启|重新启动|关机|锁屏|睡眠|休眠|注销|最大化|最小化|restart|reboot|shutdown|lock screen",RegexOptions.IgnoreCase))return new DockPlan{Message="没有改动。"};
        var exact=new Dictionary<string,string>{{"关机","shutdown"},{"关闭电脑","shutdown"},{"关闭计算机","shutdown"},{"重启","restart"},{"重启电脑","restart"},{"重新启动电脑","restart"},{"重启系统","restart"},{"更新并关机","update_shutdown"},{"更新后关机","update_shutdown"},{"更新并重启","update_restart"},{"更新后重启","update_restart"},{"锁屏","lock"},{"锁定电脑","lock"},{"睡眠","sleep"},{"让电脑睡眠","sleep"},{"休眠","hibernate"},{"注销","signout"},{"退出登录","signout"},{"取消关机","cancel"},{"取消重启","cancel"},{"打开电源菜单","menu"},{"电源菜单","menu"},{"shutdown","shutdown"},{"shut down","shutdown"},{"reboot","restart"},{"restart computer","restart"},{"lock screen","lock"},{"sleep computer","sleep"},{"hibernate","hibernate"},{"sign out","signout"}};
        string action;if(exact.TryGetValue(q.ToLowerInvariant(),out action))return new DockPlan{Intent="power",SystemAction=action};
        if(Regex.IsMatch(q,@"工作区|工作空间|模式|workspace",RegexOptions.IgnoreCase))return null;
        var m=Regex.Match(q,@"^(重启|重新启动|restart\s+|最小化|最大化|切换到|唤起|聚焦)(.+)$",RegexOptions.IgnoreCase);
        if(m.Success){
            string appQuery=m.Groups[2].Value.Trim();if(Regex.IsMatch(appQuery,@"然后|并且|之后|关机|格式化|执行|卸载|清理|并打开|并关闭|再打开|restart|shutdown",RegexOptions.IgnoreCase))return new DockPlan{Message="请一次操作一个应用。"};
            var found=SoftwareResolver.Resolve(appQuery,catalog);if(found.Ambiguous||found.Matches.Count!=1)return new DockPlan{Message=found.Ambiguous?found.Message:"请指定一个明确的软件入口。"};
            var app=found.Matches[0];string verb=m.Groups[1].Value;return new DockPlan{Intent=verb=="最小化"?"app_minimize":verb=="最大化"?"app_maximize":verb=="切换到"||verb=="唤起"||verb=="聚焦"?"app_focus":"app_restart",AppName=app.Name,TargetKey=AppCatalog.Key(app)};
        }
        m=Regex.Match(q,@"^(打开|查看)(Windows\s*)?(.+?)(设置|文件夹|目录)$",RegexOptions.IgnoreCase);
        if(m.Success){string key=m.Groups[3].Value.Trim();if(m.Groups[4].Value=="设置"&&settings.TryGetValue(key,out action))return new DockPlan{Intent="system_settings",SystemAction=action,AppName=key+"设置"};if(folders.TryGetValue(key,out action))return new DockPlan{Intent="system_folder",SystemAction=action,AppName=key+"文件夹"};}
        if(Regex.IsMatch(q,@"重启|重新启动|关机|锁屏|睡眠|休眠|注销|\breboot\b|\bshutdown\b|\brestart\b",RegexOptions.IgnoreCase))return new DockPlan{Message="请指定电脑或一个应用，例如“重启电脑”或“重启微信”。"};
        return null;
    }
    public static string PowerArguments(string action){switch(action){case "shutdown":return "/s /t 0";case "restart":return "/r /t 0";case "hibernate":return "/h";case "signout":return "/l";case "cancel":return "/a";default:throw new InvalidOperationException("无效电源操作。");}}
    public static string SettingsUri(string action){if(!settings.ContainsValue(action))throw new InvalidOperationException("未知设置。");return "ms-settings:"+action;}
    public static string FolderArgument(string action){if(!folders.ContainsValue(action))throw new InvalidOperationException("未知文件夹。");return "shell:"+action;}
}
public sealed class PowerGlyph:FrameworkElement {
    public PowerGlyph(){Width=32;Height=32;}
    protected override void OnRender(DrawingContext d){d.PushTransform(new ScaleTransform(ActualWidth/32,ActualHeight/32));var pen=new Pen(Dock.B("#D6EEE9"),2.3){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(new Point(9,7),false,false);c.ArcTo(new Point(23,7),new Size(11,11),0,true,SweepDirection.Counterclockwise,true,false);}d.DrawGeometry(null,pen,g);d.DrawLine(pen,new Point(16,3),new Point(16,15));d.Pop();}
}
public sealed class PowerWindow:GlassWindow {
    public PowerWindow(Dock dock){Owner=dock;Title="电源";Width=500;Height=510;WindowStartupLocation=WindowStartupLocation.CenterScreen;var root=new DockPanel{Margin=new Thickness(26)};var heading=Dock.Text("电源",22,"#F1F5FA");heading.Margin=new Thickness(0,0,0,22);DockPanel.SetDock(heading,System.Windows.Controls.Dock.Top);root.Children.Add(heading);var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};root.Children.Add(grid);foreach(string key in new[]{"lock","sleep","shutdown","restart","hibernate","signout","update_shutdown","update_restart"}){string action=key;var button=new Button{Content=SystemIntents.PowerNames[key],Padding=new Thickness(12),Margin=new Thickness(5),FontSize=16};Dock.ButtonStyle(button);button.Click+=(s,e)=>{try{dock.Execute(new DockPlan{Intent="power",SystemAction=action});}catch(Exception ex){dock.Toast(ex.Message);}};grid.Children.Add(button);}PanelChrome.Attach(this,root,dock);}
}
public partial class Dock {
    [DllImport("user32.dll")]static extern bool LockWorkStation();
    [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr h,int command);
    public void OpenPower(){new PowerWindow(this).Show();}
    string ExecuteSystem(DockPlan plan){
        if(SmokeUi||RecordingDemo)return "演示模式未执行系统操作。";
        if(plan.Intent=="system_settings"){Process.Start(SystemIntents.SettingsUri(plan.SystemAction));return "已打开 "+plan.AppName;}
        if(plan.Intent=="system_folder"){Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),SystemIntents.FolderArgument(plan.SystemAction));return "已打开 "+plan.AppName;}
        if(plan.Intent=="power"){
            string label;if(!SystemIntents.PowerNames.TryGetValue(plan.SystemAction,out label))throw new InvalidOperationException("未知电源操作。");
            if(plan.SystemAction=="menu"){OpenPower();return "电源";}
            bool update=plan.SystemAction.StartsWith("update_");
            if(!ConfirmSheet.AskAction(this,this,label,update?"打开 Windows 更新与关机面板，在系统提供的选项中选择“"+label+"”。":"即将"+label+"。请先保存正在编辑的内容。",update?"前往 Windows":label))return "已取消。";
            Persist();SaveApps();
            if(update){Process.Start("ms-settings:windowsupdate");dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));try{shell.ShutdownWindows();}finally{Marshal.FinalReleaseComObject(shell);}return "已打开 Windows 电源选项。";}
            if(plan.SystemAction=="lock"){if(!LockWorkStation())throw new InvalidOperationException("Windows 未接受锁屏请求。");}
            else if(plan.SystemAction=="sleep"){if(!System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Suspend,false,false))throw new InvalidOperationException("此设备暂不支持睡眠。");}
            else Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"shutdown.exe"),SystemIntents.PowerArguments(plan.SystemAction)){UseShellExecute=false,CreateNoWindow=true});
            return "已向 Windows 请求"+label+"。";
        }
        var entry=AllApps().FirstOrDefault(e=>AppCatalog.Key(e)==plan.TargetKey);
        if(entry==null||entry.Group=="系统"||!entry.Target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||!File.Exists(entry.Target)||CareScan.Within(entry.Target,Environment.GetFolderPath(Environment.SpecialFolder.Windows))||String.Equals(entry.Target,typeof(Dock).Assembly.Location,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("请选择普通应用的有效 EXE 入口。");
        if(plan.Intent=="app_restart"&&!ConfirmSheet.AskAction(this,this,"重启 "+entry.Name,"正常关闭后重新打开。若有保存提示，请先完成保存。","重启应用"))return "已取消。";
        Task.Run(()=>{
            var processes=new List<Process>();try{
                foreach(var p in Process.GetProcesses()){bool keep=false;try{keep=String.Equals(p.MainModule.FileName,entry.Target,StringComparison.OrdinalIgnoreCase);}catch{}if(keep)processes.Add(p);else p.Dispose();}
                if(plan.Intent=="app_restart"){
                    foreach(var p in processes)if(p.MainWindowHandle==IntPtr.Zero||!p.CloseMainWindow())throw new InvalidOperationException("应用仍有后台实例，未重启。");
                    foreach(var p in processes)if(!p.WaitForExit(8000))throw new InvalidOperationException("应用尚未退出，请完成保存后重试。");
                    Dispatcher.BeginInvoke(new Action(()=>DispatchLaunch(entry)));
                }else{
                    var window=processes.Select(p=>p.MainWindowHandle).FirstOrDefault(h=>h!=IntPtr.Zero);
                    if(window==IntPtr.Zero){if(plan.Intent=="app_focus")Dispatcher.BeginInvoke(new Action(()=>DispatchLaunch(entry)));else throw new InvalidOperationException("应用没有可操作的窗口。");}
                    else{ShowWindowAsync(window,plan.Intent=="app_minimize"?6:plan.Intent=="app_maximize"?3:9);if(plan.Intent!="app_minimize")Native.SetForegroundWindow(window);}
                }
            }catch(Exception ex){Dispatcher.BeginInvoke(new Action(()=>Toast(ex.Message)));}finally{foreach(var p in processes)p.Dispose();}
        });return plan.Intent=="app_restart"?"正在重启 "+entry.Name:"已请求调整 "+entry.Name+" 的窗口";
    }
}
