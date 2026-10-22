using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;

namespace PZ3D {
 static class CoreReleaseSourcesTests {
  const string Latest="https://orangezl.com/updates/latest/update-manifest.json";
  const string Fixed="https://orangezl.com/releases/v9.8.7/";
  static object Call(string name,params object[] args) {
   var type=typeof(CoreFixture).Assembly.GetType("PZ3D.CoreReleaseSources");
   CoreFixture.Check(type!=null,"own HTTPS release source is implemented");
   try{return type.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}
  }
  static byte[][] Lookup(CoreManifest target,Func<string,CancellationToken,Stream> open,Func<CancellationToken,byte[][]> github,CancellationToken token) {
   return (byte[][])Call("Lookup",token,open,github,CoreFixture.Key,AppVersion.Current);
  }
  static string[] Urls(string url){return (string[])Call("AssetUrls",url);}
  static byte[][] Envelope(CoreManifest target){return new[]{target.VerifiedEnvelope.Raw,target.VerifiedEnvelope.Signature};}
  internal static void Run() {
   var target=CoreFixture.Manifest("9.8.7",CoreFixture.B());var calls=new List<string>();
   var envelope=Lookup(target,(url,token)=>{calls.Add(url);if(url==Latest)return new MemoryStream(target.VerifiedEnvelope.Raw);if(url==Fixed+"update-manifest.sig")return new MemoryStream(target.VerifiedEnvelope.Signature);throw new Exception("FAIL: unexpected own route "+url);},token=>{throw new Exception("FAIL: healthy own source called GitHub");},CancellationToken.None);
   CoreFixture.Check(calls.SequenceEqual(new[]{Latest,Fixed+"update-manifest.sig"}),"latest manifest selects immutable matching signature, not racing latest signature");
   CoreFixture.Check(CoreReleaseProtocol.Verify(envelope[0],envelope[1],CoreFixture.Key,AppVersion.Current).version=="9.8.7","own source preserves authentic envelope");
   foreach(bool failSignature in new[]{false,true}) {
    int fallback=0;envelope=Lookup(target,(url,token)=>{if(!failSignature||url.EndsWith(".sig"))throw new WebException("fixture unavailable",WebExceptionStatus.ConnectFailure);return new MemoryStream(target.VerifiedEnvelope.Raw);},token=>{fallback++;return Envelope(target);},CancellationToken.None);
    CoreFixture.Check(fallback==1&&envelope[0].SequenceEqual(target.VerifiedEnvelope.Raw),"transport failure falls back to authentic GitHub metadata");
   }
   int olderFallback=0;var older=CoreFixture.Manifest("0.1.0",CoreFixture.B());
   Lookup(older,(url,token)=>new MemoryStream(url==Latest?older.VerifiedEnvelope.Raw:older.VerifiedEnvelope.Signature),token=>{olderFallback++;return Envelope(target);},CancellationToken.None);
   CoreFixture.Check(olderFallback==1,"authenticated lagging mirror consults GitHub rather than replaying old version");
   foreach(bool oversized in new[]{false,true}) {
    int fallback=0;
    CoreFixture.Reject(()=>Lookup(target,(url,token)=>new MemoryStream(oversized?new byte[CoreReleaseProtocol.MaxManifestBytes+1]:url==Latest?target.VerifiedEnvelope.Raw:new byte[256]),token=>{fallback++;return Envelope(target);},CancellationToken.None),"bad own signature or oversized response is rejected");
    CoreFixture.Check(fallback==0,"integrity failure is not hidden by GitHub fallback");
   }
   using(var cancel=new CancellationTokenSource()) {
    int fallback=0;cancel.Cancel();
    CoreFixture.Reject(()=>Lookup(target,(url,token)=>{throw new Exception("FAIL: canceled lookup opened network");},token=>{fallback++;return Envelope(target);},cancel.Token),"canceled lookup stops");
    CoreFixture.Check(fallback==0,"cancellation does not fall back");
   }
   string github=CoreReleaseProtocol.ReleaseBase+"v9.8.7/app.js";
   CoreFixture.Check(Urls(github).SequenceEqual(new[]{Fixed+"app.js",github}),"validated signed GitHub asset maps to own immutable path first");
   foreach(string bad in new[]{"https://evil.example/a","https://github.com/LiuDaCheng666/other/releases/download/v9.8.7/a",CoreReleaseProtocol.ReleaseBase+"v9.8.7/../a",github+"?x=1"})CoreFixture.Reject(()=>Urls(bad),"unsafe signed URL mapping refused");
   CoreFixture.Check(CoreReleaseNetwork.Allowed(new Uri(Latest))&&CoreReleaseNetwork.Allowed(new Uri(Fixed+"app.js")),"own metadata and immutable file are allowed");
   foreach(string bad in new[]{"http://orangezl.com/releases/v9.8.7/app.js","https://www.orangezl.com/releases/v9.8.7/app.js","https://orangezl.com/admin","https://orangezl.com/releases/v9.8.7/a?x=1","https://orangezl.com:444/updates/latest/update-manifest.json"})CoreFixture.Check(!CoreReleaseNetwork.Allowed(new Uri(bad)),"own transport rejects unsafe destination");
   Download(target,false,false,true);Download(target,false,false);Download(target,true,false);Download(target,false,true);
  }
  static void Until(CoreUpdateService service,string state){var end=DateTime.UtcNow.AddSeconds(10);while(service.Status().state!=state){if(DateTime.UtcNow>end)throw new Exception("FAIL: expected "+state+", got "+service.Status().state+": "+service.Status().message);Thread.Sleep(10);}}
  static void Download(CoreManifest target,bool corrupt,bool cancel,bool healthy=false) {
   string root=CoreFixture.Root();CoreDeployment.Install(root,CoreFixture.Manifest(AppVersion.Current,CoreFixture.A),CoreFixture.A,CoreFixture.Key);
   int own=0,github=0;var received=new List<long>();CoreUpdateService service=null;
   Func<string,CancellationToken,Stream> open=(url,token)=>{
    if(url.StartsWith("https://orangezl.com/",StringComparison.Ordinal)){own++;return healthy?new MemoryStream(CoreFixture.Bytes("new-js")):corrupt?new MemoryStream(CoreFixture.Bytes("bad-js")):new PartialStream(token,cancel?new Action(()=>service.Cancel()):null);}
    github++;return new MemoryStream(CoreFixture.Bytes("new-js"));
   };
   Func<CancellationToken,byte[][]> lookup=token=>Envelope(target);Func<string,string[]> sources=Urls;
   service=(CoreUpdateService)Activator.CreateInstance(typeof(CoreUpdateService),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{root,CoreFixture.Key,lookup,open,sources},null);
   using(service){service.Progress=s=>{if(s.state=="Downloading")received.Add(s.receivedBytes);};service.Check();Until(service,"Available");service.Download();Until(service,cancel?"Cancelled":corrupt?"Error":"Ready");
    CoreFixture.Check(own==1&&github==(healthy||corrupt||cancel?0:1),"healthy own download does not touch GitHub; fallback only for transport");
    CoreFixture.Same(root,CoreFixture.A,"source fallback preserves installed files until restart");
    if(!healthy&&!corrupt&&!cancel)CoreFixture.Check(service.Status().receivedBytes==6&&received.Contains(2)&&received.Contains(0),"partial mirror stream resets before full verified GitHub retry");
   }
  }
  sealed class PartialStream:MemoryStream {
   readonly CancellationToken token;readonly Action cancel;bool first=true;
   internal PartialStream(CancellationToken token,Action cancel):base(CoreFixture.Bytes("ne")){this.token=token;this.cancel=cancel;}
   public override int Read(byte[] bytes,int offset,int count){if(first){first=false;return base.Read(bytes,offset,count);}if(cancel!=null){cancel();token.ThrowIfCancellationRequested();}throw new IOException("fixture mid-stream transport loss");}
  }
 }
}
