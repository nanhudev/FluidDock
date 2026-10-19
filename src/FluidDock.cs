using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Diagnostics;using System.Runtime.InteropServices;using System.Threading;using System.Threading.Tasks;using System.Web.Script.Serialization;using System.Windows;using System.Windows.Controls;using System.Windows.Input;using System.Windows.Media;using System.Windows.Media.Animation;using System.Windows.Media.Imaging;using System.Windows.Interop;using System.Windows.Threading;using Forms=System.Windows.Forms;
public partial class Entry {public string Name{get;set;}public string Target{get;set;}public string Arguments{get;set;}public string WorkDir{get;set;}public string Icon{get;set;}public string Group{get;set;}public bool Pinned{get;set;}public bool Hidden{get;set;}public string[] ProcessNames{get;set;}}
public class Use {public int Clicks{get;set;}public double Seconds{get;set;}}
public partial class Prefs {public bool AutoHide{get;set;}public bool ReducedMotion{get;set;}public bool Adaptive{get;set;}public bool Widget{get;set;}public Prefs(){Adaptive=true;Widget=true;ReducedMotion=!SystemParameters.ClientAreaAnimation;DockScale=1;Appearance="glass";GroupOrder=new[]{"系统","AI","办公","开发","音乐","游戏","常用网站","其他工具"};}}
public static partial class Native {
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint id);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h,StringBuilder text,int max);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
 [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll")] public static extern bool GetLastInputInfo(ref INPUT data);
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)] public static extern uint ExtractIconEx(string p,int n,IntPtr[] big,IntPtr[] small,uint count);
 [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
 [DllImport("user32.dll")] public static extern int SetWindowCompositionAttribute(IntPtr h,ref COMPOSITION data);
 [DllImport("dwmapi.dll")] public static extern int DwmExtendFrameIntoClientArea(IntPtr h,ref MARGINS m);
 [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
 [DllImport("gdi32.dll")] public static extern IntPtr CreateRoundRectRgn(int a,int b,int c,int d,int rx,int ry);
 [DllImport("user32.dll")] public static extern int SetWindowRgn(IntPtr h,IntPtr region,bool redraw);
 [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
 public struct RECT {public int L,T,R,B;}public struct INPUT {public uint cbSize,dwTime;}
 struct ACCENT {public int State,Flags;public uint Color;public int Animation;}
 public struct COMPOSITION {public int Attribute;public IntPtr Data;public int Size;}
 public struct MARGINS {public int L,R,T,B;}
 public static bool Glass(Window w){var h=new WindowInteropHelper(w).EnsureHandle();var src=HwndSource.FromHwnd(h);if(src!=null)src.CompositionTarget.BackgroundColor=Colors.Transparent;var margin=new MARGINS{L=-1};DwmExtendFrameIntoClientArea(h,ref margin);
 var accent=new ACCENT{State=3,Flags=0,Color=0,Animation=0};int size=Marshal.SizeOf(accent);IntPtr mem=Marshal.AllocHGlobal(size);bool ok=false;
 try{Marshal.StructureToPtr(accent,mem,false);var c=new COMPOSITION{Attribute=19,Data=mem,Size=size};ok=SetWindowCompositionAttribute(h,ref c)!=0;}finally{Marshal.FreeHGlobal(mem);}int corner=1;DwmSetWindowAttribute(h,33,ref corner,4);int dark=1;DwmSetWindowAttribute(h,20,ref dark,4);int noBorder=unchecked((int)0xFFFFFFFE);DwmSetWindowAttribute(h,34,ref noBorder,4);Round(w);return ok;}
 static Dictionary<IntPtr,long> roundedSizes=new Dictionary<IntPtr,long>();
 public static void Round(Window w){var h=new WindowInteropHelper(w).Handle;if(h==IntPtr.Zero||w.Width<=0||w.Height<=0)return;var src=PresentationSource.FromVisual(w);double scale=src==null?1:src.CompositionTarget.TransformToDevice.M11;int width=(int)(w.Width*scale)+1,height=(int)(w.Height*scale)+1;long key=((long)width<<32)|(uint)height;long old;if(roundedSizes.TryGetValue(h,out old)&&old==key)return;roundedSizes[h]=key;var region=CreateRoundRectRgn(0,0,width,height,(int)(28*scale),(int)(28*scale));if(SetWindowRgn(h,region,true)==0)DeleteObject(region);}
}
public class GlassWindow:Window {
 public bool GlassEnabled;
 public GlassWindow(){WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=true;ShowActivated=false;SourceInitialized+=(s,e)=>GlassEnabled=Native.Glass(this);SizeChanged+=(s,e)=>Native.Round(this);}
}
public partial class Dock:Window {
 public static string Home=ProductData.Location();public static JavaScriptSerializer Json=new JavaScriptSerializer();
 public List<Entry> Entries;public Dictionary<string,Use> Usage=new Dictionary<string,Use>();public Prefs Settings;
 Dictionary<string,ImageSource> icons=new Dictionary<string,ImageSource>();object iconLock=new object();Canvas canvas;Border rim,caption,dropBadge;TextBlock captionText;GlassWindow glass;SearchPanel search;ClockWidget widget;Forms.NotifyIcon tray;
 DispatcherTimer monitor=new DispatcherTimer();Stopwatch time=Stopwatch.StartNew();double last,hide,hideV,hideTarget,baseWidth;bool rendering,dirty,closing;int ticks,dragIndex=-1;uint lastPid;Entry activeEntry;DateTime lastUsage=DateTime.UtcNow;bool resolving;
 public bool HotkeyRegistered;public double LastSearchMs;List<Tile> tiles=new List<Tile>();List<Border> dividers=new List<Border>();long surfaceWrites;bool measureHover;List<double> hoverFrameCosts=new List<double>(256),hoverFrameGaps=new List<double>(256);Stopwatch renderCostWatch=new Stopwatch();
 class Tile {public Entry App;public Button Button;public ScaleTransform Scale;public TranslateTransform Move;public double ScaleNow=1,ScaleV,Target=1,Center,Baseline,Lift,LiftV,Bounce,OffsetX,OffsetV,InitialX=Double.NaN;public Point Down;public bool Pressed;}
 [STAThread]public static void Main(string[] args){if(args.Contains("--record-demo")&&!args.Contains("--data-dir"))return;if(args.Contains("--self-test")){Environment.Exit(ProductTests.Run(Home));return;}bool first;using(var mutex=new Mutex(true,"Local\\FluidDock.Desktop.v1"+ProductData.InstanceSuffix(),out first)){if(!first){try{using(var signal=EventWaitHandle.OpenExisting("Local\\FluidDock.Toggle"+ProductData.InstanceSuffix()))signal.Set();}catch{}return;}try{var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};Theme(app);app.Run(new Dock(args));}catch(Exception ex){File.AppendAllText(Path.Combine(Home,"error.log"),DateTime.Now+" "+ex+"\r\n");Environment.ExitCode=1;}}}
 public Dock(string[] args){
 ProductData.Ensure(Home);Entries=Json.Deserialize<List<Entry>>(File.ReadAllText(Path.Combine(Home,"apps.json")));
 try{Settings=Json.Deserialize<Prefs>(File.ReadAllText(Path.Combine(Home,"preferences.json")));}catch{Settings=new Prefs();}
 try{Usage=Json.Deserialize<Dictionary<string,Use>>(File.ReadAllText(Path.Combine(Home,"usage.json")));}catch{}
 MigrateCategories();if(Settings.Adaptive)SortSmart(false);
 Title="DockBot";Width=SystemParameters.WorkArea.Width-16;Height=156;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=true;ShowActivated=false;
 glass=new GlassWindow{Title="DockBot · Glass",Height=78,Width=900};glass.Show();Owner=glass;
 canvas=new Canvas();Content=canvas;rim=new Border{Height=78,CornerRadius=new CornerRadius(25),BorderThickness=new Thickness(0),BorderBrush=Brushes.Transparent,Background=new LinearGradientBrush(Color.FromArgb(34,24,31,43),Color.FromArgb(55,14,19,29),90)};rim.ContextMenu=Menu();canvas.Children.Add(rim);
 captionText=Text("",12,"#FFFFFF");captionText.Margin=new Thickness(12,6,12,6);caption=new Border{Child=captionText,Background=B("#E5252932"),CornerRadius=new CornerRadius(9),BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),Opacity=0,IsHitTestVisible=false};canvas.Children.Add(caption);
 var dropText=Text("＋ 释放以添加到 Dock",14,"#FFFFFF");dropText.Margin=new Thickness(22,12,22,12);dropBadge=new Border{Child=dropText,Background=B("#DC386E8C"),CornerRadius=new CornerRadius(18),Visibility=Visibility.Collapsed,IsHitTestVisible=false};canvas.Children.Add(dropBadge);
 Rebuild();AllowDrop=true;DragEnter+=(s,e)=>DropOver(e);DragOver+=(s,e)=>DropOver(e);DragLeave+=(s,e)=>{var p=e.GetPosition(this);if(p.X<0||p.X>Width||p.Y<0||p.Y>Height)dropBadge.Visibility=Visibility.Collapsed;};Drop+=OnDrop;
 MouseMove+=(s,e)=>Pointer(e.GetPosition(canvas));MouseLeave+=(s,e)=>{botTarget=18;foreach(var t in tiles)if(!t.Pressed)t.Target=1;caption.Opacity=0;Wake();};
 var handle=new WindowInteropHelper(this).EnsureHandle();HwndSource.FromHwnd(handle).AddHook(Hook);HotkeyRegistered=Native.RegisterHotKey(handle,71,0x4002,0x20);
 monitor.Interval=TimeSpan.FromSeconds(1);monitor.Tick+=(s,e)=>Monitor();monitor.Start();
 tray=new Forms.NotifyIcon{Visible=true,Text="DockBot · Ctrl + 空格",Icon=System.Drawing.Icon.ExtractAssociatedIcon(typeof(Dock).Assembly.Location)};var tm=new Forms.ContextMenuStrip();tm.Items.Add("搜索应用  Ctrl + 空格",null,(s,e)=>Dispatcher.BeginInvoke(new Action(ToggleSearch)));tm.Items.Add("一键配置",null,(s,e)=>Dispatcher.BeginInvoke(new Action(OpenSetup)));tm.Items.Add("电脑整理",null,(s,e)=>Dispatcher.BeginInvoke(new Action(OpenCare)));tm.Items.Add("退出",null,(s,e)=>Dispatcher.BeginInvoke(new Action(Close)));tray.ContextMenuStrip=tm;tray.DoubleClick+=(s,e)=>Dispatcher.BeginInvoke(new Action(ToggleSearch));
 Loaded+=(s,e)=>{Place();Wake();Persist();if(Settings.HideDesktopIcons)DesktopSurface.HideIcons();Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{search=new SearchPanel(this);search.Prepare();if(Settings.Widget){widget=new ClockWidget(this);widget.Show();}Prewarm();}));if(args.Contains("--preview")||args.Contains("--verify")){var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};timer.Tick+=(a,b)=>{timer.Stop();Verify();};timer.Start();}};
 Closed+=(s,e)=>{closing=true;monitor.Stop();if(rendering)CompositionTarget.Rendering-=Render;Native.UnregisterHotKey(handle,71);Persist();if(search!=null)search.Close();if(widget!=null)widget.Close();glass.Close();if(Settings.HideDesktopIcons)DesktopSurface.Restore();tray.Dispose();};
 SystemParameters.StaticPropertyChanged+=(s,e)=>Dispatcher.BeginInvoke(new Action(()=>{Width=SystemParameters.WorkArea.Width-16;Rebuild();Place();}));
 if(args.Contains("--profile-hover"))Loaded+=(s,e)=>{var warm=new DispatcherTimer{Interval=TimeSpan.FromSeconds(7)};warm.Tick+=(a,b)=>{warm.Stop();RunHoverProfile();};warm.Start();};InitializeProduct();
 }
 public static SolidColorBrush B(string s){var b=(SolidColorBrush)new BrushConverter().ConvertFromString(s);b.Freeze();return b;}
 static void Theme(Application app){
 var menu=new Style(typeof(ContextMenu));menu.Setters.Add(new Setter(Control.BackgroundProperty,B("#F4252C39")));menu.Setters.Add(new Setter(Control.ForegroundProperty,B("#E6EDF7")));menu.Setters.Add(new Setter(Control.BorderBrushProperty,B("#45FFFFFF")));menu.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(6)));app.Resources[typeof(ContextMenu)]=menu;
 var item=new Style(typeof(MenuItem));item.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(12,7,12,7)));item.Setters.Add(new Setter(Control.FontSizeProperty,12.0));app.Resources[typeof(MenuItem)]=item;
 }
 public static TextBlock Text(string s,double size,string color){var brush=B(color);return new TextBlock{Text=s,FontSize=size,Tag=brush,Foreground=ThemeState.Light?B("#253247"):brush,FontFamily=new FontFamily("Segoe UI"),VerticalAlignment=VerticalAlignment.Center};}
 public static void ButtonStyle(Button b,bool hoverEffects=true){
 var f=new FrameworkElementFactory(typeof(Border));f.SetValue(Border.CornerRadiusProperty,new CornerRadius(11));f.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));var cp=new FrameworkElementFactory(typeof(ContentPresenter));cp.SetValue(FrameworkElement.MarginProperty,new TemplateBindingExtension(Control.PaddingProperty));cp.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);cp.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);f.AppendChild(cp);b.Template=new ControlTemplate(typeof(Button)){VisualTree=f};b.BorderThickness=new Thickness(0);b.Foreground=ThemeState.Light?B("#253247"):B("#E8EDF6");b.Cursor=Cursors.Hand;b.FocusVisualStyle=null;
 if(!hoverEffects){b.Background=Brushes.Transparent;return;}var hover=new SolidColorBrush(Colors.Transparent);b.Background=hover;b.MouseEnter+=(s,e)=>hover.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(Color.FromArgb(30,255,255,255),TimeSpan.FromMilliseconds(100)));b.MouseLeave+=(s,e)=>hover.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(Colors.Transparent,TimeSpan.FromMilliseconds(180)));
 }
 void Place(){var a=SystemParameters.WorkArea;Left=a.Left+8;Top=a.Bottom-Height-10;LayoutDock();}
 bool Utility(Entry e){return e.Name=="此电脑"||e.Name=="回收站"||e.Name=="桌面收纳"||e.Name=="文件";}
 int GroupOrder(string s){int index=Array.IndexOf(Settings.GroupOrder??new[]{"系统","AI","办公","开发","音乐","游戏","常用网站","其他工具"},s);return index<0?8:index;}
 public void Rebuild(){
 var oldTiles=tiles.GroupBy(t=>AppCatalog.Key(t.App)).ToDictionary(g=>g.Key,g=>g.First());foreach(var t in tiles)canvas.Children.Remove(t.Button);tiles.Clear();foreach(var d in dividers)canvas.Children.Remove(d);dividers.Clear();
 var normal=Entries.Where(e=>e.Pinned&&!e.Hidden&&!Utility(e)).ToList();UpdateOnboarding(normal.Count==0);if(normal.Count>0){normal.Add(new Entry{Name="添加软件",Target="fluid://add",Group="其他工具"});normal.Add(new Entry{Name="搜索",Target="fluid://search",Group="其他工具"});}normal.Add(new Entry{Name="DockBot",Target="fluid://assistant",Group="助手"});var apps=Entries.Where(Utility).OrderBy(e=>e.Name=="此电脑"?0:e.Name=="桌面收纳"?1:2).Concat(normal.OrderBy(e=>GroupOrder(e.Group))).ToList();
 int capacity=Math.Max(8,(int)((Width/Settings.DockScale-140)/36));if(apps.Count>capacity){var special=apps.Where(a=>a.Target.StartsWith("fluid://")).ToList();apps=apps.Where(a=>!a.Target.StartsWith("fluid://")).Take(capacity-special.Count).Concat(special).ToList();}double cell=Math.Max(24,Math.Min(58,(Width/Settings.DockScale-180)/Math.Max(1,apps.Count)));double x=0;string group="";
 foreach(var e in apps){
 if(group!=""&&e.Group!=group){x+=17;var d=new Border{Width=1,Height=29,Background=B("#35FFFFFF"),IsHitTestVisible=false,RenderTransform=new TranslateTransform()};Canvas.SetLeft(d,0);Canvas.SetTop(d,91);canvas.Children.Add(d);dividers.Add(d);}group=e.Group;
 var t=new Tile{App=e,Scale=new ScaleTransform(1,1),Move=new TranslateTransform()};Tile previous;if(oldTiles.TryGetValue(AppCatalog.Key(e),out previous)){t.InitialX=previous.Move.X;t.ScaleNow=previous.ScaleNow;t.ScaleV=previous.ScaleV;t.Target=1;t.OffsetV=previous.OffsetV;}var b=new Button{Width=cell,Height=88};ButtonStyle(b,false);var image=e.Target=="fluid://search"?(FrameworkElement)Text("⌕",30,"#EEF4FE"):e.Target=="fluid://add"?(FrameworkElement)Text("+",30,"#EEF4FE"):AppVisual(e,Math.Min(47,cell-8));if(e.Target=="fluid://assistant"){botSlide=new TranslateTransform(0,botY);image.RenderTransform=botSlide;image=new Border{Width=44,Height=40,ClipToBounds=true,Child=image};}image.IsHitTestVisible=false;image.CacheMode=new BitmapCache{RenderAtScale=1.5,SnapsToDevicePixels=true};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.Linear);b.Content=image;
 var tg=new TransformGroup();tg.Children.Add(t.Scale);tg.Children.Add(t.Move);b.RenderTransform=tg;b.RenderTransformOrigin=new Point(.5,.64);b.ContextMenu=e.Target.StartsWith("fluid://")?Menu():EntryMenu(e);System.Windows.Automation.AutomationProperties.SetName(b,e.Name);
 t.Baseline=x+cell/2;t.Button=b;Canvas.SetLeft(b,0);Canvas.SetTop(b,52);
 b.PreviewMouseLeftButtonDown+=(s,a)=>{t.Down=a.GetPosition(canvas);t.Pressed=true;t.Target=.90;Wake();};b.PreviewMouseLeftButtonUp+=(s,a)=>{t.Pressed=false;t.Target=1.35;Wake();};
 b.MouseMove+=(s,a)=>{if(t.Pressed&&a.LeftButton==MouseButtonState.Pressed&&(a.GetPosition(canvas)-t.Down).Length>7){t.Pressed=false;BeginDockDrag(e,b,t);a.Handled=true;}};
 b.Click+=(s,a)=>{t.Bounce=-9;Wake();Launch(e);};b.GotKeyboardFocus+=(s,a)=>{t.Target=1.25;Wake();};b.LostKeyboardFocus+=(s,a)=>{t.Target=1;Wake();};
 tiles.Add(t);canvas.Children.Add(b);x+=cell;
 }
baseWidth=x;if(plateWidth<=0)plateWidth=baseWidth;LayoutDock();if(search!=null)search.InvalidateCatalog();Wake();
 }
 void LayoutDock(){
 if(tiles.Count==0)return;double cell=tiles[0].Button.Width,total=baseWidth;foreach(var tile in tiles)total+=Math.Max(0,tile.ScaleNow-1)*cell*.72;double left=(Width-total)/2,shift=0;string group="";int divider=0;
 foreach(var t in tiles){double extra=Math.Max(0,t.ScaleNow-1)*cell*.72;t.Center=left+t.Baseline+shift+extra/2;t.Scale.ScaleX=t.Scale.ScaleY=t.ScaleNow;double destination=t.Center-t.Button.Width/2;if(!Double.IsNaN(t.InitialX)){t.OffsetX=t.InitialX-destination;t.InitialX=Double.NaN;}t.Move.X=destination+t.OffsetX;t.Move.Y=hide+t.Lift+t.Bounce;shift+=extra;
 if(group!=""&&t.App.Group!=group&&divider<dividers.Count){var d=dividers[divider++];var move=(TranslateTransform)d.RenderTransform;move.X=t.Center-t.Button.Width/2-9;move.Y=hide;}group=t.App.Group;}
 double visibleWidth=plateWidth>0?plateWidth:baseWidth;double plateLeft=(Width-visibleWidth)/2-7;rim.Width=visibleWidth+14;if(onboarding!=null){Canvas.SetLeft(onboarding,(Width-112)/2);Canvas.SetTop(onboarding,20+hide);}Canvas.SetLeft(rim,plateLeft);Canvas.SetTop(rim,68+hide);
 double nativeWidth=(visibleWidth+14)*Settings.DockScale,nativeLeft=Left+(Width-nativeWidth)/2,nativeTop=Top+Height-(156-68-hide)*Settings.DockScale;glass.Height=78*Settings.DockScale;if(Math.Abs(glass.Width-nativeWidth)>.5){glass.Width=nativeWidth;surfaceWrites++;}if(Math.Abs(glass.Left-nativeLeft)>.25){glass.Left=nativeLeft;surfaceWrites++;}if(Math.Abs(glass.Top-nativeTop)>.25){glass.Top=nativeTop;surfaceWrites++;}glass.Visibility=hide>100?Visibility.Hidden:Visibility.Visible;Canvas.SetLeft(dropBadge,(Width-250)/2);Canvas.SetTop(dropBadge,6);
 }
 void Pointer(Point p){
 if(dragIndex>=0)return;botTarget=0;double origin=(Width-baseWidth)/2;Tile nearest=null;double nd=9999;
 foreach(var t in tiles){double d=Math.Abs(p.X-(origin+t.Baseline));if(!t.Pressed)t.Target=t.App.Target=="fluid://assistant"?1:p.Y<40?1:1+.40*Math.Exp(-d*d/(2*52*52));if(d<nd){nd=d;nearest=t;}}
 if(nearest!=null&&nd<44&&p.Y>35){string label=nearest.App.Name+"  ·  "+nearest.App.Group;if(captionText.Text!=label){captionText.Text=label;caption.Measure(new Size(500,100));}Canvas.SetLeft(caption,nearest.Center-caption.DesiredSize.Width/2);Canvas.SetTop(caption,14);caption.Opacity=1;}else caption.Opacity=0;Wake();
 }
 void Wake(){if(!rendering){rendering=true;last=time.Elapsed.TotalSeconds;CompositionTarget.Rendering+=Render;}}
 void Render(object sender,EventArgs e){
 double now=time.Elapsed.TotalSeconds,rawGap=now-last,dt=Math.Min(.035,Math.Max(.001,rawGap));last=now;bool moving=false;if(measureHover){renderCostWatch.Restart();if(rawGap>.001)hoverFrameGaps.Add(rawGap*1000);}
 foreach(var t in tiles){double liftTarget=-Math.Max(0,t.Target-1)*32;
 if(Settings.ReducedMotion){t.ScaleNow=t.Target>1?1.08:t.Target;t.Lift=0;t.ScaleV=0;t.Bounce=0;}
 else for(int i=0;i<4;i++){double d=dt/4;t.ScaleV+=(680*(t.Target-t.ScaleNow)-43*t.ScaleV)*d;t.ScaleNow+=t.ScaleV*d;t.LiftV+=(600*(liftTarget-t.Lift)-43*t.LiftV)*d;t.Lift+=t.LiftV*d;}
 for(int i=0;i<4;i++){t.OffsetV+=(-600*t.OffsetX-43*t.OffsetV)*dt/4;t.OffsetX+=t.OffsetV*dt/4;}if(Math.Abs(t.OffsetX)>.08||Math.Abs(t.OffsetV)>.1)moving=true;t.Bounce*=Math.Exp(-dt*15);if(!Settings.ReducedMotion&&(Math.Abs(t.Target-t.ScaleNow)>.001||Math.Abs(t.ScaleV)>.005||Math.Abs(liftTarget-t.Lift)>.04||Math.Abs(t.Bounce)>.05))moving=true;}
 if(AnimateBot(dt))moving=true;if(StepPlate(dt))moving=true;for(int i=0;i<4;i++){hideV+=(360*(hideTarget-hide)-38*hideV)*dt/4;hide+=hideV*dt/4;}if(Math.Abs(hideTarget-hide)>.1||Math.Abs(hideV)>.1)moving=true;LayoutDock();
 if(measureHover)hoverFrameCosts.Add(renderCostWatch.Elapsed.TotalMilliseconds);if(!moving){CompositionTarget.Rendering-=Render;rendering=false;}
 }
 IntPtr Hook(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled){if(msg==0x312&&w.ToInt32()==71){ToggleSearch();handled=true;}return IntPtr.Zero;}
 public void ToggleSearch(){if(search==null){search=new SearchPanel(this);search.Prepare();}if(search.IsVisible)search.Dismiss();else search.Reveal();}
 public void OpenCategory(string category){if(search==null){search=new SearchPanel(this);search.Prepare();}search.SetMode("library");search.Category=category;search.Reveal();}
 public FrameworkElement AppVisual(Entry e,double size){
 if(e.Target=="fluid://assistant")return new BuddyFace{Width=size,Height=size};
 var grid=new Grid{Width=size,Height=size};var image=new Image{Width=size,Height=size,Stretch=Stretch.Uniform,SnapsToDevicePixels=true};
 var fallback=new Border{CornerRadius=new CornerRadius(12),Background=B("#4E586579"),BorderBrush=B("#35FFFFFF"),BorderThickness=new Thickness(1)};
 var name=Text(e.Name=="回收站"?"♲":e.Name=="此电脑"?"▣":e.Name=="桌面收纳"?"▤":e.Name.Substring(0,Math.Min(2,e.Name.Length)),size*.28,"#E2EDF9");name.HorizontalAlignment=HorizontalAlignment.Center;fallback.Child=name;grid.Children.Add(fallback);grid.Children.Add(image);
 ImageSource found=null;lock(iconLock)icons.TryGetValue(e.Name,out found);
 if(found!=null){image.Source=found;fallback.Visibility=Visibility.Collapsed;}else Task.Run(()=>{var icon=ReadIcon(e);if(icon!=null&&!closing)Dispatcher.BeginInvoke(new Action(()=>{image.Source=icon;fallback.Visibility=Visibility.Collapsed;}));});return grid;
 }
 void Prewarm(){foreach(var e in Entries.Where(e=>!e.Hidden)){var entry=e;Task.Run(()=>ReadIcon(entry));}}
 ImageSource ReadIcon(Entry e){
 lock(iconLock){ImageSource existing;if(icons.TryGetValue(e.Name,out existing))return existing;}ImageSource result=null;
 try{string custom=e.Icon??"";int index=0;int comma=custom.LastIndexOf(',');if(comma>0){int.TryParse(custom.Substring(comma+1),out index);custom=custom.Substring(0,comma);}
 string cached=Path.Combine(Home,"icons",Safe(e.Name)+".png");string p=File.Exists(custom)?custom:File.Exists(cached)?cached:e.Target;
 if(File.Exists(p)&&(p.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".ico",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".jpg",StringComparison.OrdinalIgnoreCase))){var dec=BitmapDecoder.Create(new Uri(p),BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);var f=dec.Frames.OrderByDescending(a=>a.PixelWidth).First();f.Freeze();result=f;}
 else if(File.Exists(p)){IntPtr[] handles=new IntPtr[1];Native.ExtractIconEx(p,index,handles,null,1);if(handles[0]!=IntPtr.Zero)try{var f=Imaging.CreateBitmapSourceFromHIcon(handles[0],Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());f.Freeze();result=f;}finally{Native.DestroyIcon(handles[0]);}}
 }catch{}if(result!=null)lock(iconLock)icons[e.Name]=result;return result;
 }
 static string Safe(string s){foreach(char c in Path.GetInvalidFileNameChars())s=s.Replace(c,'_');return s;}
 void Monitor(){
 var h=Native.GetForegroundWindow();uint pid;Native.GetWindowThreadProcessId(h,out pid);var cls=new StringBuilder(128);Native.GetClassName(h,cls,128);bool full=false;
 if(pid!=(uint)Process.GetCurrentProcess().Id&&cls.ToString()!="Progman"&&cls.ToString()!="WorkerW"&&cls.ToString()!="Shell_TrayWnd"){Native.RECT r;if(Native.GetWindowRect(h,out r)){var b=Forms.Screen.FromHandle(h).Bounds;full=r.L<=b.Left&&r.T<=b.Top&&r.R>=b.Right&&r.B>=b.Bottom;}}
 bool near=IsMouseOver||(search!=null&&search.IsVisible)||Forms.Cursor.Position.Y>=Forms.Screen.PrimaryScreen.WorkingArea.Bottom-9;double target=full?175:Settings.AutoHide&&!near?104:0;
 if(target!=hideTarget){hideTarget=target;caption.Opacity=0;Wake();}if(widget!=null)widget.SetCovered(full);
 var now=DateTime.UtcNow;double delta=Math.Min(2,(now-lastUsage).TotalSeconds);lastUsage=now;var idle=new Native.INPUT{cbSize=(uint)Marshal.SizeOf(typeof(Native.INPUT))};Native.GetLastInputInfo(ref idle);bool inputActive=unchecked((uint)Environment.TickCount-idle.dwTime)<60000;
 if(Settings.TrackUsage&&pid==lastPid&&activeEntry!=null&&inputActive){GetUse(activeEntry).Seconds+=delta;dirty=true;}
 if(Settings.TrackUsage&&pid!=lastPid&&!resolving){lastPid=pid;activeEntry=null;resolving=true;var snapshot=Entries.ToArray();Task.Run(()=>{Entry match=null;try{var p=Process.GetProcessById((int)pid);string process=p.ProcessName;match=snapshot.FirstOrDefault(a=>!String.IsNullOrEmpty(a.Target)&&a.Group!="系统"&&!a.Target.EndsWith("powershell.exe",StringComparison.OrdinalIgnoreCase)&&!(a.Target.EndsWith("explorer.exe",StringComparison.OrdinalIgnoreCase)&&(a.ProcessNames==null||a.ProcessNames.Length==0))&&(String.Equals(Path.GetFileNameWithoutExtension(a.Target),process,StringComparison.OrdinalIgnoreCase)||(a.ProcessNames!=null&&a.ProcessNames.Any(n=>String.Equals(n,process,StringComparison.OrdinalIgnoreCase)))));if(match!=null&&(match.ProcessNames==null||match.ProcessNames.Length==0)){if(!String.Equals(p.MainModule.FileName,match.Target,StringComparison.OrdinalIgnoreCase))match=null;}}catch{}return match;}).ContinueWith(t=>Dispatcher.BeginInvoke(new Action(()=>{if(lastPid==pid)activeEntry=t.Result;resolving=false;})));}
 ticks++;if(ticks%30==0&&dirty)Persist();
 }
 public Use GetUse(Entry e){Use u;if(!Usage.TryGetValue(e.Name,out u)){u=new Use();Usage[e.Name]=u;}return u;}
 public double Score(Entry e){var u=GetUse(e);return u.Clicks*4+Math.Sqrt(u.Seconds/60)*2;}
 public void SortSmart(bool rebuild){Entries=Entries.OrderBy(e=>GroupOrder(e.Group)).ThenByDescending(e=>Utility(e)?0:Score(e)).ToList();if(rebuild){SaveApps();Rebuild();Toast("已按分类和使用习惯整理");}}
 public void SaveApps(){Atomic("apps.json",Entries);if(search!=null)search.InvalidateCatalog();}
 public void Persist(){Atomic("usage.json",Usage);Atomic("preferences.json",Settings);dirty=false;}
 void Atomic(string name,object obj){string p=Path.Combine(Home,name);File.WriteAllText(p+".tmp",Json.Serialize(obj),Encoding.UTF8);File.Copy(p+".tmp",p,true);}
 public void Launch(Entry e){
 if(e.Target=="fluid://search"){OpenManager("library");return;}if(e.Target=="fluid://assistant"){OpenManager("assistant");return;}if(e.Target=="fluid://add"){OpenManager("add");return;}if(Settings.TrackUsage){GetUse(e).Clicks++;dirty=true;}if(search!=null)search.Dismiss();DispatchLaunch(e);
 }
 public void Toast(string text){tray.BalloonTipTitle="DockBot";tray.BalloonTipText=text;tray.ShowBalloonTip(2500);}
 static void MenuAction(ContextMenu m,string title,Action action){var item=new MenuItem{Header=title};item.Click+=(s,e)=>action();m.Items.Add(item);}
 ContextMenu Menu(){
 var m=new ContextMenu();MenuAction(m,"搜索应用    Ctrl + 空格",ToggleSearch);MenuAction(m,"添加软件…",()=>OpenManager("add"));MenuAction(m,"一键配置…",OpenSetup);MenuAction(m,"电脑整理…",OpenCare);MenuAction(m,"小助手",()=>OpenManager("assistant"));MenuAction(m,"移除入口…",()=>OpenManager("remove"));MenuAction(m,"添加搜索目录…",ChooseSearchRoot);MenuAction(m,"按使用习惯智能整理",()=>SortSmart(true));MenuAction(m,"使用统计",()=>OpenCategory("统计"));MenuAction(m,"管理隐藏应用",()=>OpenCategory("隐藏"));
 var hideItem=new MenuItem{Header="自动收起",IsCheckable=true,IsChecked=Settings.AutoHide};hideItem.Click+=(s,e)=>{Settings.AutoHide=hideItem.IsChecked;Persist();};m.Items.Add(hideItem);
 var reduce=new MenuItem{Header="减少动态效果",IsCheckable=true,IsChecked=Settings.ReducedMotion};reduce.Click+=(s,e)=>{Settings.ReducedMotion=reduce.IsChecked;Persist();Wake();};m.Items.Add(reduce);MenuAction(m,"显示桌面文件",DesktopSurface.Restore);MenuAction(m,"隐藏桌面文件",DesktopSurface.HideIcons);MenuAction(m,"退出 Dock",Close);return m;
 }
 public ContextMenu EntryMenu(Entry e){
 var m=new ContextMenu();MenuAction(m,"打开 "+e.Name,()=>Launch(e));if(!Utility(e)){
 MenuAction(m,e.Pinned?"取消固定":"固定到 Dock",()=>Execute(new DockPlan{Intent=e.Pinned?"remove":"add",AppName=e.Name,TargetKey=AppCatalog.Key(e),ResolvedEntry=e}));
 var groups=new MenuItem{Header="移动到分类"};foreach(string c in new[]{"AI","办公","开发","音乐","游戏","常用网站","其他工具"}){string category=c;var item=new MenuItem{Header=c};item.Click+=(s,a)=>{e.Group=category;SaveApps();Rebuild();};groups.Items.Add(item);}m.Items.Add(groups);
 MenuAction(m,e.Hidden?"恢复显示":"隐藏此应用",()=>{e.Hidden=!e.Hidden;if(e.Hidden)e.Pinned=false;SaveApps();Rebuild();});}
 MenuAction(m,"重新绑定程序…",()=>Pick(e));MenuAction(m,"查看启动位置",()=>{if(File.Exists(e.Target))Process.Start("explorer.exe","/select,\""+e.Target+"\"");else Toast(e.Target);});return m;
 }
 void DropOver(DragEventArgs e){
 if(e.Data.GetDataPresent("FluidDock.Entry")){e.Effects=DragDropEffects.Move;e.Handled=true;return;}
 if(e.Data.GetDataPresent(DataFormats.FileDrop)){e.Effects=DragDropEffects.Copy;dropBadge.Visibility=Visibility.Visible;rim.BorderBrush=B("#B39BE4FF");e.Handled=true;}
 }
 void OnDrop(object sender,DragEventArgs e){
 dropBadge.Visibility=Visibility.Collapsed;rim.BorderBrush=Brushes.Transparent;
 if(e.Data.GetDataPresent("FluidDock.Entry")){
 string name=e.Data.GetData("FluidDock.Entry") as string;var item=Entries.FirstOrDefault(a=>a.Name==name);var p=e.GetPosition(canvas);var dest=tiles.OrderBy(t=>Math.Abs(t.Center-p.X)).FirstOrDefault();
 if(item!=null&&dest!=null&&!Utility(item)&&!Utility(dest.App)&&dest.App.Target!="fluid://search"){Entries.Remove(item);int index=Entries.IndexOf(dest.App);item.Group=dest.App.Group;Entries.Insert(Math.Max(0,index),item);SaveApps();Rebuild();}return;}
 var files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files!=null)foreach(var p in files)AddPath(p);e.Handled=true;
 }
 public void Pick(){Pick(null);}
 public void Pick(Entry replace){
 if(search!=null)search.Hold=true;
 try{var dialog=new Microsoft.Win32.OpenFileDialog{Filter="程序与快捷方式|*.exe;*.lnk;*.url;*.cmd;*.bat|所有文件|*.*",Multiselect=replace==null};if(dialog.ShowDialog()==true){if(replace!=null){Resolve(replace,dialog.FileName);lock(iconLock)icons.Remove(replace.Name);SaveApps();Rebuild();}else foreach(string p in dialog.FileNames)AddPath(p);}}
 finally{if(search!=null)search.Hold=false;}
 }
 public void AddPath(string p){
 try{var candidate=AppCatalog.FromFile(p);installed.Insert(0,candidate);string message=Execute(new DockPlan{Intent="add",AppName=candidate.Name,TargetKey=AppCatalog.Key(candidate),ResolvedEntry=candidate});Toast(message);}catch(Exception ex){Toast(ex.Message);}
 }
 void Resolve(Entry e,string p){
 var candidate=AppCatalog.FromFile(p);e.Target=candidate.Target;e.Arguments=candidate.Arguments;e.WorkDir=candidate.WorkDir;e.Icon=candidate.Icon;e.SourceShortcut=candidate.SourceShortcut;e.Fingerprint=EntryBindings.Fingerprint(candidate.Target);e.BindingStatus="ready";
 }
 static double Percentile(List<double> values,double p){if(values.Count==0)return 0;var sorted=values.OrderBy(x=>x).ToArray();return sorted[Math.Min(sorted.Length-1,(int)Math.Ceiling(p*sorted.Length)-1)];}
 void RunHoverProfile(){
 hoverFrameCosts.Clear();hoverFrameGaps.Clear();long initialWrites=surfaceWrites;measureHover=true;int phase=0;
 Pointer(new Point((Width-baseWidth)/2+tiles[0].Baseline,100));
 var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(190)};
 timer.Tick+=(s,e)=>{if(phase==0){Pointer(new Point((Width-baseWidth)/2+tiles[tiles.Count/2].Baseline,100));phase++;}
 else if(phase==1){Pointer(new Point((Width-baseWidth)/2+tiles[1].Baseline,100));phase++;}
 else if(phase==2){foreach(var tile in tiles)tile.Target=1;caption.Opacity=0;Wake();phase++;timer.Interval=TimeSpan.FromMilliseconds(1100);}
 else{timer.Stop();measureHover=false;Atomic("hover-profile.json",new{sampledAnimationFrames=hoverFrameCosts.Count,uiUpdateP95Ms=Percentile(hoverFrameCosts,.95),uiUpdateMaxMs=hoverFrameCosts.Count==0?0:hoverFrameCosts.Max(),frameGapP95Ms=Percentile(hoverFrameGaps,.95),nativeSurfaceWritesDuringHover=surfaceWrites-initialWrites,settled=tiles.All(t=>Math.Abs(t.ScaleNow-1)<.002),animationDriverSleeping=!rendering,hoverUsesCachedIcons=tiles.All(t=>t.Button.Content is FrameworkElement&&((FrameworkElement)t.Button.Content).CacheMode is BitmapCache)});Capture(this,"dock-hover-optimized.png");}};timer.Start();
 }
 void Verify(){
 var a=tiles.First();a.Target=1.4;Wake();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};int phase=0;double peak=1;
 timer.Tick+=(s,e)=>{if(phase==0){peak=a.ScaleNow;Capture(this,"dock-v2-hover.png");a.Target=.94;phase++;timer.Interval=TimeSpan.FromMilliseconds(80);}
 else if(phase==1){a.Target=1;phase++;timer.Interval=TimeSpan.FromMilliseconds(1200);}
 else{timer.Stop();if(search==null){search=new SearchPanel(this);search.Prepare();}bool searchPassed=search.CheckSearch();search.Reveal();double firstSearch=LastSearchMs;search.Dismiss();search.Reveal();double warmSearch=LastSearchMs;var preview=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(350)};preview.Tick+=(x,y)=>{preview.Stop();Capture(search,"search-v2.png");search.Dismiss();};preview.Start();Atomic("runtime-v2.json",new{desktopIconsHidden=DesktopSurface.IsHidden,widgetVisible=widget!=null&&widget.IsVisible,glassApiEnabled=glass.GlassEnabled,hotkeyRegistered=HotkeyRegistered,mainWindowVisible=Native.IsWindowVisible(new WindowInteropHelper(this).Handle),springReversalPassed=peak>1.1&&Math.Abs(a.ScaleNow-1)<.005,hoverScale=peak,settledScale=a.ScaleNow,renderingSleepsWhenIdle=!rendering,searchFirstOpenMs=firstSearch,searchWarmOpenMs=warmSearch,searchMatchesPassed=searchPassed,apps=Entries.Count,pinned=Entries.Count(x=>x.Pinned&&!x.Hidden),hidden=Entries.Count(x=>x.Hidden)});Capture(this,"dock-v2.png");}};
 timer.Start();
 }
 public static void Capture(Window w,string file){w.UpdateLayout();var bmp=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(w);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(Path.Combine(Home,file)))png.Save(f);}
}
public partial class SearchPanel:GlassWindow {
 Dictionary<Entry,ListBoxItem> rowCache=new Dictionary<Entry,ListBoxItem>();
 Dock dock;TextBox input;ListBox results;TextBlock empty,title,foot;Border surface;StackPanel tabs;bool stale=true,showing;List<Entry> current=new List<Entry>();double morph,morphV,morphTarget,morphLast;bool morphTick;Stopwatch morphClock=Stopwatch.StartNew();ScaleTransform morphScale=new ScaleTransform(1,1);TranslateTransform morphMove=new TranslateTransform();public string Category="全部";public bool Hold;
 public SearchPanel(Dock owner){
 dock=owner;Owner=owner;Title="DockBot · 搜索";Width=720;Height=570;
 surface=new Border{CornerRadius=new CornerRadius(26),Background=Dock.B("#48141C28"),BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(24,22,24,16)};Content=surface;
 var root=new DockPanel();surface.Child=root;var top=new DockPanel{Margin=new Thickness(1,0,0,18)};DockPanel.SetDock(top,System.Windows.Controls.Dock.Top);root.Children.Add(top);
 var badge=Dock.Text(dock.ShortcutLabel.ToUpperInvariant(),10,"#A6B6C9");DockPanel.SetDock(badge,System.Windows.Controls.Dock.Right);top.Children.Add(badge);title=Dock.Text("随时，触手可及。",18,"#F1F4FA");title.FontWeight=FontWeights.SemiBold;top.Children.Add(title);
 var box=new Border{Background=Dock.B("#35101824"),BorderBrush=Dock.B("#12C4D4EB"),BorderThickness=new Thickness(.5),CornerRadius=new CornerRadius(16),Padding=new Thickness(16,12,16,12),Margin=new Thickness(0,0,0,18)};DockPanel.SetDock(box,System.Windows.Controls.Dock.Top);root.Children.Add(box);
 var sg=new DockPanel();box.Child=sg;var glyph=Dock.Text("⌕",28,"#CBD9E9");glyph.Margin=new Thickness(0,0,13,0);sg.Children.Add(glyph);
 var ig=new Grid();input=new TextBox{Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=Brushes.White,CaretBrush=Brushes.White,FontSize=23,VerticalContentAlignment=VerticalAlignment.Center,Padding=new Thickness(0)};ig.Children.Add(input);var hint=Dock.Text("搜索应用，直接出发…",21,"#8E9FB5");hint.IsHitTestVisible=false;ig.Children.Add(hint);sg.Children.Add(ig);inputHint=hint;
 input.TextChanged+=(s,e)=>{hint.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;Refresh();};
 tabs=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(tabs,System.Windows.Controls.Dock.Top);root.Children.Add(tabs);
 foreach(string c in new[]{"全部","AI","办公","开发","音乐","游戏","常用网站","其他工具","文件","统计"}){string cat=c;var b=new Button{Content=c,FontSize=12,Padding=new Thickness(9,7,9,7),Margin=new Thickness(0,0,3,0)};Dock.ButtonStyle(b);b.Click+=(s,e)=>{if(Mode=="assistant")Mode="library";Category=cat;Refresh();};b.Tag=c;tabs.Children.Add(b);}
 var bottom=new DockPanel{Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(bottom,System.Windows.Controls.Dock.Bottom);root.Children.Add(bottom);var add=new Button{Content="＋ 添加应用",Padding=new Thickness(12,7,12,7),FontSize=11};Dock.ButtonStyle(add);DockPanel.SetDock(add,System.Windows.Controls.Dock.Right);bottom.Children.Add(add);add.Click+=(s,e)=>SetMode("add");foot=Dock.Text("↵ 打开 · esc 收起",11,"#98AAC0");bottom.Children.Add(foot);
 results=new ListBox{Background=Brushes.Transparent,BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch};
 var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(0)));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));style.Setters.Add(new Setter(Control.FocusVisualStyleProperty,null));
 var f=new FrameworkElementFactory(typeof(Border));f.SetValue(Border.CornerRadiusProperty,new CornerRadius(13));f.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));var cp=new FrameworkElementFactory(typeof(ContentPresenter));f.AppendChild(cp);var tpl=new ControlTemplate(typeof(ListBoxItem)){VisualTree=f};
 var selected=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};selected.Setters.Add(new Setter(Control.BackgroundProperty,Dock.B("#294FA9D5")));tpl.Triggers.Add(selected);var over=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};over.Setters.Add(new Setter(Control.BackgroundProperty,Dock.B("#18FFFFFF")));tpl.Triggers.Add(over);style.Setters.Add(new Setter(Control.TemplateProperty,tpl));results.ItemContainerStyle=style;
 ScrollViewer.SetHorizontalScrollBarVisibility(results,ScrollBarVisibility.Disabled);ScrollViewer.SetVerticalScrollBarVisibility(results,ScrollBarVisibility.Auto);
 string scrollTheme="<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'><Setter Property='Width' Value='5'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'><Track x:Name='PART_Track' Orientation='Vertical' IsDirectionReversed='True' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' ViewportSize='{TemplateBinding ViewportSize}'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}'><RepeatButton.Template><ControlTemplate><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb MinHeight='24'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border x:Name='Grip' Background='#526E8299' CornerRadius='2' Width='4' HorizontalAlignment='Center'/><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Grip' Property='Background' Value='#91ADC3DD'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}'><RepeatButton.Template><ControlTemplate><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></ControlTemplate></Setter.Value></Setter></Style>";
 results.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)]=(Style)System.Windows.Markup.XamlReader.Parse(scrollTheme);
 var body=new Grid();body.Children.Add(results);empty=Dock.Text("没有找到应用 · 可拖入程序添加",14,"#A6B6CA");empty.HorizontalAlignment=HorizontalAlignment.Center;empty.Visibility=Visibility.Collapsed;body.Children.Add(empty);root.Children.Add(body);InitializeAssistant(body,bottom);
 results.PreviewMouseLeftButtonUp+=(s,e)=>{var node=e.OriginalSource as DependencyObject;while(node!=null&&!(node is ListBoxItem))node=VisualTreeHelper.GetParent(node);if(node!=null){var entry=((ListBoxItem)node).Tag as Entry;if(entry!=null)ActivateItem(entry);}};
 PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){Dismiss();e.Handled=true;}else if(e.Key==Key.Enter&&Mode=="assistant"){RunAssistant();e.Handled=true;}else if(e.Key==Key.Enter&&results.SelectedIndex>=0&&results.SelectedIndex<current.Count){ActivateItem(current[results.SelectedIndex]);e.Handled=true;}else if(e.Key==Key.Down||e.Key==Key.Up){results.SelectedIndex=Math.Max(0,Math.Min(current.Count-1,results.SelectedIndex+(e.Key==Key.Down?1:-1)));if(results.SelectedItem!=null)results.ScrollIntoView(results.SelectedItem);e.Handled=true;}};
 Deactivated+=(s,e)=>{if(showing&&!Hold)Dismiss();};AllowDrop=true;DragOver+=(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop)){e.Effects=DragDropEffects.Copy;e.Handled=true;}};
 Drop+=(s,e)=>{var files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files!=null)foreach(string p in files)dock.AddPath(p);Refresh();e.Handled=true;};
 }
 public void Prepare(){new WindowInteropHelper(this).EnsureHandle();Refresh();Measure(new Size(Width,Height));Arrange(new Rect(0,0,Width,Height));Opacity=0;Show();UpdateLayout();Hide();Opacity=1;}
 public void InvalidateCatalog(){rowCache.Clear();stale=true;if(IsVisible)Refresh();}
 public void Reveal(){
 var sw=Stopwatch.StartNew();var a=SystemParameters.WorkArea;Left=a.Left+(a.Width-Width)/2;Top=a.Top+Math.Max(60,(a.Height-Height)*.34);if(input.Text.Length>0)input.Text="";else if(stale||Category!="全部")Refresh();
 showing=true;Show();if(!dock.RecordingDemo){Activate();input.Focus();}
 AnimatePanel(true);
 dock.LastSearchMs=sw.Elapsed.TotalMilliseconds;
 }
 public bool CheckSearch(){Category="全部";input.Text="wx";bool wx=current.Any(x=>x.Name=="微信");input.Text="sjz";bool delta=current.Any(x=>x.Name=="三角洲");input.Text="__fluid_no_result__";bool none=current.Count==0;input.Text="";return wx&&delta&&none;}
 public void Dismiss(){showing=false;AnimatePanel(false);Hide();}
 void AnimatePanel(bool open){
  morphTarget=open?1:0;surface.RenderTransformOrigin=new Point(.5,.2);
  var tg=new TransformGroup();tg.Children.Add(morphScale);tg.Children.Add(morphMove);surface.RenderTransform=tg;
  if(dock.Settings.ReducedMotion){morph=morphTarget;morphV=0;surface.Opacity=1;morphScale.ScaleX=morphScale.ScaleY=1;morphMove.Y=0;return;}
  if(!morphTick){morphTick=true;morphLast=morphClock.Elapsed.TotalSeconds;CompositionTarget.Rendering+=MorphFrame;}
 }
 void MorphFrame(object s,EventArgs e){
  double now=morphClock.Elapsed.TotalSeconds,dt=Math.Min(.032,Math.Max(.001,now-morphLast));morphLast=now;
  for(int i=0;i<4;i++){morphV+=(720*(morphTarget-morph)-48*morphV)*dt/4;morph+=morphV*dt/4;}
  morphScale.ScaleX=morphScale.ScaleY=.985+.015*morph;morphMove.Y=5*(1-morph);surface.Opacity=Math.Max(0,Math.Min(1,.35+.65*morph));
  if(Math.Abs(morphTarget-morph)<.001&&Math.Abs(morphV)<.006){CompositionTarget.Rendering-=MorphFrame;morphTick=false;}
 }
 void Refresh(){
 stale=false;if(results==null)return;if(Mode=="assistant"){UpdateAssistant();return;}RequestFiles(input.Text.Trim());if(assistantRoot!=null)assistantRoot.Visibility=Visibility.Collapsed;results.Visibility=Visibility.Visible;results.Items.Clear();string q=input.Text.Trim().ToLowerInvariant();bool stats=Category=="统计",hidden=Category=="隐藏";
 var list=(Mode=="remove"?dock.Entries.Where(e=>e.Pinned&&e.Group!="系统"):dock.AllApps().Concat(Mode=="library"?fileResults:new List<Entry>()).GroupBy(AppCatalog.Key).Select(g=>g.First())).Where(e=>hidden?e.Hidden:!e.Hidden).Where(e=>Category=="全部"||stats||hidden||e.Group==Category);if(q.Length>0){var candidates=list.ToArray();var hits=SoftwareResolver.Resolve(input.Text,candidates);string nq=IntentModel.Normalize(q);list=candidates.Where(e=>SoftwareResolver.Aliases(e).Any(a=>IntentModel.Normalize(a).Contains(nq))||hits.Matches.Any(h=>SoftwareResolver.Canonical(h)==SoftwareResolver.Canonical(e)));}list=list.OrderByDescending(e=>dock.Score(e));current=list.Take(q.Length==0?40:80).ToList();int clicks=Math.Max(1,dock.Usage.Values.Sum(u=>u.Clicks));
 foreach(var entry in current){
 ListBoxItem cached;if(!stats&&rowCache.TryGetValue(entry,out cached)){results.Items.Add(cached);continue;}
 var row=new Grid{Height=59,Margin=new Thickness(2,2,2,2)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(52)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(94)});
 var icon=dock.AppVisual(entry,36);Grid.SetColumn(icon,0);row.Children.Add(icon);var label=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4,0,0,0)};label.Children.Add(Dock.Text(entry.Name,14,"#F4F6FC"));var u=dock.GetUse(entry);string sub=stats?String.Format("{0} 次点击 · {1:0}% · 活跃 {2:0.#} 分钟",u.Clicks,u.Clicks*100.0/clicks,u.Seconds/60):Mode=="add"?entry.Group+" · "+entry.Target:entry.BindingStatus=="missing"?"入口失效 · 右键重新定位":entry.Group;label.Children.Add(Dock.Text(sub,10,"#94A6BF"));Grid.SetColumn(label,1);row.Children.Add(label);
 var right=Dock.Text(hidden?"已隐藏":Mode=="remove"?"移出 Dock":Mode=="add"?(entry.Pinned?"已添加":"添加  +"):entry.Pinned?"已固定  ↵":"打开  ↵",10,"#889CB7");right.HorizontalAlignment=HorizontalAlignment.Right;right.Margin=new Thickness(0,0,14,0);Grid.SetColumn(right,2);row.Children.Add(right);var item=new ListBoxItem{Content=row,Tag=entry,ContextMenu=dock.EntryMenu(entry)};if(!stats)rowCache[entry]=item;results.Items.Add(item);
 }
 if(results.Items.Count>0)results.SelectedIndex=0;empty.Visibility=current.Count==0?Visibility.Visible:Visibility.Collapsed;title.Text=Mode=="add"?"添加软件":Mode=="remove"?"管理 Dock 入口":stats?"你的使用习惯":hidden?"隐藏应用管理":"随时，触手可及。";foot.Text=stats?"统计仅存本机 · 闲置超过 60 秒暂停计时":"↵ 打开 · esc 收起";foreach(Button b in tabs.Children){b.FontWeight=(string)b.Tag==Category?FontWeights.SemiBold:FontWeights.Normal;b.Opacity=(string)b.Tag==Category?1:.65;}
 }
 static string Alias(string n){switch(n){case "GPT":return "chatgpt openai";case "微信":return "weixin wechat wx";case "三角洲":return "delta sjz";case "网易云音乐":return "music wyy";case "此电脑":return "computer explorer pc";case "回收站":return "trash recycle";case "腾讯会议":return "meeting txhy";case "无畏契约":return "valorant wwqy";case "浏览器":return "edge browser llq";default:return "";}}
}
public class ClockWidget:GlassWindow {
 Dock dock;TextBlock clock,date;DispatcherTimer timer=new DispatcherTimer();
 public ClockWidget(Dock owner){dock=owner;Title="DockBot · 桌面时钟";Width=252;Height=158;Topmost=false;var area=SystemParameters.WorkArea;Left=area.Right-Width-34;Top=area.Top+38;
 var frame=new Border{CornerRadius=new CornerRadius(24),BorderThickness=new Thickness(0),BorderBrush=Brushes.Transparent,Background=Dock.B("#35141C28"),Padding=new Thickness(22,18,22,16)};Content=frame;var stack=new StackPanel();frame.Child=stack;stack.Children.Add(Dock.Text("D O C K B O T    /    TODAY",9,"#A2B8CF"));clock=Dock.Text("",43,"#F3F6FB");clock.FontWeight=FontWeights.Light;clock.Margin=new Thickness(0,4,0,0);stack.Children.Add(clock);date=Dock.Text("",11,"#C0CBDA");stack.Children.Add(date);var summary=Dock.Text("Ctrl + 空格 · 随时搜索",10,"#91ABC6");summary.Margin=new Thickness(0,12,0,0);stack.Children.Add(summary);
 MouseLeftButtonDown+=(s,e)=>{if(e.ClickCount==2)dock.ToggleSearch();else DragMove();};MouseRightButtonUp+=(s,e)=>dock.OpenCategory("统计");timer.Interval=TimeSpan.FromSeconds(15);timer.Tick+=(s,e)=>Update();timer.Start();Update();Closed+=(s,e)=>timer.Stop();}
 void Update(){clock.Text=DateTime.Now.ToString("HH:mm");date.Text=DateTime.Now.ToString("M 月 d 日  dddd",System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));}
 public void SetCovered(bool covered){if(covered&&IsVisible)Hide();else if(!covered&&!IsVisible)Show();}
}


public static class DesktopSurface {
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string cls,string title);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int cmd);
 delegate bool EnumCallback(IntPtr h,IntPtr extra);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumCallback cb,IntPtr extra);
 static IntPtr desktopList;public static bool IsHidden{get{return desktopList!=IntPtr.Zero&&!Native.IsWindowVisible(desktopList);}}
 public static void HideIcons(){
 var prog=FindWindow("Progman",null);var view=FindWindowEx(prog,IntPtr.Zero,"SHELLDLL_DefView",null);
 if(view==IntPtr.Zero)EnumWindows((h,p)=>{view=FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null);return view==IntPtr.Zero;},IntPtr.Zero);
 if(view!=IntPtr.Zero){desktopList=FindWindowEx(view,IntPtr.Zero,"SysListView32",null);if(desktopList!=IntPtr.Zero)ShowWindow(desktopList,0);}
 }
 public static void Restore(){if(desktopList==IntPtr.Zero){var prog=FindWindow("Progman",null);var view=FindWindowEx(prog,IntPtr.Zero,"SHELLDLL_DefView",null);if(view==IntPtr.Zero)EnumWindows((h,p)=>{view=FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null);return view==IntPtr.Zero;},IntPtr.Zero);if(view!=IntPtr.Zero)desktopList=FindWindowEx(view,IntPtr.Zero,"SysListView32",null);}if(desktopList!=IntPtr.Zero)ShowWindow(desktopList,5);}
}
