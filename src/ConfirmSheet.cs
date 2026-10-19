using System.Windows;
using System.Windows.Controls;

// Confirmation remains a separate explicit click; animation never authorizes work.
public sealed class ConfirmSheet:GlassWindow {
    bool confirmed;
    ConfirmSheet(Window owner,Dock dock,string summary){
        Owner=owner;ShowActivated=true;WindowStartupLocation=WindowStartupLocation.CenterOwner;Width=560;Height=350;Title="确认处理";
        var root=new DockPanel{Margin=new Thickness(26)};var title=Dock.Text("确认处理所选文件",21,"#F1F5FA");title.Margin=new Thickness(0,0,0,20);DockPanel.SetDock(title,System.Windows.Controls.Dock.Top);root.Children.Add(title);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,20,0,0)};DockPanel.SetDock(actions,System.Windows.Controls.Dock.Bottom);root.Children.Add(actions);
        var cancel=new Button{Content="取消",Padding=new Thickness(20,11,20,11),Margin=new Thickness(0,0,10,0)};Dock.ButtonStyle(cancel);cancel.Click+=(s,e)=>Close();actions.Children.Add(cancel);
        var accept=new Button{Content="确认处理",Padding=new Thickness(20,11,20,11)};Dock.ButtonStyle(accept,false);ControlTheme.Primary(accept);accept.Click+=(s,e)=>{confirmed=true;Close();};actions.Children.Add(accept);
        var text=Dock.Text(summary,13,"#D9E0E5");text.TextWrapping=TextWrapping.Wrap;text.LineHeight=23;root.Children.Add(new ScrollViewer{Content=text,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});PanelChrome.Attach(this,root,dock);
        Loaded+=(s,e)=>cancel.Focus();
    }
    public static bool Ask(Window owner,Dock dock,string summary){var sheet=new ConfirmSheet(owner,dock,summary);sheet.ShowDialog();return sheet.confirmed;}
}
