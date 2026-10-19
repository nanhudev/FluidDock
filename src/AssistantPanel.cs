using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;

public static class ThemeState {public static bool Light;}
public static partial class Native {
    public static void SetAppearance(Window window,string theme){
        var h=new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        var accent=new ACCENT{State=theme=="graphite"?0:3,Flags=0,Color=0,Animation=0};
        int size=System.Runtime.InteropServices.Marshal.SizeOf(accent);var mem=System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
        try{System.Runtime.InteropServices.Marshal.StructureToPtr(accent,mem,false);var data=new COMPOSITION{Attribute=19,Data=mem,Size=size};SetWindowCompositionAttribute(h,ref data);}catch{window.Background=Dock.B(theme=="light"?"#FFE1E7EE":"#FF202630");}finally{System.Runtime.InteropServices.Marshal.FreeHGlobal(mem);}
    }
}
public partial class SearchPanel {
    public string Mode="library";
    Border assistantRoot;TextBlock assistantText;Button apply;DockPlan pending;TextBlock inputHint;
    void InitializeAssistant(Grid body,DockPanel bottom){
        assistantRoot=new Border{CornerRadius=new CornerRadius(18),Background=Dock.B("#16101824"),Padding=new Thickness(20),Visibility=Visibility.Collapsed};
        var stack=new StackPanel();assistantRoot.Child=stack;
        var heading=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,8)};heading.Children.Add(new BuddyFace());stack.Children.Add(heading);
        assistantText=Dock.Text("试试添加软件、调整顺序、改变风格或大小。",14,"#D2DDEB");assistantText.TextWrapping=TextWrapping.Wrap;assistantText.LineHeight=25;assistantText.Margin=new Thickness(0,10,0,18);stack.Children.Add(assistantText);
        apply=new Button{Content="应用这个操作",Padding=new Thickness(18,11,18,11),HorizontalAlignment=HorizontalAlignment.Left,IsEnabled=false};Dock.ButtonStyle(apply,false);apply.Background=Dock.B("#ADDCD5");apply.Foreground=Dock.B("#203638");apply.Click+=(s,e)=>RunAssistant();stack.Children.Add(apply);
        var chips=new WrapPanel{Margin=new Thickness(0,22,0,0)};foreach(string sample in new[]{"一键配置","清理电脑","图标小一点","撤销"}){string text=sample;var b=new Button{Content=sample,Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,0,8,8),FontSize=11};Dock.ButtonStyle(b);b.Click+=(s,e)=>{input.Text=text;input.Focus();};chips.Children.Add(b);}stack.Children.Add(chips);body.Children.Add(assistantRoot);
        var assistant=new Button{Content="✦ 助手",Padding=new Thickness(9,7,9,7),FontSize=11};Dock.ButtonStyle(assistant);DockPanel.SetDock(assistant,System.Windows.Controls.Dock.Right);bottom.Children.Insert(0,assistant);assistant.Click+=(s,e)=>SetMode("assistant");
        var remove=new Button{Content="− 管理",Padding=new Thickness(9,7,9,7),FontSize=11};Dock.ButtonStyle(remove);DockPanel.SetDock(remove,System.Windows.Controls.Dock.Right);bottom.Children.Insert(0,remove);remove.Click+=(s,e)=>SetMode("remove");
        var file=new Button{Content="选择文件…",Padding=new Thickness(9,7,9,7),FontSize=11};Dock.ButtonStyle(file);DockPanel.SetDock(file,System.Windows.Controls.Dock.Right);bottom.Children.Insert(0,file);file.Click+=(s,e)=>dock.Pick();
        ApplyTheme(dock.Settings.Appearance);
    }
    public void SetMode(string mode){Mode=mode;Height=mode=="assistant"?440:570;tabs.Visibility=mode=="assistant"?Visibility.Collapsed:Visibility.Visible;Category="全部";rowCache.Clear();input.Text="";Refresh();input.Focus();if(inputHint!=null)inputHint.Text=mode=="assistant"?"想做什么？":mode=="add"?"搜索想添加的软件…":mode=="remove"?"搜索想移除的 Dock 入口…":"搜索应用与文件…";}
    void UpdateAssistant(){
        if(assistantRoot==null)return;assistantRoot.Visibility=Visibility.Visible;results.Visibility=Visibility.Collapsed;empty.Visibility=Visibility.Collapsed;
        title.Text="DockBot";foot.Text="";pending=dock.Interpret(input.Text);
        if(input.Text.Trim().Length==0){assistantText.Text="想做什么？";apply.IsEnabled=false;return;}
        if(pending.Intent=="search"){
            var result=SoftwareResolver.Resolve(input.Text,dock.AllApps());var entry=result.Matches.FirstOrDefault();
            assistantText.Text=entry==null?"没有找到入口。可以选择快捷方式 / EXE，或在加号中搜索已安装软件。":entry.Name+"\n启动目标："+entry.Target+(String.IsNullOrEmpty(entry.WorkDir)?"":"\n工作目录："+entry.WorkDir);
            apply.Content="在文件夹中显示";apply.IsEnabled=entry!=null&&File.Exists(entry.Target);return;
        }
        apply.Content=pending.Intent=="care"||pending.Intent=="configure"?"打开":pending.Intent=="open"?"打开":pending.Intent=="close"?"关闭":"应用";apply.IsEnabled=pending.Actionable;
        if(!pending.Actionable){assistantText.Text=pending.Message;return;}
        assistantText.Text=Describe(pending);
    }
    static string Describe(DockPlan p){
        switch(p.Intent){
            case "batch":return String.Join("、",p.Steps.Select(s=>s.AppName).Distinct())+(p.Group!=""?" → "+p.Group:" · 加入 Dock");
            case "care":return "桌面归档 · 临时文件 · 大文件";
            case "configure":return "选择常用软件，加入 Dock。";
            case "open":return "打开 "+p.AppName;
            case "close":return "关闭 "+p.AppName+"，保留保存提示。";
            case "add":return "添加 "+p.AppName+" · "+(p.ResolvedEntry!=null?p.ResolvedEntry.Group:AppCatalog.GuessGroup(p.AppName));
            case "remove":return "移出 Dock · "+p.AppName;
            case "move":return "将调整“"+p.AppName+"”的位置"+(p.OtherName!=""?"，放到“"+p.OtherName+"”"+(p.Direction=="after"?"后面":"前面"):p.Direction=="last"?"，放到同类末尾":"，放到同类前面")+"。";
            case "group":return "将把“"+p.AppName+"”归入“"+p.Group+"”。";
            case "sort":return p.Group!=""?"将调整分类顺序："+p.Group+" → "+p.OtherGroup:"将按本机使用习惯整理同类应用。";
            case "theme":return "将切换为“"+(p.Theme=="light"?"浅色玻璃":p.Theme=="graphite"?"石墨":"深色磨砂")+"”。";
            case "size":return p.Scale>0?"将把 Dock 调整为 "+Math.Round(p.Scale*100)+"%。":p.Delta>0?"将把 Dock 放大一点。":"将把 Dock 缩小一点。";
            case "undo":return "将恢复上一项 Dock 改动。";
            default:return "将"+(p.Enable?"开启":"关闭")+(p.Intent=="startup"?"登录自启动":p.Intent=="widget"?"时钟小组件":p.Intent=="tracking"?"本地使用统计":"自动收起")+"。";
        }
    }
    void RunAssistant(){
        if(pending==null)return;
        if(pending.Intent=="search"){
            var entry=SoftwareResolver.Resolve(input.Text,dock.AllApps()).Matches.FirstOrDefault();if(entry!=null&&File.Exists(entry.Target))Process.Start("explorer.exe","/select,\""+entry.Target+"\"");return;
        }
        if(!pending.Actionable)return;
        try{var plan=pending;string message=dock.Execute(plan);assistantText.Text=message;apply.IsEnabled=false;}catch(Exception ex){assistantText.Text=ex.Message;apply.IsEnabled=false;}
    }
    void ActivateItem(Entry entry){
        if(Mode=="add"||Mode=="remove")try{
            string message=dock.Execute(new DockPlan{Intent=Mode=="add"?"add":"remove",AppName=entry.Name,TargetKey=AppCatalog.Key(entry)});Refresh();foot.Text=message;
        }catch(Exception ex){foot.Text=ex.Message;}
        else dock.Launch(entry);
    }
    public void ApplyTheme(string theme){
        ThemeState.Light=theme=="light";Native.SetAppearance(this,theme);surface.Background=theme=="graphite"?Dock.B("#FF202630"):PanelChrome.Material(theme=="light");PaintText(surface,ThemeState.Light);
    }
    public static void PaintText(DependencyObject root,bool light){
        var text=root as TextBlock;if(text!=null){if(text.Tag==null)text.Tag=text.Foreground;text.Foreground=light?Dock.B("#253247"):text.Tag as Brush;}
        var box=root as TextBox;if(box!=null){box.Foreground=light?Dock.B("#253247"):Brushes.White;box.CaretBrush=box.Foreground;}
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)PaintText(VisualTreeHelper.GetChild(root,i),light);
    }
}
