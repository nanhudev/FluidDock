using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Threading;

public sealed class FileSearchResult {public List<Entry> Entries=new List<Entry>();public bool Indexed;}
public static class ComputerSearch {
    static readonly SemaphoreSlim searchGate=new SemaphoreSlim(1,1);
    static Entry FileEntry(string path){if(new[]{".lnk",".url",".exe"}.Contains(Path.GetExtension(path).ToLowerInvariant()))try{return AppCatalog.FromFile(path);}catch{}return new Entry{Name=Path.GetFileName(path),Target=path,WorkDir=Path.GetDirectoryName(path),Arguments="",Icon="",Group="文件"};}
    public static FileSearchResult Find(string query,string[] roots,Func<bool> canceled){
        while(!searchGate.Wait(30))if(canceled())return new FileSearchResult();
        try{return canceled()?new FileSearchResult():FindCore(query,roots,canceled);}finally{searchGate.Release();}
    }
    static FileSearchResult FindCore(string query,string[] roots,Func<bool> canceled){
        var result=new FileSearchResult();string term=Regex.Replace(query,@"[^\p{L}\p{N} ._-]","");if(term.Trim().Length<2)return result;
        try{using(var conn=new OleDbConnection("Provider=Search.CollatorDSO;Extended Properties='Application=Windows';")){
            conn.Open();using(var command=conn.CreateCommand()){
                command.CommandTimeout=2;command.CommandText="SELECT TOP 60 System.ItemPathDisplay FROM SystemIndex WHERE System.FileName LIKE '%"+term.Replace("'","''")+"%' ORDER BY System.DateModified DESC";
                using(var reader=command.ExecuteReader()){result.Indexed=true;while(reader.Read()&&!canceled()){string path=Convert.ToString(reader[0]);if(File.Exists(path))result.Entries.Add(FileEntry(path));}}
            }
        }}catch{}
        if(result.Entries.Count>0||canceled())return result;
        var queue=new Queue<Tuple<string,int>>();foreach(var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))queue.Enqueue(Tuple.Create(root,0));var watch=Stopwatch.StartNew();int files=0;
        while(queue.Count>0&&watch.ElapsedMilliseconds<500&&files<5000&&!canceled()){
            var dir=queue.Dequeue();try{
                foreach(string file in Directory.EnumerateFiles(dir.Item1)){if(canceled()||files++>5000||watch.ElapsedMilliseconds>=500)break;if(Path.GetFileName(file).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0){result.Entries.Add(FileEntry(file));if(result.Entries.Count>=60)return result;}}
                if(dir.Item2<3)foreach(string child in Directory.EnumerateDirectories(dir.Item1))try{if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0||new[]{"node_modules",".git","appdata",".venv","$recycle.bin"}.Contains(Path.GetFileName(child).ToLowerInvariant()))continue;queue.Enqueue(Tuple.Create(child,dir.Item2+1));}catch{}
            }catch{}
        }
        return result;
    }
}
public partial class SearchPanel {
    List<Entry> fileResults=new List<Entry>();string fileQuery="";int fileGeneration;DispatcherTimer fileDebounce;bool fileIndexed;
    void RequestFiles(string query){
        if(Mode!="library"||!(Category=="全部"||Category=="文件"))return;
        if(query==fileQuery)return;fileQuery=query;fileResults.Clear();int generation=++fileGeneration;
        if(fileDebounce!=null)fileDebounce.Stop();if(query.Length<2)return;
        fileDebounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(160)};
        fileDebounce.Tick+=(s,e)=>{fileDebounce.Stop();var roots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads"),Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)}.Concat(dock.Settings.SearchRoots??new string[0]).ToArray();
            ThreadPool.QueueUserWorkItem(state=>{var found=ComputerSearch.Find(query,roots,()=>generation!=fileGeneration);if(generation!=fileGeneration||Dispatcher.HasShutdownStarted)return;Dispatcher.BeginInvoke(new Action(()=>{if(generation!=fileGeneration||Mode!="library")return;fileResults=found.Entries;fileIndexed=found.Indexed;Refresh();foot.Text=fileIndexed?"应用与 Windows 索引文件":"应用与个人文件夹 · 可添加搜索目录";}));});
        };fileDebounce.Start();
    }
}
