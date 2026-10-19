using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

public partial class Dock {
    bool SmokeUi {get{return Environment.GetCommandLineArgs().Contains("--smoke-ui");}}
    void RunUiSmoke(){
        var checks=new Dictionary<string,bool>();int phase=0;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(650)};
        timer.Tick+=(s,e)=>{try{
            if(phase==0){string startup=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Fluid Dock.lnk");byte[] original=File.Exists(startup)?File.ReadAllBytes(startup):new byte[0];SyncStartup();byte[] after=File.Exists(startup)?File.ReadAllBytes(startup):new byte[0];checks["isolatedStartupUnchanged"]=original.SequenceEqual(after);checks["modelLoaded"]=intent.ModelAvailable;var app=installed.First();Execute(new DockPlan{Intent="add",AppName=app.Name,TargetKey=AppCatalog.Key(app)});checks["addFlow"]=Entries.Any(a=>a.Name==app.Name&&a.Pinned);}
            else if(phase==1){var app=installed.Last();Execute(new DockPlan{Intent="add",AppName=app.Name,TargetKey=AppCatalog.Key(app)});checks["secondAdd"]=Entries.Any(a=>a.Name==app.Name&&a.Pinned);}
            else if(phase==2){Execute(new DockPlan{Intent="move",AppName=installed.Last().Name,OtherName=installed.First().Name,Direction="before"});checks["moveFlow"]=Entries.FindIndex(a=>a.Name==installed.Last().Name)<Entries.FindIndex(a=>a.Name==installed.First().Name);}
            else if(phase==3){OpenManager("assistant");search.SmokeCommand("图标小一点");checks["assistantSize"]=Math.Abs(Settings.DockScale-.9)<.001;}
            else if(phase==4){search.SmokeCommand("换成浅色主题");checks["assistantTheme"]=Settings.Appearance=="light";}
            else if(phase==5){search.SmokeCommand("撤销刚才的操作");checks["undoTheme"]=Settings.Appearance=="glass";search.SetMode("assistant");search.SmokePreview("把"+installed.Last().Name+"放到最前面");}
            else if(phase==6){Capture(search,"assistant-preview.png");search.Dismiss();Execute(new DockPlan{Intent="remove",AppName=installed.Last().Name});checks["removeFlow"]=!Entries.First(a=>a.Name==installed.Last().Name).Pinned;}
            else if(phase==7){Execute(new DockPlan{Intent="undo"});checks["undoRemove"]=Entries.First(a=>a.Name==installed.Last().Name).Pinned;}
            else if(phase==8){Capture(this,"dock-preview.png");var setup=new SetupWindow(this);setup.Show();checks["usageConsentDefaultOff"]=!setup.UsageConsent;Capture(setup,"setup-preview.png");setup.Close();}
            else if(phase==9){var care=new CareWindow(this);care.Show();checks["cleanupDefaultNoSelection"]=!care.HasSelection;Capture(care,"care-preview.png");care.Close();timer.Stop();Atomic("ui-smoke.json",new{passed=checks.Values.All(x=>x),checks=checks});Environment.ExitCode=checks.Values.All(x=>x)?0:1;Close();}
            phase++;
        }catch(Exception ex){timer.Stop();Atomic("ui-smoke.json",new{passed=false,error=ex.ToString(),phase=phase});Environment.ExitCode=1;Close();}};timer.Start();
    }
}
public partial class SearchPanel {
    public void SmokeCommand(string query){SetMode("assistant");input.Text=query;RunAssistant();}
    public void SmokePreview(string query){SetMode("assistant");input.Text=query;}
}
