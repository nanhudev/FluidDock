using System;
using System.Text.RegularExpressions;
public static class Websites {
    public static Entry Resolve(string query){
        string n=IntentModel.Normalize(query);string name=null,url=null;
        if(n.Contains("ummoodle")||n.Contains("澳门大学moodle")){name="UM Moodle";url="https://ummoodle.um.edu.mo/";}
        else if(n.Contains("siweb")||n.Contains("澳门大学学生系统")){name="SI Web";url="https://isw.um.edu.mo/siweb/";}
        else{var match=Regex.Match(query,@"https?://[^\s<>""，。]+",RegexOptions.IgnoreCase);Uri uri;if(match.Success&&Uri.TryCreate(match.Value,UriKind.Absolute,out uri)&&uri.UserInfo==""&&(uri.Scheme=="https"||uri.Scheme=="http")){name=uri.Host;url=uri.AbsoluteUri;}}
        return url==null?null:new Entry{Name=name,Target=url,Arguments="",WorkDir="",Icon="",Group="常用网站",CanonicalId=url,Pinned=false};
    }
}
