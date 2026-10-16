using System;
using System.Collections.Generic;
namespace PZ3D {
 static class ComponentBatchTests {
  public static void Run(){var t=typeof(Engine).Assembly.GetType("PZ3D.ComponentBatch");if(t==null)throw new Exception("FAIL: component failure isolation missing");bool chinese=false;var work=new Dictionary<string,Func<string>>{{"枪械",()=>{throw new Exception("fixture shooting error");}},{"汉化",()=>{chinese=true;return "Chinese installed";}}};string result=(string)t.GetMethod("Run").Invoke(null,new object[]{work});if(!chinese||!result.Contains("Chinese installed")||!result.Contains("fixture shooting error"))throw new Exception("FAIL: shooting failure prevents Chinese or hides error");Console.WriteLine("PASS: shooting failure does not skip Chinese and both outcomes are reported");}
 }
}
