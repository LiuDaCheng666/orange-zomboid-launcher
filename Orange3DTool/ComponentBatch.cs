using System;
using System.Collections.Generic;
namespace PZ3D {
 static class ComponentBatch {
  public static string Run(Dictionary<string,Func<string>> work){var result=new List<string>();int failed=0;foreach(var item in work){try{result.Add("[完成] "+item.Key+"\r\n"+item.Value());}catch(Exception ex){failed++;result.Add("[失败] "+item.Key+"\r\n"+ex.Message);}}return (failed==0?"所选组件操作完成。":"部分完成："+failed+" 个组件失败；其他组件已独立继续，详见逐项结果。")+"\r\n"+string.Join("\r\n\r\n",result);}
 }
}
