using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// A shared, sleeping driver. Repeated input changes targets without resetting velocity.
public static class FluidMotion {
    sealed class State {
        public FrameworkElement Element;public ScaleTransform Scale=new ScaleTransform(1,1);public TranslateTransform Move=new TranslateTransform();
        public double Size=1,Velocity,Target=1,Y,YVelocity,Opacity=1,OpacityTarget=1,Delay;public Action Finished;
    }
    static readonly ConditionalWeakTable<FrameworkElement,State> states=new ConditionalWeakTable<FrameworkElement,State>();
    static readonly HashSet<State> active=new HashSet<State>();static readonly Stopwatch clock=Stopwatch.StartNew();static double last;
    static State Get(FrameworkElement element){return states.GetValue(element,e=>{var state=new State{Element=e};var group=new TransformGroup();if(e.RenderTransform!=null&&e.RenderTransform!=Transform.Identity)group.Children.Add(e.RenderTransform);group.Children.Add(state.Scale);group.Children.Add(state.Move);e.RenderTransform=group;e.RenderTransformOrigin=new Point(.5,.5);e.Unloaded+=(s,a)=>Remove(state);return state;});}
    static void Start(State state){if(active.Add(state)&&active.Count==1){last=clock.Elapsed.TotalSeconds;CompositionTarget.Rendering+=Frame;}}
    static void Remove(State state){active.Remove(state);if(active.Count==0)CompositionTarget.Rendering-=Frame;}
    public static void Press(Button button){
        button.PreviewMouseLeftButtonDown+=(s,e)=>{if(!SystemParameters.ClientAreaAnimation||ThemeState.ReducedMotion)return;var state=Get(button);state.Target=.965;Start(state);};
        System.Windows.Input.MouseEventHandler release=(s,e)=>{if(!SystemParameters.ClientAreaAnimation||ThemeState.ReducedMotion)return;var state=Get(button);state.Target=1;Start(state);};button.PreviewMouseLeftButtonUp+=(s,e)=>release(s,e);button.LostMouseCapture+=release;button.MouseLeave+=release;
    }
    public static void Reveal(FrameworkElement element,double delay=0){if(!SystemParameters.ClientAreaAnimation||ThemeState.ReducedMotion)return;var state=Get(element);state.Size=.975;state.Y=13;state.Opacity=0;state.OpacityTarget=1;state.Target=1;state.Delay=delay;state.Scale.ScaleX=state.Scale.ScaleY=state.Size;state.Move.Y=state.Y;element.Opacity=0;Start(state);}
    public static void Exit(FrameworkElement element,Action done){if(!SystemParameters.ClientAreaAnimation||ThemeState.ReducedMotion){done();return;}var state=Get(element);state.Target=.985;state.OpacityTarget=0;state.Finished=done;Start(state);}
    static void Frame(object sender,EventArgs e){double now=clock.Elapsed.TotalSeconds,dt=Math.Min(.032,Math.Max(.001,now-last));last=now;
        foreach(var state in active.ToArray()){
            if(state.Delay>0){state.Delay-=dt;continue;}
            for(int i=0;i<4;i++){double step=dt/4;state.Velocity+=(480*(state.Target-state.Size)-31*state.Velocity)*step;state.Size+=state.Velocity*step;state.YVelocity+=(-440*state.Y-34*state.YVelocity)*step;state.Y+=state.YVelocity*step;}
            state.Opacity+=(state.OpacityTarget-state.Opacity)*(1-Math.Exp(-dt*23));state.Scale.ScaleX=state.Scale.ScaleY=state.Size;state.Move.Y=state.Y;state.Element.Opacity=Math.Max(0,Math.Min(1,state.Opacity));
            if(Math.Abs(state.Target-state.Size)<.0003&&Math.Abs(state.Velocity)<.006&&Math.Abs(state.Y)<.05&&Math.Abs(state.OpacityTarget-state.Opacity)<.012){Remove(state);state.Element.Opacity=state.OpacityTarget;var done=state.Finished;state.Finished=null;if(done!=null)done();}
        }
    }
}
public partial class Dock {
    public void RevealItem(FrameworkElement element,int index=0){if(Settings.ReducedMotion)return;if(RecordingDemo)DemoReveal(element,index*3);else FluidMotion.Reveal(element,Math.Min(index,12)*.028);}
}
