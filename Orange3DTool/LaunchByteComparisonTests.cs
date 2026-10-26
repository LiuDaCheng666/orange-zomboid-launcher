using System;using System.Diagnostics;using System.Reflection;
namespace PZ3D {static class LaunchByteComparisonTests {
 static void Check(bool value,string why){if(!value)throw new Exception("FAIL: "+why);Console.WriteLine("PASS: "+why);}
 public static int Main(){try{var method=typeof(GameStartupComponent).GetMethod("Same",BindingFlags.Static|BindingFlags.NonPublic);Func<byte[],byte[],bool> same=(a,b)=>(bool)method.Invoke(null,new object[]{a,b});
  Check(same(null,null)&&!same(null,new byte[0])&&!same(new byte[0],null),"missing and empty states remain distinct");
  var bytes=new byte[1024*1024];for(int i=0;i<bytes.Length;i++)bytes[i]=(byte)(i*17);var copy=(byte[])bytes.Clone();Check(same(bytes,copy),"equal full payload accepted");
  foreach(int offset in new[]{0,bytes.Length/2,bytes.Length-1}){copy[offset]^=1;Check(!same(bytes,copy),"changed payload byte rejected at "+offset);copy[offset]^=1;}Check(!same(bytes,new byte[bytes.Length-1]),"different lengths rejected");
  same(bytes,bytes);var watch=Stopwatch.StartNew();for(int n=0;n<100;n++)CheckResult(same(bytes,bytes));watch.Stop();Console.WriteLine("same-reference-100-ms="+watch.ElapsedMilliseconds);Check(watch.ElapsedMilliseconds<250,"issue-only updates reuse identical in-memory payload bytes without scanning 100 MiB");return 0;
 }catch(Exception error){Console.WriteLine(error);return 1;}}
 static void CheckResult(bool value){if(!value)throw new Exception("identical reference changed");}
}}
