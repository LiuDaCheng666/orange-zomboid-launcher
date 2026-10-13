using System;
using System.Reflection;
namespace PZ3D {
 static class HardwareProbeTests {
  static void Check(bool value,string label){Console.WriteLine((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
  public static int Main(){try{Run();return 0;}catch(Exception ex){Console.WriteLine(ex.Message);return 1;}}
  public static void Run(){
   Type type=typeof(Engine).Assembly.GetType("PZ3D.HardwareProbe");Check(type!=null,"read-only hardware probe exists");var parse=type.GetMethod("ParseNvidia",BindingFlags.Public|BindingFlags.Static);
   Func<string,object> read=text=>parse.Invoke(null,new object[]{text});Func<object,string,object> field=(value,name)=>value.GetType().GetField(name).GetValue(value);
   object gpu=read("NVIDIA GeForce RTX 4090, 24576, 576.80\n");Check(Convert.ToString(field(gpu,"gpu"))=="NVIDIA GeForce RTX 4090"&&Convert.ToDouble(field(gpu,"vramGiB"))==24&&Convert.ToString(field(gpu,"driver"))=="576.80","NVIDIA literal 24576 MiB is reported as 24 GiB with driver");
   object bad=read("not a GPU result\nNVIDIA Unknown, NaN, 576.80\n");Check(field(bad,"vramGiB")==null&&Convert.ToString(field(bad,"warning")).Length>0,"malformed NVIDIA output reports unknown VRAM");
   object multiple=read("NVIDIA Invalid, N/A, 576.80\nNVIDIA Valid, 8192, 576.80\nNVIDIA Other, 16384, 576.80\n");Check(Convert.ToString(field(multiple,"gpu"))=="NVIDIA Valid"&&Convert.ToDouble(field(multiple,"vramGiB"))==8,"multiple adapters choose first complete valid record");
   object quoted=read("\"NVIDIA GPU, Test\", 12288, 576.80\n");Check(Convert.ToString(field(quoted,"gpu"))=="NVIDIA GPU, Test"&&Convert.ToDouble(field(quoted,"vramGiB"))==12,"quoted adapter names do not corrupt CSV capacity fields");
   Check(field(read("NVIDIA Broken, -4096, 576.80"),"vramGiB")==null&&field(read("NVIDIA Broken, Infinity, 576.80"),"vramGiB")==null,"negative or non-finite capacities remain unknown");
   object unknown=read("");Check(field(unknown,"vramGiB")==null&&Convert.ToString(field(unknown,"driver"))=="","missing output does not invent capacity or driver");
  }
 }
}
