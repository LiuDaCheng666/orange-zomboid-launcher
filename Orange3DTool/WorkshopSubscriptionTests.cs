using System;
using System.Reflection;
namespace PZ3D {
 static class WorkshopSubscriptionTests {
  public static void Run(){
   var method=typeof(SteamWorkshop).GetMethod("EnsureSubscribed",new[]{typeof(ulong),typeof(Func<ulong,uint>),typeof(Action<ulong>)});
   if(method==null)throw new Exception("FAIL: installed-but-unsubscribed component must request and verify a subscription");
   uint state=4;int requests=0;
   Func<ulong,uint> read=id=>state;
   Action<ulong> subscribe=id=>{requests++;state=5;};
   method.Invoke(null,new object[]{3809306528UL,read,subscribe});
   if(requests!=1 || state!=5)throw new Exception("FAIL: absent subscription was not made ready");
   method.Invoke(null,new object[]{3809306528UL,read,subscribe});
   if(requests!=1)throw new Exception("FAIL: subscribed components must not be subscribed again");
   state=4;bool rejected=false;
   try{method.Invoke(null,new object[]{3809306528UL,read,new Action<ulong>(id=>{throw new Exception("synthetic subscription rejected");})});}catch(TargetInvocationException){rejected=true;}
   if(!rejected)throw new Exception("FAIL: failed subscription must not count as installed successfully");
   int reads=0;requests=0;
   SteamWorkshop.EnsureSubscribed(3810637496UL,id=>++reads>=3?5U:4U,id=>requests++);
   if(requests!=1||reads<3)throw new Exception("FAIL: successful subscribe tolerates delayed client flag");
   Console.WriteLine("PASS: installed-only state requests subscription; existing subscriptions preserved; failure rejected; delayed flag accepted");
  }
  public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e.Message);return 1;}}
 }
}
