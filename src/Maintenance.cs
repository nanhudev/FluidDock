using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;

public sealed class CareItem {
    public string Path,Destination,Kind,Reason;public long Bytes;public DateTime Modified;
    public string Label {get{return System.IO.Path.GetFileName(Path)+"  ·  "+CareScan.Size(Bytes)+"\n"+Path+"\n"+Reason+(Destination==null?"":" → "+Destination);}}
}
public sealed class CareReport {public List<CareItem> Items=new List<CareItem>();public int Skipped;public bool Limited;}
public static class CareScan {
    public static string Size(long n){return n>=1073741824?String.Format("{0:0.0} GB",n/1073741824.0):n>=1048576?String.Format("{0:0.0} MB",n/1048576.0):String.Format("{0:0} KB",n/1024.0);}
    public static bool Within(string file,string root){try{return Path.GetFullPath(file).StartsWith(Path.GetFullPath(root).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);}catch{return false;}}
    public static bool NoLinks(string path){try{var info=new FileInfo(path);if(!info.Exists)return false;if((info.Attributes&FileAttributes.ReparsePoint)!=0)return false;for(var d=info.Directory;d!=null;d=d.Parent)if((d.Attributes&FileAttributes.ReparsePoint)!=0)return false;return true;}catch{return false;}}
    public static bool TempEligible(string path,DateTime now){
        try{var f=new FileInfo(path);return Within(path,Path.GetTempPath())&&NoLinks(path)&&f.LastWriteTimeUtc<now.AddDays(-7)&&f.LastAccessTimeUtc<now.AddDays(-7)&&new[]{".tmp",".temp",".log",".dmp"}.Contains(f.Extension.ToLowerInvariant());}catch{return false;}
    }
    static string DesktopCategory(string path){string ext=Path.GetExtension(path).ToLowerInvariant();if(new[]{".lnk",".url"}.Contains(ext))return "快捷方式";if(new[]{".png",".jpg",".jpeg",".webp",".gif",".svg"}.Contains(ext))return "图片";if(new[]{".mp4",".mov",".mp3",".wav",".flac"}.Contains(ext))return "影音";if(new[]{".zip",".7z",".rar"}.Contains(ext))return "压缩包";return "文档与其他";}
    public static bool Protected(string path){
        try{if(File.Exists(path)||Directory.Exists(path)){var attributes=File.GetAttributes(path);if((attributes&(FileAttributes.System|FileAttributes.Hidden|FileAttributes.ReparsePoint))!=0)return true;}if(File.Exists(path)&&Path.GetDirectoryName(Path.GetFullPath(path).TrimEnd('\\'))==Path.GetPathRoot(path))return true;}catch{return true;}
        string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows),programs=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),programs32=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),appdata=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string relative=Path.GetFullPath(path).Substring(Path.GetPathRoot(path).Length).Split('\\')[0].ToLowerInvariant();if(new[]{"recovery","boot","windows.old","perflogs","$windows.~bt","$windows.~ws","$recycle.bin","system volume information"}.Contains(relative))return true;
        if(new[]{windows,programs,programs32,appdata,local,Environment.GetEnvironmentVariable("ProgramData")}.Where(r=>!String.IsNullOrEmpty(r)).Any(r=>Within(path,r)||String.Equals(Path.GetFullPath(path).TrimEnd('\\'),r.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)))return true;
        return new[]{".exe",".dll",".sys",".msi",".lnk",".url",".vhd",".vhdx"}.Contains(Path.GetExtension(path).ToLowerInvariant());
    }
    public static CareReport Scan(string mode,string root,Func<bool> canceled){
        var report=new CareReport();var watch=Stopwatch.StartNew();var queue=new Queue<string>();queue.Enqueue(Path.GetFullPath(root));int visited=0;
        string archive=Path.Combine(root,"Dock 整理",DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        while(queue.Count>0&&!canceled()){
            if(visited++>4000||watch.Elapsed.TotalSeconds>20||report.Items.Count>=500){report.Limited=true;break;}string dir=queue.Dequeue();
            try{
                foreach(string path in Directory.EnumerateFiles(dir)){
                    if(canceled())break;if(watch.Elapsed.TotalSeconds>20||report.Items.Count>=500){report.Limited=true;break;}
                    try{var f=new FileInfo(path);if(!NoLinks(path)){report.Skipped++;continue;}
                        if(mode=="desktop"){
                            if(f.Name.Equals("desktop.ini",StringComparison.OrdinalIgnoreCase)||new[]{".exe",".dll",".sys"}.Contains(f.Extension.ToLowerInvariant()))continue;
                            report.Items.Add(new CareItem{Path=path,Bytes=f.Length,Modified=f.LastWriteTimeUtc,Kind=mode,Destination=Path.Combine(archive,DesktopCategory(path),f.Name),Reason="移动到桌面归档"});
                        }else if(mode=="temp"?TempEligible(path,DateTime.UtcNow):f.Length>=250L*1048576&&!Protected(path)){
                            report.Items.Add(new CareItem{Path=path,Bytes=f.Length,Modified=f.LastWriteTimeUtc,Kind=mode,Reason=mode=="temp"?"超过 7 天未修改、未访问的临时文件；移入回收站":"大文件，内容未判断；仅在确认不需要后移入回收站"});
                        }
                    }catch{report.Skipped++;}
                }
                if(mode!="desktop")foreach(string child in Directory.EnumerateDirectories(dir))try{var d=new DirectoryInfo(child);if((d.Attributes&FileAttributes.ReparsePoint)!=0||new[]{".git","node_modules",".venv","dock 整理","$recycle.bin","system volume information","onedrive"}.Contains(d.Name.ToLowerInvariant())||(mode!="temp"&&Protected(child))){report.Skipped++;continue;}queue.Enqueue(child);}catch{report.Skipped++;}
            }catch{report.Skipped++;}
        }
        report.Items=report.Items.OrderByDescending(i=>i.Bytes).ToList();return report;
    }
    public static bool SameFile(CareItem item){try{var f=new FileInfo(item.Path);return NoLinks(item.Path)&&f.Length==item.Bytes&&f.LastWriteTimeUtc==item.Modified;}catch{return false;}}
    public static string Apply(CareItem item,string root){
        if(!Within(item.Path,root)||!SameFile(item))return "跳过：文件已变化或位置不符";
        if(item.Kind=="desktop"){
            if(Path.GetDirectoryName(Path.GetFullPath(item.Path)).TrimEnd('\\')!=Path.GetFullPath(root).TrimEnd('\\')||!Within(item.Destination,Path.Combine(root,"Dock 整理")))return "跳过：归档位置不符";
            var parent=Path.GetDirectoryName(item.Destination);Directory.CreateDirectory(parent);for(var d=new DirectoryInfo(parent);d!=null;d=d.Parent)if((d.Attributes&FileAttributes.ReparsePoint)!=0)return "跳过：归档含链接";
            if(File.Exists(item.Destination))return "跳过：目标已存在";File.Move(item.Path,item.Destination);return "已移动";
        }
        if(item.Kind=="temp"?!TempEligible(item.Path,DateTime.UtcNow):item.Kind!="large"||Protected(item.Path))return "跳过：不在允许范围";
        try{using(var unlocked=File.Open(item.Path,FileMode.Open,FileAccess.Read,FileShare.None)){} }catch{return "跳过：文件被占用或无权访问";}
        return RecycleFiles.Send(item.Path)&&!File.Exists(item.Path)?"已移入回收站":"未清理（系统取消或文件占用）";
    }
}
public sealed class CareWindow:Window {
    public bool HasSelection {get{return selections.Any(p=>p.Item1.IsChecked==true);}}
    readonly Dock dock;readonly StackPanel rows=new StackPanel();readonly TextBlock status;readonly Button execute;readonly List<Tuple<CheckBox,CareItem>> selections=new List<Tuple<CheckBox,CareItem>>();string root,mode;bool stopped,busy;
    public CareWindow(Dock owner){
        dock=owner;Title="电脑整理";Width=740;Height=640;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Dock.B("#202630");Foreground=Brushes.White;FontFamily=new FontFamily("Microsoft YaHei UI");ResizeMode=ResizeMode.CanResize;Closed+=(s,e)=>stopped=true;
        var grid=new Grid{Margin=new Thickness(24)};Content=grid;grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var head=new StackPanel();head.Children.Add(Dock.Text("电脑整理",22,"#F1F5FA"));var actions=new WrapPanel{Margin=new Thickness(0,16,0,12)};head.Children.Add(actions);grid.Children.Add(head);
        AddButton(actions,"桌面归档",()=>Scan("desktop",Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)));AddButton(actions,"用户临时文件",()=>Scan("temp",Path.GetTempPath()));AddButton(actions,"大文件…",PickLarge);AddButton(actions,"已安装应用",ShowApps);
        AddButton(actions,"Windows 存储清理",()=>{if(MessageBox.Show(this,"打开 Windows 存储设置。系统缓存、更新文件等由你在系统中勾选确认。","系统存储",MessageBoxButton.OKCancel)==MessageBoxResult.OK)Process.Start(new ProcessStartInfo("ms-settings:storagesense"){UseShellExecute=true});});
        status=Dock.Text("选择扫描范围。扫描不会修改文件。",12,"#AFC1D6");status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(0,0,0,12);Grid.SetRow(status,1);grid.Children.Add(status);
        var scroll=new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,2);grid.Children.Add(scroll);
        execute=new Button{Content="处理勾选项",Padding=new Thickness(20,12,20,12),HorizontalAlignment=HorizontalAlignment.Right,IsEnabled=false,Margin=new Thickness(0,14,0,0)};Dock.ButtonStyle(execute);execute.Click+=(s,e)=>Confirm();Grid.SetRow(execute,3);grid.Children.Add(execute);
    }
    void AddButton(Panel p,string text,Action action){var b=new Button{Content=text,Padding=new Thickness(12,9,12,9),Margin=new Thickness(0,0,8,8)};Dock.ButtonStyle(b);b.Click+=(s,e)=>{if(!busy)action();};p.Children.Add(b);}
    void ShowApps(){rows.Children.Clear();selections.Clear();UpdateCount();status.Text="可通过 Windows 卸载的应用，按登记大小排序。请自行判断是否仍需要；未记录使用不代表没用。";foreach(var app in InstalledApps.List()){
        var row=new StackPanel{Margin=new Thickness(3,10,3,10)};row.Children.Add(Dock.Text(app.Name+" · "+app.Version+" · "+(app.Bytes>0?CareScan.Size(app.Bytes):"大小未知"),13,"#F1F5FA"));
        var b=new Button{Content="在 Windows 中选择卸载",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,7,0,0)};Dock.ButtonStyle(b);string name=app.Name;b.Click+=(s,e)=>{if(MessageBox.Show(this,"打开 Windows 应用列表。找到“"+name+"”，选择卸载并由系统确认。","卸载应用",MessageBoxButton.OKCancel)==MessageBoxResult.OK)Process.Start(new ProcessStartInfo("ms-settings:appsfeatures"){UseShellExecute=true});};row.Children.Add(b);rows.Children.Add(row);
    }}
    void PickLarge(){using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description="选择扫描大文件的本地文件夹"})if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK)Scan("large",picker.SelectedPath);}
    void Scan(string kind,string folder){
        try{var drive=new DriveInfo(Path.GetPathRoot(folder));if(drive.DriveType!=DriveType.Fixed){status.Text="请选择本地固定磁盘。";return;}if(kind=="large"&&CareScan.Protected(folder)){status.Text="请选择个人文件夹，系统和应用目录不参与清理。";return;}}catch{status.Text="无法访问此位置。";return;}
        mode=kind;root=folder;busy=true;execute.IsEnabled=false;rows.Children.Clear();selections.Clear();status.Text="正在扫描："+folder;
        ThreadPool.QueueUserWorkItem(s=>{CareReport report;try{report=CareScan.Scan(kind,folder,()=>stopped);}catch(Exception ex){if(!stopped)Dispatcher.BeginInvoke(new Action(()=>{busy=false;status.Text=ex.Message;}));return;}if(stopped)return;Dispatcher.BeginInvoke(new Action(()=>{
            busy=false;status.Text=(kind=="desktop"?"只移动所选文件，文件夹与程序保留。":kind=="temp"?"仅用户临时目录，超过 7 天的临时文件。": "≥250 MB，大小不代表可删除。请核对内容。")+"\n"+report.Items.Count+" 项 · "+CareScan.Size(report.Items.Sum(i=>i.Bytes))+" · 跳过 "+report.Skipped+" 个无法访问或受保护位置"+(report.Limited?" · 已达扫描上限，可选择更小的文件夹":"")+"\n移入回收站后不会立即释放空间；清空回收站需自行操作。";
            foreach(var item in report.Items){var cb=new CheckBox{Content=new TextBlock{Text=item.Label,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.White,FontSize=12},Margin=new Thickness(4,10,4,10),IsChecked=false};cb.Checked+=(a,b)=>UpdateCount();cb.Unchecked+=(a,b)=>UpdateCount();selections.Add(Tuple.Create(cb,item));rows.Children.Add(cb);}UpdateCount();
        }));});
    }
    void UpdateCount(){int n=selections.Count(p=>p.Item1.IsChecked==true);execute.Content="处理勾选项（"+n+"）";execute.IsEnabled=!busy&&n>0;}
    void Confirm(){
        var chosen=selections.Where(p=>p.Item1.IsChecked==true).Select(p=>p.Item2).ToArray();if(chosen.Length==0||busy)return;
        string summary=(mode=="desktop"?"移动到桌面归档":"移入回收站")+"："+chosen.Length+" 项，"+CareScan.Size(chosen.Sum(i=>i.Bytes))+"。\n\n"+String.Join("\n",chosen.Take(8).Select(i=>i.Path))+(chosen.Length>8?"\n其余 "+(chosen.Length-8)+" 项见勾选清单。":"");
        if(MessageBox.Show(this,summary,"确认处理所选文件",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
        busy=true;execute.IsEnabled=false;foreach(var p in selections)p.Item1.IsEnabled=false;
        var stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");string audit=Path.Combine(Dock.Home,"care-history",stamp+".json");
        try{Directory.CreateDirectory(Path.GetDirectoryName(audit));File.WriteAllText(audit,Dock.Json.Serialize(new{state="authorized",time=DateTime.UtcNow,items=chosen}));}catch(Exception ex){busy=false;status.Text="无法保存处理清单："+ex.Message;foreach(var p in selections)p.Item1.IsEnabled=true;UpdateCount();return;}
        var worker=new Thread(()=>{var outcomes=new List<object>();int done=0;foreach(var item in chosen){if(stopped)break;string result;try{result=CareScan.Apply(item,root);}catch(Exception ex){result=ex.Message;}if(result=="已移动"||result=="已移入回收站")done++;outcomes.Add(new{item=item,result=result});try{File.WriteAllText(audit,Dock.Json.Serialize(new{state="processed",time=DateTime.UtcNow,authorized=chosen,items=outcomes}));}catch{break;}}if(!stopped)Dispatcher.BeginInvoke(new Action(()=>{busy=false;status.Text="已处理 "+done+" / "+chosen.Length+" 项。记录："+audit;rows.Children.Clear();selections.Clear();UpdateCount();}));});worker.IsBackground=true;worker.SetApartmentState(ApartmentState.STA);worker.Start();
    }
}
public partial class Dock {public void OpenCare(){new CareWindow(this).Show();}}

