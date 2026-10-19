using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Win32;

public sealed class InstalledAppInfo {public string Name,Version;public long Bytes;}
public static class InstalledApps {
    public static List<InstalledAppInfo> List(){var items=new List<InstalledAppInfo>();foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})foreach(string branch in new[]{@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"})try{using(var root=hive.OpenSubKey(branch))if(root!=null)foreach(string sub in root.GetSubKeyNames())try{using(var key=root.OpenSubKey(sub)){
        string name=Convert.ToString(key.GetValue("DisplayName")),version=Convert.ToString(key.GetValue("DisplayVersion"));
        if(name==""||Convert.ToString(key.GetValue("SystemComponent"))=="1"||Convert.ToString(key.GetValue("NoRemove"))=="1"||String.IsNullOrWhiteSpace(Convert.ToString(key.GetValue("UninstallString"))))continue;
        string lower=name.ToLowerInvariant();if(new[]{"runtime","redistributable","driver","驱动","visual c++",".net","sdk","framework","update for","security update","windows update"}.Any(lower.Contains))continue;
        long kb;Int64.TryParse(Convert.ToString(key.GetValue("EstimatedSize")),out kb);items.Add(new InstalledAppInfo{Name=name,Version=version,Bytes=Math.Max(0,kb)*1024});
    }}catch{}}catch{}return items.GroupBy(e=>e.Name,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).OrderByDescending(e=>e.Bytes).ThenBy(e=>e.Name).ToList();}
}
