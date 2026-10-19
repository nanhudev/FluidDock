using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

public static class ProductTests {
    sealed class Case {public string Text,Intent,App;public Case(string text,string intent,string app=""){Text=text;Intent=intent;App=app;}}
    public static int Run(string output){
        var rows=new List<object>();int failures=0;var watch=Stopwatch.StartNew();
        Action<string,bool,string> check=(name,ok,detail)=>{rows.Add(new{name=name,passed=ok,detail=detail});if(!ok)failures++;};
        var catalog=new List<Entry>{
            new Entry{Name="三角洲",Target=@"Z:\Games\DeltaForce\WeGameLauncher\launcher.exe",Group="游戏"},
            new Entry{Name="微信",Target=@"Z:\Apps\Weixin.exe",Group="办公",Pinned=true},
            new Entry{Name="QQ",Target=@"Z:\Apps\QQ.exe",Group="办公",Pinned=true},
            new Entry{Name="GPT",Target=@"Z:\Apps\ChatGPT.exe",Group="AI",Pinned=true},
            new Entry{Name="Steam",Target=@"Z:\Apps\Steam.exe",Group="游戏",Pinned=true},
            new Entry{Name="Blender",Target=@"Z:\Apps\blender.exe",Group="办公",Pinned=true},
            new Entry{Name="v2RayN",Target=@"Z:\Apps\v2rayN.exe",Group="其他工具",Pinned=true}
        };
        var engine=new DockIntent();check("embedded-model",engine.ModelAvailable,"real trained model must load from the executable");
        catalog.Add(new Entry{Name="Feishu",Target=@"Z:\Apps\Feishu\Feishu.exe",Group=AppCatalog.GuessGroup("Feishu")});
        var cases=new[]{
            new Case("帮我加一个三角洲","add","三角洲"),new Case("帮我加一个酸角洲","add","三角洲"),new Case("帮我加一个suanjiaozhou","add","三角洲"),new Case("帮我加一个deltafoce","add","三角洲"),new Case("添加Delta Force","add","三角洲"),
            new Case("把微信固定到Dock","add","微信"),new Case("把Steam加上","add","Steam"),new Case("pin v2ray","add","v2RayN"),new Case("微信留在这里","add","微信"),
            new Case("从Dock移除QQ","remove","QQ"),new Case("把微信扔掉","remove","微信"),new Case("取消固定Steam","remove","Steam"),
            new Case("把GPT放到最前面","move","GPT"),new Case("把QQ挪到微信后面","move","QQ"),new Case("把Steam移到最后","move","Steam"),
            new Case("把Blender归到办公","group","Blender"),new Case("把QQ放到游戏分类","group","QQ"),
            new Case("按使用频率排序","sort"),new Case("把游戏分类放到办公前面","sort"),
            new Case("换成石墨风格","theme"),new Case("切换浅色主题","theme"),new Case("我要深色磨砂","theme"),
            new Case("图标小一点","size"),new Case("把尺寸调到80%","size"),new Case("make dock smaller","size"),
            new Case("开启自动隐藏","autohide"),new Case("不要自动收起","autohide"),new Case("开机自动启动","startup"),new Case("取消开机自启","startup"),
            new Case("显示时钟小组件","widget"),new Case("隐藏桌面时钟","widget"),new Case("开启使用统计","tracking"),new Case("关闭点击统计","tracking"),
            new Case("撤销刚才的操作","undo"),new Case("微信的exe在哪里","search","微信"),new Case("你能做什么","help"),
            new Case("帮我卸载QQ","unknown"),new Case("执行powershell命令","unknown"),new Case("删除磁盘上的文件","unknown"),new Case("不要删除QQ","unknown"),new Case("别缩小图标","unknown"),new Case("不要关闭自动隐藏","unknown"),
            new Case("添加QQ和微信","batch"),new Case("图标设置成500%","unknown"),new Case("添加一个不存在的软件xyz987","unknown"),new Case("今天天气怎么样","unknown")
        };
        foreach(var c in cases){var p=engine.Parse(c.Text,catalog);check("intent:"+c.Text,p.Intent==c.Intent&&(c.App==""||p.AppName==c.App),p.Intent+" / "+p.AppName+" / "+p.Source+" / "+p.Message);}
        foreach(var c in new[]{new Case("帮我打开微信","open","微信"),new Case("开一下QQ","open","QQ"),new Case("关掉微信","close","微信"),new Case("关闭时钟小组件","widget"),new Case("打开时钟小组件","widget"),new Case("帮我把飞书加入","add","Feishu"),new Case("帮我加Feishu","add","Feishu"),new Case("清理C盘","care"),new Case("清理大文件","care"),new Case("帮忙整理桌面","care"),new Case("不要清理C盘","unknown"),new Case("一键配置dock","configure"),new Case("添加moodle","unknown")}){var p=engine.Parse(c.Text,catalog);check("extended-intent:"+c.Text,p.Intent==c.Intent&&(c.App==""||p.AppName==c.App),p.Intent+" / "+p.AppName);}
        foreach(var item in new[]{Tuple.Create("UU远程","办公"),Tuple.Create("uuremote","办公"),Tuple.Create("Feishu","办公"),Tuple.Create("飞书","办公"),Tuple.Create("Blender","开发"),Tuple.Create("UnrealEditor","开发"),Tuple.Create("UE5","开发"),Tuple.Create("Cubase","音乐"),Tuple.Create("网易云音乐","音乐")})check("category:"+item.Item1,AppCatalog.GuessGroup(item.Item1)==item.Item2,AppCatalog.GuessGroup(item.Item1));
        foreach(var item in new[]{Tuple.Create("帮我把um moodle加入到dock","https://ummoodle.um.edu.mo/"),Tuple.Create("把si web加入dock","https://isw.um.edu.mo/siweb/"),Tuple.Create("添加 https://example.com/","https://example.com/")}){var p=engine.Parse(item.Item1,catalog);var entries=new List<Entry>();CommandActions.Apply(p,entries,new Prefs(),catalog);check("website:"+item.Item1,p.Intent=="add"&&entries.Count==1&&entries[0].Target==item.Item2&&entries[0].Group=="常用网站"&&entries[0].Pinned,p.Intent);}
        check("no-unsafe-website",Websites.Resolve("添加 javascript:alert(1)")==null&&Websites.Resolve("添加 file:///C:/Windows/system.ini")==null,"only http(s) websites");
        check("usage-consent-default",Recommendations.ReadUsage(false).Count==0,"no consent means no UserAssist read");
        var multi=new List<Entry>{new Entry{Name="Cursor",Target=@"Z:\Apps\Cursor.exe",Group="AI"},new Entry{Name="WorkBuddy",Target=@"Z:\Apps\WorkBuddy.exe",Group="AI"},new Entry{Name="Blender",Target=@"Z:\Apps\blender.exe",Group="开发"},new Entry{Name="UE5",Target=@"Z:\Apps\UnrealEditor.exe",Group="开发"}};
        var composite=engine.Parse("帮我再加一下 Cursor 和 WorkBuddy",multi);var compositeState=new List<Entry>();CommandActions.Apply(composite,compositeState,new Prefs(),multi);check("two-app-add",composite.Intent=="batch"&&compositeState.Count==2&&compositeState.All(e=>e.Pinned),"two recognized apps, one reviewed operation");
        var grouping=engine.Parse("把 Blender 新开一个开发分组，UE5 也放进去",multi);CommandActions.Apply(grouping,compositeState,new Prefs(),multi);check("create-development-section",grouping.Intent=="batch"&&compositeState.Count(e=>e.Group=="开发"&&e.Pinned)==2,"create a visible group by adding its first apps");
        int beforeComposite=compositeState.Count;try{CommandActions.Apply(new DockPlan{Intent="batch",Steps=new List<DockPlan>{new DockPlan{Intent="add",AppName="Fixture",ResolvedEntry=new Entry{Name="Fixture",Target=@"Z:\Fixture.exe"}},new DockPlan{Intent="group",AppName="absent",Group="开发"}}},compositeState,new Prefs(),multi);}catch{}check("batch-atomic",compositeState.Count==beforeComposite&&!compositeState.Any(e=>e.Name=="Fixture"),"invalid second action leaves the first unapplied");
        foreach(string name in new[]{"Steinberg Download Assistant","ASIO Driver","Visual C++ Redistributable","应用安装器","Setup.exe","声卡控制面板"})check("recommend-exclusion:"+name,!Recommendations.HumanApp(new Entry{Name=name,Target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"notepad.exe")}),"background infrastructure must not be recommended");
        try{
            var fixture=Path.Combine(output,"care-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);var file=Path.Combine(fixture,"my-paper.txt");File.WriteAllText(file,"keep my work");var streamed=new List<CareItem>();var report=CareScan.Scan("desktop",fixture,()=>false,i=>streamed.Add(i));check("care-stream-matches-final",streamed.Count==1&&streamed[0]==report.Items[0],"stream only discovered items before final report");check("care-scan-read-only",File.Exists(file)&&report.Items.Count==1&&!Directory.Exists(Path.Combine(fixture,"Dock 整理")),"scan must not mutate");
            var item=report.Items.Single();File.AppendAllText(file,"changed after scan");check("care-revalidate",!CareScan.SameFile(item)&&CareScan.Apply(item,fixture).StartsWith("跳过"),"changed files must be skipped");
            check("care-root-boundary",!CareScan.Within(Path.Combine(fixture+"-sibling","my-paper.txt"),fixture)&&CareScan.Apply(item,Path.Combine(fixture,"child")).StartsWith("跳过"),"prefix siblings must not pass path containment");
            check("care-protect-system",CareScan.Protected(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","anything.bin")),"system folders excluded");
            check("care-protect-program",CareScan.Protected(Path.Combine(fixture,"tool.exe")),"installed app executables excluded from large file cleanup");
            var fresh=CareScan.Scan("desktop",fixture,()=>false).Items.Single();check("care-desktop-archive",CareScan.Apply(fresh,fixture)=="已移动"&&!File.Exists(file)&&File.Exists(fresh.Destination),"authorized fixture movement preserves source bytes");
            File.Move(fresh.Destination,file);var found=ComputerSearch.Find("my-paper",new[]{fixture},()=>false);check("global-file-fallback",found.Entries.Any(e=>e.Target==file),"indexed search or bounded personal-folder fallback must find the fixture");File.Delete(file);if(CareScan.Within(fixture,output))Directory.Delete(fixture,true);
        }catch(Exception ex){check("care-fixture",false,ex.Message);}
        check("auto-hide-enable",engine.Parse("开启自动隐藏",catalog).Enable,"enable must not be confused by the word 隐藏");
        check("auto-hide-disable",!engine.Parse("一直显示Dock",catalog).Enable,"explicit disable");
        check("widget-disable",!engine.Parse("隐藏桌面时钟",catalog).Enable,"widget hiding");
        var settings=new Prefs();var state=Dock.Json.Deserialize<List<Entry>>(Dock.Json.Serialize(catalog));
        var add=engine.Parse("帮我加一个酸角洲",catalog);CommandActions.Apply(add,state,settings,catalog);
        check("delta-auto-group",state.First(x=>x.Name=="三角洲").Pinned&&state.First(x=>x.Name=="三角洲").Group=="游戏","fuzzy resolution must preserve the game's category");
        CommandActions.Apply(engine.Parse("把QQ移到微信后面",catalog),state,settings,catalog);
        check("relative-order",state.FindIndex(x=>x.Name=="QQ")==state.FindIndex(x=>x.Name=="微信")+1,"relative order must be exact");
        CommandActions.Apply(engine.Parse("移除QQ",catalog),state,settings,catalog);check("remove-does-not-uninstall",!state.First(x=>x.Name=="QQ").Pinned&&state.First(x=>x.Name=="QQ").Target==catalog.First(x=>x.Name=="QQ").Target,"only the pin is removed");
        check("cancel-drag",DockDragPolicy.Decide(new Rect(100,100,500,80),new Point(50,20),true)=="cancel","escape/lost capture never delete a pin");
        check("drag-out",DockDragPolicy.Decide(new Rect(100,100,500,80),new Point(50,20),false)=="remove","release outside padded dock removes the pin");
        check("drag-reorder",DockDragPolicy.Decide(new Rect(100,100,500,80),new Point(150,120),false)=="move","release on dock reorders");
        var shortcutOverride=Dock.Json.Deserialize<Entry>(Dock.Json.Serialize(catalog[1]));shortcutOverride.WorkDir=@"Z:\Custom Working Folder";shortcutOverride.Arguments="--original-argument";
        CommandActions.Apply(new DockPlan{Intent="add",AppName=shortcutOverride.Name,ResolvedEntry=shortcutOverride},state,settings,catalog);
        check("explicit-shortcut-workdir",state.First(x=>x.Name=="微信").WorkDir==shortcutOverride.WorkDir&&state.First(x=>x.Name=="微信").Arguments==shortcutOverride.Arguments,"explicit file drop must not be replaced by an indexed shortcut with different arguments");
        try{
            string exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"notepad.exe"),link=Path.Combine(output,"shortcut-fixture.lnk");
            dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));dynamic shortcut=shell.CreateShortcut(link);shortcut.TargetPath=exe;shortcut.Arguments="/A test-document.txt";shortcut.WorkingDirectory=output;shortcut.Save();System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            var resolved=AppCatalog.FromFile(link);check("shortcut-target",String.Equals(resolved.Target,exe,StringComparison.OrdinalIgnoreCase),resolved.Target);check("shortcut-arguments",resolved.Arguments=="/A test-document.txt",resolved.Arguments);check("shortcut-working-directory",String.Equals(resolved.WorkDir,output,StringComparison.OrdinalIgnoreCase),resolved.WorkDir);File.Delete(link);
        }catch(Exception ex){check("shortcut-resolution",false,ex.GetType().Name+": "+ex.Message);}
        try{
            string folder=Path.Combine(output,"migration-fixture"),a=Path.Combine(folder,"old"),b=Path.Combine(folder,"new");Directory.CreateDirectory(a);Directory.CreateDirectory(b);
            string old=Path.Combine(a,"fixture.exe"),moved=Path.Combine(b,"fixture.exe");File.WriteAllBytes(old,new byte[]{77,90,13,10,1,2,3,4});
            var entry=new Entry{Name="Fixture Tool",Target=old,Arguments="--keep-argument",WorkDir=a,Group="其他工具",Fingerprint=EntryBindings.Fingerprint(old)};
            File.Move(old,moved);var repaired=EntryBindings.Resolve(entry,new Entry[0],moved,null);
            check("rename-migration",repaired.Repaired&&repaired.Entry.Target==moved&&repaired.Entry.Arguments=="--keep-argument"&&repaired.Entry.WorkDir==b,"rename hint must retain arguments and update the base working directory");
            string wrong=Path.Combine(a,"fixture.exe");File.WriteAllBytes(wrong,new byte[]{77,90,99,98,97});
            var missing=EntryBindings.Resolve(new Entry{Name=entry.Name,Target=Path.Combine(a,"gone.exe"),Fingerprint=entry.Fingerprint},new Entry[0],wrong,null);
            check("reject-wrong-executable",missing.Status=="missing","same-looking file name must not override executable identity");
            var fromCatalog=EntryBindings.Resolve(entry,new[]{new Entry{Name=entry.Name,Target=moved,Arguments=entry.Arguments,WorkDir=b,Group=entry.Group}},null,null);
            check("catalog-migration",fromCatalog.Repaired&&fromCatalog.Entry.Target==moved,"install catalog repairs moved entries using identity");
            File.Delete(moved);File.Delete(wrong);Directory.Delete(a);Directory.Delete(b);Directory.Delete(folder);
        }catch(Exception ex){check("migration-fixture",false,ex.Message);}
        WorkspaceTests.Run(check);PersistenceTests.Run(output,check);SystemIntentTests.Run(check);
        var model=new IntentModel();var predictions=new List<object>();foreach(string q in new[]{"软件收上来","微信留在这里","这个图标先撤下","再紧凑一点","帮我写封邮件"}){var p=model.Predict(q);predictions.Add(new{text=q,intent=p.Intent,confidence=p.Confidence,margin=p.Margin});}
        File.WriteAllText(Path.Combine(output,"self-test.json"),Dock.Json.Serialize(new{passed=failures==0,checks=rows.Count,failures=failures,elapsedMs=watch.Elapsed.TotalMilliseconds,cases=rows,modelProbes=predictions}),System.Text.Encoding.UTF8);
        return failures==0?0:1;
    }
}
