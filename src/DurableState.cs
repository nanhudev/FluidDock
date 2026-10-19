using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

// State belongs in the user profile, independently of the executable/install folder.
public static class DurableState {
    static bool Read<T>(string path,out T value) where T:class {
        value=null;
        if(!File.Exists(path))return false;
        try{value=Dock.Json.Deserialize<T>(File.ReadAllText(path));return value!=null;}
        catch(ArgumentException){return false;}catch(InvalidOperationException){return false;}
    }
    public static T Load<T>(string path,Func<T> defaults) where T:class {
        T value;if(Read(path,out value))return value;
        // A flushed pending write is newer than the last committed backup.
        foreach(string candidate in new[]{path+".tmp",path+".bak"})
            if(Read(candidate,out value)){PreserveDamaged(path);Save(path,value);return value;}
        string backups=Path.Combine(Path.GetDirectoryName(path),"backups");
        if(Directory.Exists(backups))foreach(string folder in Directory.GetDirectories(backups).OrderByDescending(Directory.GetLastWriteTimeUtc)){
            if(Read(Path.Combine(folder,Path.GetFileName(path)),out value)){PreserveDamaged(path);Save(path,value);return value;}
        }
        // Never silently replace existing, unreadable user data with an empty Dock.
        if(File.Exists(path)||File.Exists(path+".tmp")||File.Exists(path+".bak"))throw new InvalidDataException("配置无法读取，请从 backups 恢复："+path);
        value=defaults();Save(path,value);return value;
    }
    static void PreserveDamaged(string path){if(File.Exists(path))File.Copy(path,path+".damaged-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+"-"+Guid.NewGuid().ToString("N"),false);}
    public static void Save(string path,object value){
        if(value==null)throw new ArgumentNullException("value");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        byte[] bytes=new UTF8Encoding(false).GetBytes(Dock.Json.Serialize(value));
        using(var stream=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(File.Exists(path)){
            object previous;bool valid=Read<object>(path,out previous);
            File.Replace(path+".tmp",path,valid?path+".bak":null);
        }else File.Move(path+".tmp",path);
    }
    public static string ResolveHome(string local){
        string legacy=Path.Combine(local,"FluidDock"),current=Path.Combine(local,"DockBot");
        // A missing/truncated primary during recovery must not select a different profile.
        return HasState(legacy)?legacy:current;
    }
    static bool HasState(string folder){return new[]{"apps.json","apps.json.tmp","apps.json.bak","preferences.json"}.Any(n=>File.Exists(Path.Combine(folder,n)))||Directory.Exists(Path.Combine(folder,"backups"));}
}
