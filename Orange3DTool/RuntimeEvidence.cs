using System;
using System.Text.RegularExpressions;
namespace PZ3D {
 static class RuntimeEvidence {
  public static string Evaluate(bool configured,bool loader,string log,bool current){
   if(current){
    int loaded=log.LastIndexOf("[Viewpoint] loaded",StringComparison.OrdinalIgnoreCase);
    var failures=Regex.Matches(log,@"(?im)^.*(?:required mod ""Viewpoint"" not found|\[Viewpoint\].*(?:failed|fatal|exception)).*$");
    if(failures.Count>0 && failures[failures.Count-1].Index>loaded)return "加载失败 · 请查看日志或首次授权提示";
    if(loaded>=0){int reload=log.LastIndexOf("ZomboidFileSystem.loadMods(",StringComparison.Ordinal);if(reload>loaded && log.IndexOf("loading Viewpoint",reload,StringComparison.OrdinalIgnoreCase)<0)return "当前模组清单已切换";return "Viewpoint 已加载 · 按 O 切换视角";}
   }
   if(!configured)return "未启用 · 下次启动不启用 3D";
   if(!loader)return "配置不完整 · 缺少 Buddy 加载文件";
   return "下次启动启用";
  }
 }
}
