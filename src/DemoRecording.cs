using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Diagnostics;

// Developer capture: native WPF visuals in an isolated fixture profile, never the desktop.
public partial class Dock {
    public bool RecordingDemo {get{return Environment.GetCommandLineArgs().Contains("--record-demo");}}
    void BeginDemoRecording(){
        var delay=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};delay.Tick+=(s,e)=>{delay.Stop();RecordDemo();};delay.Start();
    }
    void RecordDemo(){
        var catalogPath=Path.Combine(Home,"demo-catalog.json");
        if(!File.Exists(catalogPath))throw new InvalidOperationException("Prepare an isolated demo catalog first.");
        installed=Json.Deserialize<List<Entry>>(File.ReadAllText(catalogPath));
        Entries=AppCatalog.Defaults();Settings.Adaptive=false;Settings.ReducedMotion=false;Settings.DockScale=1;Settings.Appearance="glass";Settings.Widget=false;Settings.TrackUsage=false;Settings.HideDesktopIcons=false;Settings.StartWithWindows=false;ApplyAppearance();Rebuild();
        foreach(var e in installed){var entry=e;System.Threading.Tasks.Task.Run(()=>ReadIcon(entry));}
        string output=Path.Combine(Home,"native-demo.mp4");int frame=0;SetupWindow setup=null;CareWindow care=null;
        var encoder=new Process{StartInfo=new ProcessStartInfo("ffmpeg.exe","-v error -f image2pipe -vcodec png -framerate 30 -i pipe:0 -c:v libx264 -preset fast -crf 17 -pix_fmt yuv420p -movflags +faststart -y \""+output+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true}};encoder.Start();
        var steps=new SortedDictionary<int,Action>();Action<double,Action> at=(sec,action)=>steps.Add((int)Math.Round(sec*30),action);
        at(0,()=>{if(search!=null)search.Dismiss();});
        at(3,()=>{setup=new SetupWindow(this){ShowActivated=false};setup.Show();setup.DemoLoading();});
        at(4.5,()=>setup.DemoCandidates(installed.Where(e=>new[]{"浏览器","QQ","微信","网易云音乐","腾讯会议"}.Contains(e.Name)).ToArray()));
        at(5.5,()=>setup.DemoSelectAll());
        at(6.5,()=>Capture(setup,"setup-filled.png"));
        at(7,()=>setup.DemoCommit());
        at(8,()=>{OpenManager("assistant");search.IsHitTestVisible=false;search.Hold=true;search.DemoInput("帮我再加一下 Cursor 和 WorkBuddy");});
        at(9.5,()=>Capture(search,"assistant-add.png"));
        at(10.5,()=>search.DemoApply());
        at(12,()=>{foreach(string name in new[]{"Cursor","WorkBuddy"})if(!Entries.Any(e=>e.Name==name&&e.Pinned))throw new InvalidOperationException("Demo add failed: "+name);});
        at(13,()=>search.DemoInput("微信放 QQ 前面"));
        at(14,()=>search.DemoApply());
        at(15.5,()=>{if(Entries.FindIndex(e=>e.Name=="微信")>=Entries.FindIndex(e=>e.Name=="QQ"))throw new InvalidOperationException("Demo order failed");});
        at(16,()=>Capture(search,"assistant-order.png"));
        at(17,()=>search.DemoInput("把 Blender 新开一个开发分组，UE5 也放进去"));
        at(18.5,()=>search.DemoApply());
        at(20,()=>{foreach(string name in new[]{"Blender","UE5"})if(!Entries.Any(e=>e.Name==name&&e.Pinned&&e.Group=="开发"))throw new InvalidOperationException("Demo group failed: "+name);});
        at(20.5,()=>Capture(search,"assistant-groups.png"));
        at(22,()=>{search.Dismiss();foreach(var tile in tiles)tile.Target=1;botTarget=18;caption.Opacity=0;Wake();});
        at(24,()=>CaptureDemoFrame(Path.Combine(Home,"configured-overview.png")));
        at(26,()=>{care=new CareWindow(this){ShowActivated=false};care.Show();care.DemoLoading();});
        at(27,()=>Capture(care,"scan-progress.png"));
        at(28,()=>care.DemoCandidates());
        at(30,()=>care.DemoSelect());
        at(32,()=>Capture(care,"care-filled.png"));
        at(34,()=>{care.Close();botTarget=0;Wake();});
        at(35.5,()=>{botTarget=18;Wake();});
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(1000.0/30)};
        timer.Tick+=(s,e)=>{try{
            Action action;if(steps.TryGetValue(frame,out action))action();
            if(frame>=660&&frame<780){double x=Width/2-330+660*Math.Sin((frame-660)/120.0*Math.PI);Pointer(new Point(x,100));}
            CaptureDemoFrame(null,encoder.StandardInput.BaseStream);
            if(frame==60)CaptureDemoFrame(Path.Combine(Home,"desktop-overview.png"));
            frame++;if(frame>=1080){timer.Stop();encoder.StandardInput.Close();encoder.WaitForExit();if(search!=null)search.Hold=false;Atomic("recording.json",new{fps=30,frames=frame,width=1600,height=900,source="native WPF visual capture",fixtures=true,desktopCaptured=false,cleanupExecuted=false,encoderExitCode=encoder.ExitCode});encoder.Dispose();Close();}
        }catch(Exception ex){timer.Stop();try{encoder.StandardInput.Close();}catch{}Atomic("recording-error.json",new{frame=frame,error=ex.ToString()});Close();}};timer.Start();
    }
    void CaptureDemoFrame(string path,Stream stream=null){
        var visual=new DrawingVisual();using(var d=visual.RenderOpen()){
            d.DrawRectangle(new LinearGradientBrush(Color.FromRgb(20,30,44),Color.FromRgb(7,14,23),65),null,new Rect(0,0,1600,900));
            var halo=new RadialGradientBrush(Color.FromArgb(50,66,132,148),Colors.Transparent);d.DrawEllipse(halo,null,new Point(900,320),850,570);
            var area=SystemParameters.WorkArea;double offsetX=800-area.Left-area.Width/2,offsetY=900-area.Bottom+16;
            foreach(var window in Application.Current.Windows.Cast<Window>().Where(w=>w.IsVisible&&w!=glass).OrderBy(w=>w is CarryOverlay?3:w==this?1:2).ToArray()){
                if(window.ActualWidth<=0||window.ActualHeight<=0)continue;window.UpdateLayout();
                var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
                var rect=new Rect(window.Left+offsetX,window.Top+offsetY,window.ActualWidth,window.ActualHeight);d.PushOpacity(window.Opacity);d.DrawImage(bitmap,rect);d.Pop();
            }
        }
        var image=new RenderTargetBitmap(1600,900,96,96,PixelFormats.Pbgra32);image.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));if(stream!=null){using(var buffer=new MemoryStream()){encoder.Save(buffer);buffer.Position=0;buffer.CopyTo(stream);}}else using(var file=File.Create(path))encoder.Save(file);
    }
}
public partial class SearchPanel {
    public void DemoInput(string text){input.Text=text;}
    public void DemoApply(){if(pending==null||!pending.Actionable)throw new InvalidOperationException("Demo intent failed: "+input.Text);RunAssistant();}
}
public sealed partial class SetupWindow {
    public void DemoLoading(){IsHitTestVisible=false;spinner.IsRunning=true;scan.IsEnabled=false;status.Text="正在扫描应用… · 演示配置";}
    public void DemoCandidates(Entry[] entries){spinner.IsRunning=false;status.Text="示例推荐 · 使用记录未授权";foreach(var entry in entries){var cb=new CheckBox{Content=CandidateLabel(entry),Foreground=Foreground,Margin=new Thickness(3,10,3,10)};candidates.Add(Tuple.Create(cb,entry));list.Children.Add(cb);}scan.IsEnabled=true;}
    public void DemoSelect(){if(candidates.Count>0)candidates[0].Item1.IsChecked=true;apply.IsEnabled=true;}
    public void DemoSelectAll(){logs.IsChecked=true;foreach(var row in candidates)row.Item1.IsChecked=true;apply.IsEnabled=true;}
    public void DemoCommit(){dock.ApplyRecommendations(candidates.Where(p=>p.Item1.IsChecked==true).Select(p=>p.Item2).ToArray());Close();}
}
public sealed partial class CareWindow {
    public void DemoLoading(){IsHitTestVisible=false;spinner.IsRunning=true;status.Text="正在扫描大文件… · 演示配置";}
    public void DemoCandidates(){spinner.IsRunning=false;status.Text="示例数据 · 核对后勾选 · 本演示不执行清理";foreach(var item in new[]{
        new CareItem{Path=@"C:\Demo\Downloads\old-archive.zip",Bytes=1830L*1048576,Kind="demo",Reason="大文件，内容未判断"},
        new CareItem{Path=@"C:\Demo\Videos\screen-recording.mp4",Bytes=840L*1048576,Kind="demo",Reason="大文件，内容未判断"},
        new CareItem{Path=@"C:\Demo\Documents\project-assets.zip",Bytes=320L*1048576,Kind="demo",Reason="大文件，内容未判断"}
    }){var cb=new CheckBox{Content=new TextBlock{Text=item.Label,Foreground=Foreground,TextWrapping=TextWrapping.Wrap},Margin=new Thickness(3,12,3,12)};selections.Add(Tuple.Create(cb,item));rows.Children.Add(cb);}}
    public void DemoSelect(){selections[0].Item1.IsChecked=true;UpdateCount();}
}
