using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;

public static class PanelChrome {
    // Native blur and the painted rounded tint have separate responsibilities.
    public static Brush Material(bool light){var b=new LinearGradientBrush(light?Color.FromArgb(230,240,241,245):Color.FromArgb(224,42,44,50),light?Color.FromArgb(210,223,227,235):Color.FromArgb(205,24,26,31),90);b.Freeze();return b;}
    public static void Attach(Window window,FrameworkElement body,Dock dock){
        window.Background=Brushes.Transparent;window.Content=null;var frame=new Border{CornerRadius=new CornerRadius(24),Background=Material(dock.Settings.Appearance=="light")};var stack=new Grid();frame.Child=stack;stack.Children.Add(body);
        var close=new Button{Content="×",FontSize=19,Width=30,Height=30,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,14,14,0)};Dock.ButtonStyle(close);close.Click+=(s,e)=>window.Close();stack.Children.Add(close);window.Content=frame;SearchPanel.PaintText(frame,dock.Settings.Appearance=="light");
        window.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){window.Close();e.Handled=true;}};
        window.MouseLeftButtonDown+=(s,e)=>{if(e.GetPosition(window).Y<58&&e.OriginalSource is TextBlock)window.DragMove();};
        window.Loaded+=(s,e)=>{if(!dock.Settings.ReducedMotion&&!dock.RecordingDemo)FluidMotion.Reveal(frame);};
        bool closing=false,allowed=false;window.Closing+=(s,e)=>{if(allowed||dock.ClosingDock||dock.RecordingDemo||dock.Settings.ReducedMotion||!window.IsVisible)return;e.Cancel=true;if(closing)return;closing=true;FluidMotion.Exit(frame,()=>{allowed=true;window.Close();});};
    }
}
public partial class Dock {
    Button onboarding;
    double plateWidth,plateVelocity;
    void UpdateOnboarding(bool empty){
        if(onboarding==null){onboarding=new Button{Content="一键配置",Width=112,Height=33};ButtonStyle(onboarding,false);onboarding.Background=B("#ADDCD5");onboarding.Foreground=B("#203638");onboarding.Click+=(s,e)=>OpenSetup();canvas.Children.Add(onboarding);}
        onboarding.Visibility=empty?Visibility.Visible:Visibility.Collapsed;
    }
    bool StepPlate(double dt){if(plateWidth<=0||Settings.ReducedMotion){plateWidth=baseWidth;plateVelocity=0;return false;}for(int i=0;i<4;i++){plateVelocity+=(520*(baseWidth-plateWidth)-40*plateVelocity)*dt/4;plateWidth+=plateVelocity*dt/4;}return Math.Abs(baseWidth-plateWidth)>.04||Math.Abs(plateVelocity)>.1;}
}
