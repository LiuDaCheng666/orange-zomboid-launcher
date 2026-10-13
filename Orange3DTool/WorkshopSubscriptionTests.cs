using System;
using System.Reflection;
namespace PZ3D {
 static class WorkshopSubscriptionTests {
  public static void Run(){
   var method=typeof(SteamWorkshop).GetMethod("EnsureSubscribed",BindingFlags.Static|BindingFlags.Public);
   if(method==null)throw new Exception("FAIL: installed-but-unsubscribed component must request and verify a subscription");
   uint state=4;int requests=0;
   Func<ulong,uint> read=id=>state;
   Action<ulong> subscribe=id=>{requests++;state=5;};
   method.Invoke(null,new object[]{3809306528UL,read,subscribe});
   if(requests!=1 || state!=5)throw new Exception("FAIL: absent subscription was not made ready");
   method.Invoke(null,new object[]{3809306528UL,read,subscribe});
   if(requests!=1)throw new Exception("FAIL: subscribed components must not be subscribed again");
   state=4;bool rejected=false;
   try{method.Invoke(null,new object[]{3809306528UL,read,new Action<ulong>(id=>{})});}catch(TargetInvocationException){rejected=true;}
   if(!rejected)throw new Exception("FAIL: failed subscription must not count as installed successfully");
   Console.WriteLine("PASS: installed-only state requests subscription; existing subscriptions preserved; failure rejected");
  }
  public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e.Message);return 1;}}
 }
}
