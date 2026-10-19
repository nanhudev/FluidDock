using System;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;

public sealed class BuddyFace:FrameworkElement {
    public BuddyFace(){Width=40;Height=40;SnapsToDevicePixels=true;}
    protected override void OnRender(DrawingContext d){
        var fill=new LinearGradientBrush(Color.FromRgb(159,224,213),Color.FromRgb(78,158,166),45);
        d.DrawEllipse(new SolidColorBrush(Color.FromArgb(35,0,0,0)),null,new Point(20,35),13,3);
        d.PushTransform(new RotateTransform(-6,20,20));
        d.DrawRoundedRectangle(fill,new Pen(new SolidColorBrush(Color.FromArgb(80,255,255,255)),.8),new Rect(5,8,30,26),10,10);
        d.DrawEllipse(Brushes.White,null,new Point(1.5,25),2.5,3.5);d.DrawEllipse(Brushes.White,null,new Point(38,22),2.5,3.5);
        d.DrawEllipse(new SolidColorBrush(Color.FromRgb(21,53,66)),null,new Point(15,20),2,3);
        d.DrawEllipse(new SolidColorBrush(Color.FromRgb(21,53,66)),null,new Point(25,20),2,3);
        d.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(110,255,255,255)),1),new Point(10,12),new Point(24,12));d.Pop();
    }
}
public static class DockDragPolicy {
    public static string Decide(Rect plate,Point drop,bool cancelled){if(cancelled)return "cancel";plate.Inflate(24,32);return plate.Contains(drop)?"move":"remove";}
}
public sealed class CarryOverlay:Window {
    [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr h,int index);
    [DllImport("user32.dll")]static extern int SetWindowLong(IntPtr h,int index,int value);
    readonly Dock dock;readonly Entry entry;Canvas canvas;Grid cargo;BuddyFace buddy;TranslateTransform cargoMove=new TranslateTransform(),buddyMove=new TranslateTransform();ScaleTransform scale=new ScaleTransform(1,1);RotateTransform tilt=new RotateTransform();
    Point position,target,velocity;Stopwatch clock=Stopwatch.StartNew();double last;bool animating,discard;
    public CarryOverlay(Dock owner,Entry app,Point start){
        dock=owner;entry=app;Owner=owner;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;IsHitTestVisible=false;
        Left=SystemParameters.VirtualScreenLeft;Top=SystemParameters.VirtualScreenTop;Width=SystemParameters.VirtualScreenWidth;Height=SystemParameters.VirtualScreenHeight;
        canvas=new Canvas{IsHitTestVisible=false};Content=canvas;cargo=new Grid{Width=48,Height=48,IsHitTestVisible=false};cargo.Children.Add(dock.AppVisual(entry,48));cargo.RenderTransformOrigin=new Point(.5,.5);
        var tg=new TransformGroup();tg.Children.Add(scale);tg.Children.Add(cargoMove);cargo.RenderTransform=tg;canvas.Children.Add(cargo);
        buddy=new BuddyFace();var bg=new TransformGroup();bg.Children.Add(tilt);bg.Children.Add(buddyMove);buddy.RenderTransform=bg;buddy.RenderTransformOrigin=new Point(.5,.5);canvas.Children.Add(buddy);
        SourceInitialized+=(s,e)=>{var h=new WindowInteropHelper(this).Handle;SetWindowLong(h,-20,GetWindowLong(h,-20)|0x20|0x80|0x08000000);};
        Show();position=PointFromScreen(start);Position(position);Closed+=(s,e)=>{if(animating)CompositionTarget.Rendering-=Frame;};
    }
    public void Follow(Point screen){Point next=PointFromScreen(screen);double now=clock.Elapsed.TotalSeconds,dt=Math.Max(.004,now-last);velocity=new Point(Math.Max(-2500,Math.Min(2500,(next.X-position.X)/dt)),Math.Max(-2500,Math.Min(2500,(next.Y-position.Y)/dt)));last=now;position=next;Position(position);}
    void Position(Point p){cargoMove.X=p.X-24;cargoMove.Y=p.Y-24;buddyMove.X=p.X+20;buddyMove.Y=p.Y-43;tilt.Angle=Math.Max(-13,Math.Min(13,velocity.X/140));}
    public void Finish(Point screen,bool remove){target=PointFromScreen(screen);discard=remove;last=clock.Elapsed.TotalSeconds;animating=true;CompositionTarget.Rendering+=Frame;}
    void Frame(object sender,EventArgs e){
        double now=clock.Elapsed.TotalSeconds,dt=Math.Min(.028,Math.Max(.001,now-last));last=now;
        for(int i=0;i<4;i++){double d=dt/4;velocity.X+=(560*(target.X-position.X)-43*velocity.X)*d;velocity.Y+=(560*(target.Y-position.Y)-43*velocity.Y)*d;position.X+=velocity.X*d;position.Y+=velocity.Y*d;}
        Position(position);double distance=(position-target).Length;if(discard){double amount=Math.Max(.08,Math.Min(1,distance/150));scale.ScaleX=scale.ScaleY=amount;cargo.Opacity=amount;}
        if(distance<1.2&&Math.Abs(velocity.X)+Math.Abs(velocity.Y)<18){animating=false;CompositionTarget.Rendering-=Frame;Close();}
    }
    public static void Animate(Dock dock,Entry entry,Point from,Point to,bool discard){var overlay=new CarryOverlay(dock,entry,from);overlay.Finish(to,discard);}
}
public partial class Dock {
    bool draggingEntry;
    void BeginDockDrag(Entry entry,Button button,Tile tile){
        if(draggingEntry||Utility(entry)||entry.Target.StartsWith("fluid://"))return;
        draggingEntry=true;tile.Pressed=false;caption.Opacity=0;button.Opacity=.25;
        var pointer=System.Windows.Forms.Cursor.Position;var ghost=new CarryOverlay(this,entry,new Point(pointer.X,pointer.Y));
        MouseEventHandler move=null,lost=null;MouseButtonEventHandler up=null;KeyEventHandler key=null;bool finished=false;
        Action<bool> complete=cancelled=>{
            if(finished)return;finished=true;draggingEntry=false;button.PreviewMouseMove-=move;button.PreviewMouseLeftButtonUp-=up;button.LostMouseCapture-=lost;button.PreviewKeyDown-=key;button.ReleaseMouseCapture();button.Opacity=1;tile.Target=1;Wake();
            var cursor=System.Windows.Forms.Cursor.Position;Point at=new Point(cursor.X,cursor.Y);string action=DockDragPolicy.Decide(PlateScreenRect(),at,cancelled);
            try{
                if(action=="remove"){Execute(new DockPlan{Intent="remove",AppName=entry.Name},false);ghost.Finish(TrashScreenPoint(),true);}
                else if(action=="move"){
                    var other=NearestEntry(at);if(other!=null&&other!=entry)Execute(new DockPlan{Intent="move",AppName=entry.Name,OtherName=other.Name,Direction=at.X<IconScreenPoint(other).X?"before":"after"},false);
                    ghost.Finish(IconDestination(entry),false);
                }else ghost.Finish(IconScreenPoint(entry),false);
            }catch(Exception ex){ghost.Close();Toast(ex.Message);}
        };
        move=(s,e)=>{var p=System.Windows.Forms.Cursor.Position;ghost.Follow(new Point(p.X,p.Y));e.Handled=true;};
        up=(s,e)=>{e.Handled=true;complete(false);};lost=(s,e)=>complete(true);key=(s,e)=>{if(e.Key==Key.Escape){e.Handled=true;complete(true);}};
        button.PreviewMouseMove+=move;button.PreviewMouseLeftButtonUp+=up;button.LostMouseCapture+=lost;button.PreviewKeyDown+=key;button.Focus();if(!button.CaptureMouse())complete(true);
    }
}
