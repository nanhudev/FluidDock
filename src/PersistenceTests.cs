using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
public static class PersistenceTests {
    public static void Run(string output,Action<string,bool,string> check){
        string root=Path.Combine(output,"state-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{
            string path=Path.Combine(root,"apps.json");
            Func<List<Entry>> empty=()=>new List<Entry>();
            var entries=new List<Entry>{new Entry{Name="微信",Target=@"Z:\Weixin.exe",Pinned=true,Group="办公"},new Entry{Name="QQ",Target=@"Z:\QQ.exe",Pinned=true,Group="办公"}};
            DurableState.Save(path,entries);entries.Reverse();DurableState.Save(path,entries);
            var loaded=DurableState.Load(path,empty);
            check("restart-keeps-manual-order",loaded[0].Name=="QQ"&&loaded[1].Pinned,"serialized order survives a fresh load");
            check("atomic-save-no-pending-file",!File.Exists(path+".tmp")&&File.Exists(path+".bak"),"committed file and previous valid generation");
            File.WriteAllText(path,"{truncated");loaded=DurableState.Load(path,empty);
            check("recover-corrupt-primary",loaded[0].Name=="微信"&&Directory.GetFiles(root,"*.damaged-*").Length==1,"restore last committed backup and preserve damaged bytes");
            File.Delete(path);loaded=DurableState.Load(path,empty);
            check("recover-missing-primary",loaded.Count==2&&File.Exists(path),"missing primary cannot seed empty defaults");
            File.WriteAllText(path,"null");File.WriteAllText(path+".tmp",Dock.Json.Serialize(entries));loaded=DurableState.Load(path,empty);
            check("recover-null-from-pending",loaded[0].Name=="QQ","complete pending write wins over older backup");
            File.WriteAllText(path+".tmp","[]");loaded=DurableState.Load(path,empty);
            check("ignore-stale-pending",loaded.Count==2,"valid committed primary wins");File.Delete(path+".tmp");
            DurableState.Save(path,new List<Entry>());check("empty-layout-valid",DurableState.Load(path,()=>entries).Count==0,"intentional empty layout stays empty");
            string settings=Path.Combine(root,"preferences.json");var prefs=new Prefs{DockScale=.85,Appearance="graphite",Adaptive=false,StartWithWindows=true};DurableState.Save(settings,prefs);
            var reread=DurableState.Load(settings,()=>new Prefs());check("restart-keeps-preferences",reread.DockScale==.85&&!reread.Adaptive&&reread.StartWithWindows&&reread.Appearance=="graphite","manual ordering, scale, theme and startup survive");
            string bad=Path.Combine(root,"invalid.json");File.WriteAllText(bad,"{");bool rejected=false;try{DurableState.Load(bad,empty);}catch(InvalidDataException){rejected=true;}
            check("unrecoverable-never-overwrite",rejected&&File.ReadAllText(bad)=="{","do not silently replace personal state");
            string local=Path.Combine(root,"local"),legacy=Path.Combine(local,"FluidDock");Directory.CreateDirectory(legacy);File.WriteAllText(Path.Combine(legacy,"apps.json.bak"),"[]");
            check("missing-primary-same-profile",DurableState.ResolveHome(local)==legacy,"recoverable legacy state remains authoritative");
        }catch(Exception ex){check("persistence-fixture",false,ex.ToString());}
        finally{Directory.Delete(root,true);}
    }
}
