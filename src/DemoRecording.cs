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
    byte[] demoPixels;
    int demoFrame;readonly List<Tuple<int,Action>> demoQueue=new List<Tuple<int,Action>>();
    void DemoSchedule(int delay,Action action){demoQueue.Add(Tuple.Create(demoFrame+delay,action));}
    public void DemoScheduleRow(int delay,Action action){DemoSchedule(delay,action);}
    readonly List<Tuple<int,FrameworkElement>> demoReveals=new List<Tuple<int,FrameworkElement>>();
    public void DemoReveal(FrameworkElement element,int delay=0){if(element==null)return;element.Opacity=0;demoReveals.Add(Tuple.Create(demoFrame+delay,element));}
    void AdvanceDemoReveals(){foreach(var row in demoReveals.ToArray()){
        double t=(demoFrame-row.Item1)/30.0;if(t<0)continue;
        double spring=1-Math.Exp(-7*t)*(Math.Cos(11*t)+7.0/11*Math.Sin(11*t));
        row.Item2.RenderTransformOrigin=new Point(.5,.5);var transform=new TransformGroup();transform.Children.Add(new ScaleTransform(.94+.06*spring,.94+.06*spring));transform.Children.Add(new TranslateTransform(0,18*(1-spring)));row.Item2.RenderTransform=transform;row.Item2.Opacity=Math.Min(1,t*5);
        if(t>1.3){row.Item2.RenderTransform=Transform.Identity;row.Item2.Opacity=1;demoReveals.Remove(row);}
    }}
    public void PulseTrash(){var tile=tiles.First(t=>t.App.Name=="回收站");tile.ScaleV=3.5;tile.Bounce=-9;Wake();}
    public bool RecordingDemo {get{return Environment.GetCommandLineArgs().Contains("--record-demo");}}
    void BeginDemoRecording(){
        var delay=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};delay.Tick+=(s,e)=>{delay.Stop();RecordDemo();};delay.Start();
    }
    void RecordDemo(){
        var catalogPath=Path.Combine(Home,"demo-catalog.json");
        if(!File.Exists(catalogPath))throw new InvalidOperationException("Prepare an isolated demo catalog first.");
        installed=Json.Deserialize<List<Entry>>(File.ReadAllText(catalogPath));
        Entries=AppCatalog.Defaults();Settings.AutoHide=false;Settings.Adaptive=false;Settings.ReducedMotion=false;Settings.DockScale=1;Settings.Appearance="glass";Settings.Widget=false;Settings.TrackUsage=false;Settings.HideDesktopIcons=false;Settings.StartWithWindows=false;ApplyAppearance();Rebuild();
        foreach(var e in installed){var entry=e;System.Threading.Tasks.Task.Run(()=>ReadIcon(entry));}
        string output=Path.Combine(Home,"native-demo.mp4");int frame=0;SetupWindow setup=null;CareWindow care=null;
        var encoder=new Process{StartInfo=new ProcessStartInfo("ffmpeg.exe","-v error -f rawvideo -pixel_format bgra -video_size 3200x1800 -framerate 30 -i pipe:0 -c:v libx264 -preset fast -threads 2 -crf 16 -pix_fmt yuv420p -movflags +faststart -y \""+output+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true}};encoder.Start();
        var steps=new SortedDictionary<int,Action>();Action<double,Action> at=(sec,action)=>steps.Add((int)Math.Round(sec*30),action);
        at(0,()=>{if(search!=null)search.Dismiss();});
        at(4,()=>{setup=new SetupWindow(this){ShowActivated=false};setup.Show();setup.DemoLoading();DemoReveal((FrameworkElement)setup.Content);});
        at(5.5,()=>{int i=0;foreach(var name in new[]{"浏览器","QQ","微信","网易云音乐","腾讯会议"}){var entry=installed.First(e=>e.Name==name);DemoSchedule(i++*12,()=>setup.DemoCandidates(new[]{entry}));}});
        at(7.5,()=>setup.DemoScanDone());
        at(8,()=>setup.DemoSelectAll());
        at(10,()=>Capture(setup,"setup-filled.png"));
        at(11,()=>setup.DemoCommit());
        at(12,()=>{OpenManager("assistant");search.IsHitTestVisible=false;search.Hold=true;search.DemoInput("帮我再加一下 Cursor 和 WorkBuddy");});
        at(14,()=>Capture(search,"assistant-add.png"));
        at(15,()=>search.DemoApply());
        at(21.5,()=>{foreach(string name in new[]{"Cursor","WorkBuddy"})if(!Entries.Any(e=>e.Name==name&&e.Pinned))throw new InvalidOperationException("Demo add failed: "+name);});
        at(22,()=>search.DemoInput("微信放 QQ 前面"));
        at(25,()=>search.DemoApply());
        at(29,()=>{if(Entries.FindIndex(e=>e.Name=="微信")>=Entries.FindIndex(e=>e.Name=="QQ"))throw new InvalidOperationException("Demo order failed");Capture(search,"assistant-order.png");});
        at(30,()=>search.DemoInput("把 Blender 新开一个开发分组，UE5 也放进去"));
        at(33,()=>search.DemoApply());
        at(40,()=>{foreach(string name in new[]{"Blender","UE5"})if(!Entries.Any(e=>e.Name==name&&e.Pinned&&e.Group=="开发"))throw new InvalidOperationException("Demo group failed: "+name);Capture(search,"assistant-groups.png");});
        at(40.5,()=>{var selected=tiles.First(t=>t.App.Name=="Blender");Pointer(new Point(selected.Center,100));});
        at(42,()=>{search.Dismiss();foreach(var tile in tiles)tile.Target=1;botTarget=18;caption.Opacity=0;Wake();});
        at(45,()=>CaptureDemoFrame(Path.Combine(Home,"configured-overview.png")));
        at(47,()=>{care=new CareWindow(this){ShowActivated=false};care.Show();care.DemoLoading();DemoReveal((FrameworkElement)care.Content);});
        at(48,()=>Capture(care,"scan-progress.png"));
        at(49,()=>care.DemoCandidates());
        at(51.5,()=>care.DemoScanDone());
        at(53,()=>care.DemoSelect());
        at(56,()=>Capture(care,"care-filled.png"));
        at(57.5,()=>{var from=care.PointToScreen(new Point(care.Width*.4,235));care.Close();var file=new Entry{Name="old-archive.zip",Target="demo://parcel",Group="文件"};CarryOverlay.Animate(this,file,from,TrashScreenPoint(),true);botTarget=0;Wake();});
        at(60,()=>PulseTrash());
        at(63,()=>{botTarget=18;Wake();});
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(1000.0/30)};
        timer.Tick+=(s,e)=>{try{
            demoFrame=frame;Action action;if(steps.TryGetValue(frame,out action))action();
            foreach(var queued in demoQueue.Where(q=>q.Item1<=frame).ToArray()){demoQueue.Remove(queued);queued.Item2();}
            foreach(var ghost in Application.Current.Windows.OfType<CarryOverlay>().ToArray())ghost.AdvanceDemo();
            if(frame>=1260&&frame<1410){double x=Width/2-330+660*Math.Sin((frame-1260)/150.0*Math.PI);Pointer(new Point(x,100));}
            AdvanceDemoReveals();BusyIndicator.DemoFrame(frame/30.0);Render(null,EventArgs.Empty);CaptureDemoFrame(null,encoder.StandardInput.BaseStream);
            if(frame==60)CaptureDemoFrame(Path.Combine(Home,"desktop-overview.png"));
            frame++;if(frame>=1980){timer.Stop();encoder.StandardInput.Close();encoder.WaitForExit();if(search!=null)search.Hold=false;Atomic("recording.json",new{fps=30,frames=frame,width=3200,height=1800,source="native WPF visual capture",fixtures=true,desktopCaptured=false,cleanupExecuted=false,encoderExitCode=encoder.ExitCode});encoder.Dispose();Close();}
        }catch(Exception ex){timer.Stop();try{encoder.StandardInput.Close();}catch{}Atomic("recording-error.json",new{frame=frame,error=ex.ToString()});Close();}};timer.Start();
    }
    void CaptureDemoFrame(string path,Stream stream=null){
        var visual=new DrawingVisual();using(var d=visual.RenderOpen()){
            d.PushTransform(new ScaleTransform(2,2));
            d.DrawRectangle(new LinearGradientBrush(Color.FromRgb(20,30,44),Color.FromRgb(7,14,23),65),null,new Rect(0,0,1600,900));
            var halo=new RadialGradientBrush(Color.FromArgb(50,66,132,148),Colors.Transparent);d.DrawEllipse(halo,null,new Point(900,320),850,570);
            var area=SystemParameters.WorkArea;double offsetX=800-area.Left-area.Width/2,offsetY=900-area.Bottom+16;
            foreach(var window in Application.Current.Windows.Cast<Window>().Where(w=>w.IsVisible&&w!=glass).OrderBy(w=>w is CarryOverlay?3:w==this?1:2).ToArray()){
                if(window.ActualWidth<=0||window.ActualHeight<=0)continue;window.UpdateLayout();
                var carry=window as CarryOverlay;var bounds=carry==null?new Rect(0,0,window.ActualWidth,window.ActualHeight):carry.CaptureBounds;
                var high=new DrawingVisual();using(var hd=high.RenderOpen()){
                    var brush=new VisualBrush((Visual)window.Content){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=bounds,Stretch=Stretch.Fill};
                    hd.DrawRectangle(brush,null,new Rect(0,0,bounds.Width*3,bounds.Height*3));
                }
                var bitmap=new RenderTargetBitmap((int)(bounds.Width*3),(int)(bounds.Height*3),96,96,PixelFormats.Pbgra32);bitmap.Render(high);
                var rect=new Rect(window.Left+offsetX+bounds.X,window.Top+offsetY+bounds.Y,bounds.Width,bounds.Height);d.PushOpacity(window.Opacity);d.DrawImage(bitmap,rect);d.Pop();
            }
        }
        var image=new RenderTargetBitmap(3200,1800,96,96,PixelFormats.Pbgra32);image.Render(visual);if(stream!=null){if(demoPixels==null)demoPixels=new byte[3200*1800*4];image.CopyPixels(demoPixels,3200*4,0);stream.Write(demoPixels,0,demoPixels.Length);}else{var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(path))encoder.Save(file);}
    }
}
public partial class SearchPanel {
    public void DemoInput(string text){input.Text=text;}
    public void DemoApply(){if(pending==null||!pending.Actionable)throw new InvalidOperationException("Demo intent failed: "+input.Text);RunAssistant();}
}
public sealed partial class SetupWindow {
    public void DemoLoading(){IsHitTestVisible=false;spinner.IsRunning=true;scan.IsEnabled=false;status.Text="正在扫描应用… · 演示配置";}
    public void DemoCandidates(Entry[] entries){foreach(var entry in entries){var cb=new CheckBox{Content=CandidateLabel(entry),Foreground=Foreground,Margin=new Thickness(3,10,3,10)};candidates.Add(Tuple.Create(cb,entry));list.Children.Add(cb);dock.DemoReveal(cb);}status.Text="正在扫描应用… · 已发现 "+candidates.Count+" 个";}
    public void DemoScanDone(){spinner.IsRunning=false;status.Text="5 个推荐 · 选择后加入";scan.IsEnabled=true;}
    public void DemoSelect(){if(candidates.Count>0)candidates[0].Item1.IsChecked=true;apply.IsEnabled=true;}
    public void DemoSelectAll(){status.Text="已选择 5 个应用";logs.IsChecked=true;foreach(var row in candidates)row.Item1.IsChecked=true;apply.IsEnabled=true;}
    public void DemoCommit(){dock.ApplyRecommendations(candidates.Where(p=>p.Item1.IsChecked==true).Select(p=>p.Item2).ToArray());Close();}
}
public sealed partial class CareWindow {
    public void DemoLoading(){IsHitTestVisible=false;spinner.IsRunning=true;status.Text="正在扫描大文件… · 演示配置";}
    public void DemoScanDone(){spinner.IsRunning=false;status.Text="扫描完成 · 选择需要清理的文件";}
    public void DemoCandidates(){int index=0;foreach(var item in new[]{
        new CareItem{Path=@"C:\Demo\Downloads\old-archive.zip",Bytes=1830L*1048576,Kind="demo",Reason="大文件，内容未判断"},
        new CareItem{Path=@"C:\Demo\Videos\screen-recording.mp4",Bytes=840L*1048576,Kind="demo",Reason="大文件，内容未判断"},
        new CareItem{Path=@"C:\Demo\Documents\project-assets.zip",Bytes=320L*1048576,Kind="demo",Reason="大文件，内容未判断"}
    }){var entry=item;int delay=index++*18;dock.DemoScheduleRow(delay,()=>{var cb=new CheckBox{Content=new TextBlock{Text=entry.Label,Foreground=Foreground,TextWrapping=TextWrapping.Wrap},Margin=new Thickness(3,12,3,12)};selections.Add(Tuple.Create(cb,entry));rows.Children.Add(cb);dock.DemoReveal(cb);status.Text="正在扫描… · 已发现 "+selections.Count+" 个大文件";});}}
    public void DemoSelect(){selections[0].Item1.IsChecked=true;UpdateCount();}
}
