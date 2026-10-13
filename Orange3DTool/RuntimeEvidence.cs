using System;
using System.Text.RegularExpressions;
namespace PZ3D {
 static class RuntimeEvidence {
  public static string Evaluate(bool configured,bool loader,string log,bool current){
   if(current){
    int loaded=log.LastIndexOf("[Viewpoint] loaded",StringComparison.OrdinalIgnoreCase);
    var failures=Regex.Matches(log,@"(?im)^.*(?:required mod ""Viewpoint"" not found|\[Viewpoint\].*(?:failed|fatal|exception)).*$");
    if(failures.Count>0 && failures[failures.Count-1].Index>loaded)return "加载失败 · 请查看日志或首次授权提示";
    if(loaded>=0){int reload=log.LastIndexOf("ZomboidFileSystem.loadMods(",StringComparison.Ordinal);if(reload>loaded && log.IndexOf("loading Viewpoint",reload,StringComparison.OrdinalIgnoreCase)<0)return "进服后待验证 · 不能沿用主菜单的加载结果";return "本次游戏已加载 Viewpoint · 视角是否开启请在游戏按 O 确认";}
   }
   if(!configured)return "未启用 · 下次启动不启用 3D";
   if(!loader)return "配置不完整 · 缺少 Buddy 加载文件";
   return "已配置 · 本次游戏加载待验证";
  }
 }
}
