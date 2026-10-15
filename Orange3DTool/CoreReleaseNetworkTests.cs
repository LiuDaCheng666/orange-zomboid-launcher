using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace PZ3D {
 static class CoreReleaseNetworkTests {
  const string Api="https://api.github.com/repos/LiuDaCheng666/orange-zomboid-launcher/releases/latest";
  const string Fixed="https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/download/v9.8.7/";
  const string Alias="https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/latest/download/";
  internal static void Run() {
   var target=CoreFixture.Manifest("9.8.7",CoreFixture.B());
   var calls=new List<string>();
   var envelope=CoreReleaseNetwork.Lookup(CancellationToken.None,(url,token)=>{
    calls.Add(url);
    if(url==Api)return new MemoryStream(CoreFixture.Bytes("{\"draft\":false,\"prerelease\":false,\"tag_name\":\"v9.8.7\"}"));
    if(url==Fixed+"update-manifest.json")return new MemoryStream(target.VerifiedEnvelope.Raw);
    if(url==Fixed+"update-manifest.sig")return new MemoryStream(target.VerifiedEnvelope.Signature);
    throw new Exception("FAIL: unexpected network route "+url);
   });
   CoreFixture.Check(calls.SequenceEqual(new[]{Api,Fixed+"update-manifest.json",Fixed+"update-manifest.sig"}),"normal API selects only its fixed release tag");
   CoreFixture.Check(CoreReleaseProtocol.Verify(envelope[0],envelope[1],CoreFixture.Key,AppVersion.Current).version=="9.8.7","normal network envelope passes production signature verification");
   Fallback(403,target);
   Fallback(429,target);
   ApiFailures();
   AliasFailures(target);
   Cancellation(target);
   Unauthenticated(target);
  }
  static void Fallback(int code,CoreManifest target) {
   var calls=new List<string>();
   var envelope=CoreReleaseNetwork.Lookup(CancellationToken.None,(url,token)=>{
    calls.Add(url);
    if(url==Api)throw HttpError(code);
    if(url==Alias+"update-manifest.json")return new MemoryStream(target.VerifiedEnvelope.Raw);
    if(url==Alias+"update-manifest.sig")return new MemoryStream(target.VerifiedEnvelope.Signature);
    throw new Exception("FAIL: unexpected fallback route "+url);
   });
   CoreFixture.Check(calls.SequenceEqual(new[]{Api,Alias+"update-manifest.json",Alias+"update-manifest.sig"}),"API HTTP "+code+" selects exactly the two official latest aliases");
   CoreFixture.Check(CoreReleaseProtocol.Verify(envelope[0],envelope[1],CoreFixture.Key,AppVersion.Current).version=="9.8.7","HTTP "+code+" fallback retains production signature verification");
   Service(envelope,"Available","HTTP "+code+" authenticated fallback");
  }
  // Changing an error branch to fallback must fail these tests before any alias is requested.
  static void ApiFailures() {
   foreach(int code in new[]{404,500}){
    var error=HttpError(code);int calls=0;
    Func<byte[][]> lookup=()=>CoreReleaseNetwork.Lookup(CancellationToken.None,(url,token)=>{calls++;if(url!=Api)throw new Exception("FAIL: unexpected API error fallback");throw error;});
    if(code==404)CoreFixture.Check(lookup()==null,"API 404 remains unavailable");
    else Throws<WebException>(()=>lookup(),e=>object.ReferenceEquals(e,error),"API 500 propagates original error");
    CoreFixture.Check(calls==1,"API "+code+" does not request aliases or retry");
   }
   var missing=new WebException("fixture DNS failure",WebExceptionStatus.NameResolutionFailure);int attempts=0;
   Throws<WebException>(()=>CoreReleaseNetwork.Lookup(CancellationToken.None,(url,token)=>{attempts++;throw missing;}),e=>object.ReferenceEquals(e,missing),"API failure without HTTP response propagates");
   CoreFixture.Check(attempts==1,"API failure without response is not retried");
   foreach(string json in new[]{"{","null","{}","{\"draft\":true,\"prerelease\":false,\"tag_name\":\"v9.8.7\"}","{\"draft\":false,\"prerelease\":true,\"tag_name\":\"v9.8.7\"}","{\"draft\":\"false\",\"prerelease\":false,\"tag_name\":\"v9.8.7\"}","{\"draft\":false,\"prerelease\":false,\"tag_name\":\"9.8.7\"}","{\"draft\":false,\"prerelease\":false,\"tag_name\":\"vbad\"}"}){
    int calls=0;
    CoreFixture.Reject(()=>CoreReleaseNetwork.Lookup(CancellationToken.None,(url,token)=>{calls++;if(url!=Api)throw new Exception("FAIL: invalid metadata selected download");return new MemoryStream(CoreFixture.Bytes(json));}),"malformed or nonstable API metadata fails closed");
    CoreFixture.Check(calls==1,"invalid API metadata does not select fallback");
   }
  }
  static void AliasFailures(CoreManifest target) {
   foreach(string asset in new[]{"update-manifest.json","update-manifest.sig"})foreach(int code in new[]{404,500,403,429}){
    var calls=new List<string>();var error=HttpError(code);var manifest=new MemoryStream(target.VerifiedEnvelope.Raw);
    Func<byte[][]> lookup=()=>CoreReleaseNetwork.Lookup(CancellationToken.None,(url,token)=>{
     calls.Add(url);if(url==Api)throw HttpError(403);if(url==Alias+asset)throw error;
     if(url==Alias+"update-manifest.json")return manifest;
     throw new Exception("FAIL: request followed failed alias "+url);
    });
    if(code==404)CoreFixture.Check(lookup()==null,"missing fallback "+asset+" is unavailable");
    else Throws<WebException>(()=>lookup(),e=>object.ReferenceEquals(e,error),"fallback "+asset+" HTTP "+code+" propagates without retry");
    CoreFixture.Check(calls.SequenceEqual(asset=="update-manifest.json"?new[]{Api,Alias+asset}:new[]{Api,Alias+"update-manifest.json",Alias+asset}),"failed fallback stops at "+asset+" HTTP "+code);
    if(asset=="update-manifest.sig")CoreFixture.Check(!manifest.CanRead,"signature failure disposes opened manifest");else manifest.Dispose();
   }
   foreach(bool manifest in new[]{true,false}){
    var calls=new List<string>();var bytes=new MemoryStream(new byte[manifest?CoreReleaseProtocol.MaxManifestBytes+1:1025]);
    Throws<InvalidDataException>(()=>CoreReleaseNetwork.Lookup(CancellationToken.None,(url,token)=>{
     calls.Add(url);if(url==Api)throw HttpError(429);
     if(url==Alias+"update-manifest.json")return manifest?bytes:new MemoryStream(target.VerifiedEnvelope.Raw);
     if(url==Alias+"update-manifest.sig")return manifest?new MemoryStream(target.VerifiedEnvelope.Signature):bytes;
     throw new Exception("FAIL: unexpected oversize route "+url);
    }),e=>true,"oversize fallback "+(manifest?"manifest":"signature")+" is rejected");
    CoreFixture.Check(!bytes.CanRead&&calls.Count==3,"oversize fallback closes streams and does not retry");
   }
  }
  static void Cancellation(CoreManifest target) {
   foreach(bool afterManifest in new[]{false,true})using(var cancel=new CancellationTokenSource()){
    var calls=new List<string>();var manifest=new MemoryStream(target.VerifiedEnvelope.Raw);
    Throws<OperationCanceledException>(()=>CoreReleaseNetwork.Lookup(cancel.Token,(url,token)=>{
     calls.Add(url);CoreFixture.Check(token==cancel.Token,"lookup forwards cancellation token");
     if(url==Api){var error=HttpError(403);if(!afterManifest)cancel.Cancel();throw error;}
     if(url==Alias+"update-manifest.json"){cancel.Cancel();return manifest;}
     throw new Exception("FAIL: canceled lookup opened signature");
    }),e=>e.CancellationToken==cancel.Token,"cancellation stops before next fallback request");
    CoreFixture.Check(calls.SequenceEqual(afterManifest?new[]{Api,Alias+"update-manifest.json"}:new[]{Api}),"canceled fallback performs no further transport calls");
    if(afterManifest)CoreFixture.Check(!manifest.CanRead,"canceled fallback disposes manifest");else manifest.Dispose();
   }
  }
  static void Unauthenticated(CoreManifest target) {
   var mixed=CoreFixture.Manifest("9.8.6",CoreFixture.B());
   foreach(byte[] signature in new[]{new byte[256],mixed.VerifiedEnvelope.Signature}){
    var envelope=CoreReleaseNetwork.Lookup(CancellationToken.None,(url,token)=>{
     if(url==Api)throw HttpError(403);
     if(url==Alias+"update-manifest.json")return new MemoryStream(target.VerifiedEnvelope.Raw);
     if(url==Alias+"update-manifest.sig")return new MemoryStream(signature);
     throw new Exception("FAIL: unexpected unauthenticated route "+url);
    });
    Throws<CryptographicException>(()=>CoreReleaseProtocol.Verify(envelope[0],envelope[1],CoreFixture.Key,AppVersion.Current),e=>true,"corrupt or mixed latest envelope fails production verification");
    Service(envelope,"Error","corrupt or mixed fallback");
   }
  }
  static void Service(byte[][] envelope,string expected,string name) {
   string root=CoreFixture.Root();CoreDeployment.Install(root,CoreFixture.Manifest(AppVersion.Current,CoreFixture.A),CoreFixture.A,CoreFixture.Key);
   string record=CoreFixture.FileAt(root,CoreDeployment.RecordName);byte[] before=File.ReadAllBytes(record);int downloads=0;
   using(var service=new CoreUpdateService(root,CoreFixture.Key,token=>envelope,(url,token)=>{downloads++;throw new Exception("FAIL: check fetched install file");})){
    service.Check();var end=DateTime.UtcNow.AddSeconds(5);CoreUpdateStatus status;
    do{status=service.Status();if(status.state!="Checking")break;Thread.Sleep(10);}while(DateTime.UtcNow<end);
    CoreFixture.Check(status.state==expected,name+" service reaches "+expected);
    if(expected=="Error"){
     CoreFixture.Check(status.latestVersion==""&&status.taskId=="","unauthenticated offer exposes no version or task");
     Throws<InvalidOperationException>(()=>service.Download(),e=>true,"unauthenticated fallback cannot start download");
    }else CoreFixture.Check(status.latestVersion=="9.8.7","authenticated fallback offers verified version");
    CoreFixture.Check(downloads==0&&File.ReadAllBytes(record).SequenceEqual(before),name+" preserves install record without downloads");
    CoreFixture.Same(root,CoreFixture.A,name+" preserves all baseline files");
   }
  }
  static void Throws<T>(Action action,Func<T,bool> expected,string name) where T:Exception {
   try{action();}catch(T error){CoreFixture.Check(expected(error),name);return;}
   throw new Exception("FAIL: expected "+typeof(T).Name+": "+name);
  }
  // A real response is needed: HttpWebResponse cannot be faked by constructing WebException.
  static WebException HttpError(int code) {
   var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
   var serve=Task.Run(()=>{using(var client=listener.AcceptTcpClient())using(var stream=client.GetStream()){
    client.ReceiveTimeout=5000;client.SendTimeout=5000;
    using(var reader=new StreamReader(stream,Encoding.ASCII,false,1024,true)){string line;while((line=reader.ReadLine())!=null&&line.Length!=0){}}
    byte[] response=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+" Fixture\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");stream.Write(response,0,response.Length);
   }});
   try {
    var request=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/");request.Proxy=null;request.Timeout=5000;
    try{using(var response=request.GetResponse())throw new Exception("FAIL: fixture HTTP status did not fail");}
    catch(WebException e){CoreFixture.Check(e.Response is HttpWebResponse&&(int)((HttpWebResponse)e.Response).StatusCode==code,"loopback produced real HTTP "+code+" WebException");return e;}
   }finally{listener.Stop();if(!serve.Wait(6000))throw new Exception("FAIL: loopback server did not finish");}
  }
 }
}
