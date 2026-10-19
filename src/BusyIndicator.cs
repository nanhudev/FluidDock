using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;

// One shared frame callback for visible, active indicators. Nothing ticks while idle.
public sealed class BusyIndicator:FrameworkElement {
    static readonly HashSet<BusyIndicator> active=new HashSet<BusyIndicator>();
    static readonly Stopwatch clock=Stopwatch.StartNew();
    static readonly Pen track=new Pen(Dock.B("#297F9CAC"),2);
    static readonly Pen ink=new Pen(Dock.B("#86D6CA"),2);
    static readonly Geometry arc=Geometry.Parse("M 10,3 A 7,7 0 1 1 3,10");
    bool running;double angle;
    public bool IsRunning {get{return running;}set{running=value;Visibility=value?Visibility.Visible:Visibility.Collapsed;Update();}}
    public static int ActiveCount {get{return active.Count;}}
    static BusyIndicator(){track.Freeze();ink.StartLineCap=ink.EndLineCap=PenLineCap.Round;ink.Freeze();arc.Freeze();}
    public BusyIndicator(){Width=20;Height=20;Margin=new Thickness(0,0,10,0);Visibility=Visibility.Collapsed;IsHitTestVisible=false;Loaded+=(s,e)=>Update();Unloaded+=(s,e)=>Stop();System.Windows.Automation.AutomationProperties.SetName(this,"正在扫描");}
    void Update(){
        if(running&&IsLoaded&&SystemParameters.ClientAreaAnimation){if(active.Add(this)&&active.Count==1)CompositionTarget.Rendering+=Frame;}
        else Stop();InvalidateVisual();
    }
    void Stop(){if(active.Remove(this)&&active.Count==0)CompositionTarget.Rendering-=Frame;}
    static void Frame(object sender,EventArgs e){double value=clock.Elapsed.TotalSeconds*240%360;foreach(var item in active.ToArray()){item.angle=value;item.InvalidateVisual();}}
    public static void DemoFrame(double seconds){foreach(var item in active.ToArray()){item.angle=seconds*240%360;item.InvalidateVisual();}}
    protected override void OnRender(DrawingContext d){d.DrawEllipse(null,track,new Point(10,10),7,7);d.PushTransform(new RotateTransform(angle,10,10));d.DrawGeometry(null,ink,arc);d.Pop();}
}
