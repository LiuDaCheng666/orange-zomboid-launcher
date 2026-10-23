using System;
using System.Collections.Generic;
namespace PZ3D {
 static class LauncherContact {
  internal const string GroupNumber="1124858561";
  internal const string InviteUrl="https://qm.qq.com/q/cuLGbwUx4A";
  internal static string Resolve(Dictionary<string,object> payload){if(payload==null||payload.Count!=0)throw new InvalidOperationException("联系入口不接受网址或路径参数。");return InviteUrl;}
  internal static void Open(Dictionary<string,object> payload,bool test,Action<string> open){string target=Resolve(payload);if(test)throw new InvalidOperationException("测试模式禁止打开外部页面。");open(target);}
 }
}
