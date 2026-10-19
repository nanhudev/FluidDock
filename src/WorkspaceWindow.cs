using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

public sealed class WorkspaceWindow:GlassWindow {
    readonly Dock dock;readonly StackPanel rows=new StackPanel();readonly TextBox name;readonly TextBlock status;readonly Button save;string renaming;
    public WorkspaceWindow(Dock owner){
        dock=owner;Owner=owner;Title="工作空间";Width=680;Height=540;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var root=new DockPanel{Margin=new Thickness(26)};var heading=new StackPanel();DockPanel.SetDock(heading,System.Windows.Controls.Dock.Top);root.Children.Add(heading);heading.Children.Add(Dock.Text("工作空间",22,"#F1F5FA"));
        status=Dock.Text("保存一套入口，随时切换。",12,"#9DAEBB");status.Margin=new Thickness(0,9,0,20);heading.Children.Add(status);
        var form=new DockPanel{Margin=new Thickness(0,0,0,20)};heading.Children.Add(form);save=new Button{Content="保存当前布局",Padding=new Thickness(17,10,17,10),Margin=new Thickness(12,0,0,0)};Dock.ButtonStyle(save,false);ControlTheme.Primary(save);DockPanel.SetDock(save,System.Windows.Controls.Dock.Right);form.Children.Add(save);
        name=new TextBox{FontSize=15,Foreground=Dock.B("#F1F5FA"),Background=Dock.B("#202A333A"),BorderBrush=Dock.B("#344E6269"),BorderThickness=new Thickness(1),Padding=new Thickness(12,9,12,9),MaxLength=32,VerticalContentAlignment=VerticalAlignment.Center};form.Children.Add(name);
        save.Click+=(s,e)=>{try{dock.Execute(new DockPlan{Intent=renaming==null?"workspace_save":"workspace_rename",WorkspaceName=renaming??name.Text,NewName=name.Text});renaming=null;save.Content="保存当前布局";name.Text="";Refresh();}catch(Exception ex){status.Text=ex.Message;}};
        root.Children.Add(new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});PanelChrome.Attach(this,root,owner);Refresh();
    }
    void Refresh(){
        rows.Children.Clear();string current=dock.Settings.ActiveWorkspace??"默认";status.Text="当前 · "+current+"   切换只调整 Dock，应用窗口保持打开。";
        var names=(dock.Settings.Workspaces??new List<WorkspaceProfile>()).Select(w=>w.Name).Concat(new[]{"默认","开发","办公","娱乐","音乐"}).Distinct(StringComparer.OrdinalIgnoreCase);
        int index=0;foreach(string key in names){string selected=key;var stored=WorkspaceStore.Find(dock.Settings,selected);var apps=stored!=null?stored.Apps??new List<Entry>():selected=="默认"?dock.Entries.Where(e=>e.Pinned&&!e.Hidden&&!WorkspaceStore.SystemEntry(e)).ToList():WorkspaceStore.TemplateApps(selected,dock.Entries);
            if(stored==null&&selected!="默认"&&apps.Count==0)continue;
            var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            var label=new StackPanel{VerticalAlignment=VerticalAlignment.Center};var title=Dock.Text(selected+(current==selected?"  ·  使用中":""),16,"#F1F5FA");label.Children.Add(title);var detail=Dock.Text(apps.Count+" 个入口 · "+String.Join("、",apps.Take(4).Select(a=>a.Name)),11,"#9DAEBB");detail.TextTrimming=TextTrimming.CharacterEllipsis;detail.Margin=new Thickness(0,6,12,0);label.Children.Add(detail);row.Children.Add(label);
            var open=new Button{Content="打开应用",Padding=new Thickness(12,9,12,9),Margin=new Thickness(0,0,8,0),IsEnabled=apps.Count>0&&apps.Count<=12};Dock.ButtonStyle(open);Grid.SetColumn(open,1);row.Children.Add(open);open.Click+=(s,e)=>{if(!ConfirmSheet.Ask(this,dock,"打开“"+selected+"”的 "+apps.Count+" 个入口：\n\n"+String.Join("、",apps.Select(a=>a.Name))))return;try{status.Text=dock.Execute(new DockPlan{Intent="workspace_open",WorkspaceName=selected});}catch(Exception ex){status.Text=ex.Message;}};
            var use=new Button{Content=current==selected?"已选":"切换",Padding=new Thickness(14,9,14,9)};Dock.ButtonStyle(use,false);ControlTheme.Primary(use);Grid.SetColumn(use,2);row.Children.Add(use);use.Click+=(s,e)=>{try{dock.Execute(new DockPlan{Intent="workspace_switch",WorkspaceName=selected});Refresh();}catch(Exception ex){status.Text=ex.Message;}};
            var card=new Border{CornerRadius=new CornerRadius(16),Background=Dock.B(current==selected?"#263B7773":"#12FFFFFF"),Padding=new Thickness(16),Margin=new Thickness(0,0,0,10),Child=row};
            if(stored!=null&&selected!="默认"){var menu=new ContextMenu();var rename=new MenuItem{Header="重命名"};rename.Click+=(s,e)=>{renaming=selected;name.Text=selected;save.Content="改名";name.Focus();name.SelectAll();};menu.Items.Add(rename);var delete=new MenuItem{Header="删除工作空间"};delete.Click+=(s,e)=>{try{dock.Execute(new DockPlan{Intent="workspace_delete",WorkspaceName=selected});Refresh();}catch(Exception ex){status.Text=ex.Message;}};menu.Items.Add(delete);card.ContextMenu=menu;}
            rows.Children.Add(card);dock.RevealItem(card,index++);
        }
    }
}
public partial class Dock {public void OpenWorkspaces(){new WorkspaceWindow(this).Show();}}
