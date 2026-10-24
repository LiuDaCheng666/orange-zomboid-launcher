using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;
namespace PZ3D {
 // No URLs are accepted from the wire. The signed identity maps to two fixed origins.
 internal sealed class VelopackFeed {
  internal const string AppId="OrangeZomboidLauncher";
  internal string Version,FileName,Hash,Notes; internal long Length;
  internal static void Authenticate(byte[] raw,byte[] signature,string key){
   if(raw==null||raw.Length==0||raw.Length>1048576||signature==null||signature.Length==0||signature.Length>1024)throw new InvalidDataException("Invalid signed envelope size.");
   using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(key);if(rsa.KeySize<2048||!rsa.VerifyData(raw,CryptoConfig.MapNameToOID("SHA256"),signature))throw new CryptographicException("发布签名验证失败。");}
  }
  internal static string RoutingVersion(byte[] raw){var m=CoreReleaseProtocol.ReadStoredObject(raw,"schema","appId","channel","version","fileName","length","sha256","notes");string v=m["version"] as string;CoreReleaseProtocol.ParseVersion(v);return v;}
  internal static VelopackFeed Verify(byte[] raw,byte[] signature,string key,string current){
   raw=(byte[])raw.Clone();signature=(byte[])signature.Clone();Authenticate(raw,signature,key);
   var m=CoreReleaseProtocol.ReadStoredObject(raw,"schema","appId","channel","version","fileName","length","sha256","notes");
   var f=new VelopackFeed{Version=m["version"] as string,FileName=m["fileName"] as string,Hash=m["sha256"] as string,Notes=m["notes"] as string};
   if(!(m["schema"] is long)||(long)m["schema"]!=1||!Equals(m["appId"],AppId)||!Equals(m["channel"],"win")||!(m["length"] is long))throw new InvalidDataException("Unsupported update lane.");
   f.Length=(long)m["length"];
   if(CoreReleaseProtocol.ParseVersion(f.Version)<CoreReleaseProtocol.ParseVersion(current))throw new InvalidDataException("Older release replay rejected.");
   if(f.FileName!=AppId+"-"+f.Version+"-full.nupkg"||f.Length<=0||f.Length>1073741824||f.Hash==null||!Regex.IsMatch(f.Hash,@"\A[0-9a-fA-F]{64}\z")||f.Notes==null)throw new InvalidDataException("Invalid full package metadata.");
   return f;
  }
 }
 internal sealed class AuthenticatedVelopackSource:IUpdateSource {
  internal const string Primary="https://orangezl.com/updates/velopack/latest/velopack-manifest.json";
  const string GithubLatest="https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/latest/download/velopack-manifest.json";
  readonly string key,current,checkpoint;readonly Func<string,CancellationToken,Stream> open;
  VelopackFeed accepted;byte[] acceptedRaw,acceptedSignature; internal CancellationToken CheckToken;
  internal AuthenticatedVelopackSource(string key,string current,Func<string,CancellationToken,Stream> open,string checkpoint=null){this.key=key;this.current=current;this.open=open;this.checkpoint=checkpoint;
   using(ReleaseCheckpoint.Acquire(checkpoint))ReleaseCheckpoint.Floor(checkpoint,key,current);
  }
  byte[] Read(string url,int max,CancellationToken token){using(var stream=open(url,token))return CoreReleaseNetwork.ReadBounded(stream,max,token);}
  public Task<VelopackAssetFeed> GetReleaseFeed(IVelopackLogger logger,string appId,string channel,Guid? stagingId=null,VelopackAsset latestLocalRelease=null){
   var token=CheckToken;token.ThrowIfCancellationRequested();if(appId!=VelopackFeed.AppId||channel!="win")throw new InvalidDataException("Unexpected framework package identity.");
   byte[] raw,signature;
   try{raw=Read(Primary,1048576,token);string version=VelopackFeed.RoutingVersion(raw);signature=Read("https://orangezl.com/releases/v"+version+"/velopack-manifest.sig",1024,token);}
   catch(Exception e){token.ThrowIfCancellationRequested();if(!CoreReleaseSources.TransportFailure(e))throw;CoreReleaseSources.DisposeResponse(e);raw=Read(GithubLatest,1048576,token);string version=VelopackFeed.RoutingVersion(raw);signature=Read(CoreReleaseProtocol.ReleaseBase+"v"+version+"/velopack-manifest.sig",1024,token);}
   using(ReleaseCheckpoint.Acquire(checkpoint)){token.ThrowIfCancellationRequested();var verified=VelopackFeed.Verify(raw,signature,key,ReleaseCheckpoint.Floor(checkpoint,key,accepted==null?current:accepted.Version));token.ThrowIfCancellationRequested();ReleaseCheckpoint.Write(checkpoint,raw,signature);accepted=verified;acceptedRaw=(byte[])raw.Clone();acceptedSignature=(byte[])signature.Clone();
    return Task.FromResult(new VelopackAssetFeed{Assets=new[]{new VelopackAsset{PackageId=VelopackFeed.AppId,Version=SemanticVersion.Parse(verified.Version),Type=VelopackAssetType.Full,FileName=verified.FileName,Size=verified.Length,SHA256=verified.Hash.ToUpperInvariant(),NotesMarkdown=verified.Notes}}});
   }
  }
  void Require(VelopackAsset a){if(accepted==null||a==null||a.PackageId!=VelopackFeed.AppId||a.Type!=VelopackAssetType.Full||a.FileName!=accepted.FileName||a.Version.ToString()!=accepted.Version||a.Size!=accepted.Length||!string.Equals(a.SHA256,accepted.Hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Package differs from authenticated feed.");using(ReleaseCheckpoint.Acquire(checkpoint))VelopackFeed.Verify(acceptedRaw,acceptedSignature,key,ReleaseCheckpoint.Floor(checkpoint,key,current));}
  internal void VerifyPackage(string path,VelopackAsset a){Require(a);LauncherMaintenance.NoReparse(path);if(!File.Exists(path)||new FileInfo(path).Length!=accepted.Length||!string.Equals(CoreReleaseProtocol.HashFile(path),accepted.Hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新包长度或 SHA-256 校验失败。");}
  internal ApplyLeaseRequest LeaseRequest(string root,string content,string packages,string updater,bool portable){if(accepted==null||checkpoint==null)throw new InvalidOperationException("Missing authenticated release checkpoint.");return new ApplyLeaseRequest{Root=root,Current=content,Package=Path.Combine(packages,accepted.FileName),Updater=updater,Checkpoint=checkpoint,Portable=portable,Raw=Convert.ToBase64String(acceptedRaw),Signature=Convert.ToBase64String(acceptedSignature)};}
  public Task DownloadReleaseEntry(IVelopackLogger logger,VelopackAsset releaseEntry,string localFile,Action<int> progress,CancellationToken cancelToken){
   Require(releaseEntry);string tail="v"+accepted.Version+"/"+accepted.FileName;string[] urls={"https://orangezl.com/releases/"+tail,CoreReleaseProtocol.ReleaseBase+tail};
   LauncherMaintenance.NoReparse(localFile);
   for(int i=0;i<urls.Length;i++){
    cancelToken.ThrowIfCancellationRequested();try{
     using(var input=open(urls[i],cancelToken))using(var output=new FileStream(localFile,FileMode.Create,FileAccess.Write,FileShare.None)){byte[] buffer=new byte[65536];long count=0;int n;while((n=input.Read(buffer,0,buffer.Length))>0){cancelToken.ThrowIfCancellationRequested();count+=n;if(count>accepted.Length)throw new InvalidDataException("Package exceeds signed length.");output.Write(buffer,0,n);if(progress!=null)progress((int)(count*100/accepted.Length));}output.Flush(true);}
     cancelToken.ThrowIfCancellationRequested();VerifyPackage(localFile,releaseEntry);return Task.FromResult(0);
    }catch(Exception e){if(File.Exists(localFile))File.Delete(localFile);cancelToken.ThrowIfCancellationRequested();if(i==1||!CoreReleaseSources.TransportFailure(e))throw;CoreReleaseSources.DisposeResponse(e);}
   }throw new IOException("No package source available.");
  }
 }
 internal static class VelopackNetwork {
  internal static bool InitialAllowed(Uri uri){if(uri.AbsoluteUri==AuthenticatedVelopackSource.Primary)return true;if(uri.AbsoluteUri=="https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/latest/download/velopack-manifest.json")return true;try{string s=uri.AbsoluteUri;if(s.StartsWith("https://orangezl.com/releases/",StringComparison.Ordinal))s=CoreReleaseProtocol.ReleaseBase+s.Substring("https://orangezl.com/releases/".Length);if(!s.StartsWith(CoreReleaseProtocol.ReleaseBase,StringComparison.Ordinal))return false;string tail=s.Substring(CoreReleaseProtocol.ReleaseBase.Length);int slash=tail.IndexOf('/');if(slash<2||tail[0]!='v')return false;CoreReleaseProtocol.ValidateUrl(s,tail.Substring(1,slash-1));return true;}catch{return false;}}
  internal static Stream Open(string url,CancellationToken token){
   Uri uri=new Uri(url);if(!InitialAllowed(uri))throw new InvalidDataException("拒绝非官方更新地址。");bool github=uri.Host=="github.com";
   for(int redirects=0;redirects<4;redirects++){
    token.ThrowIfCancellationRequested();var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;request.Timeout=15000;request.ReadWriteTimeout=30000;request.UserAgent="OrangeLauncher/"+AppVersion.Current;
    var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromMinutes(10));var abort=deadline.Token.Register(request.Abort);HttpWebResponse response=null;
    try{response=(HttpWebResponse)request.GetResponse();if((int)response.StatusCode>=300&&(int)response.StatusCode<400){string location=response.Headers["Location"];response.Dispose();abort.Dispose();deadline.Dispose();Uri next;if(!github||!Uri.TryCreate(uri,location,out next)||next.Scheme!="https"||!next.IsDefaultPort||next.UserInfo!=""||next.Fragment!=""||!(InitialAllowed(next)||next.Host=="release-assets.githubusercontent.com"||next.Host=="objects.githubusercontent.com"))throw new InvalidDataException("Untrusted update redirect.");uri=next;continue;}if(response.StatusCode!=HttpStatusCode.OK)throw new InvalidDataException("Unexpected response status.");return new OwnedResponse(response,abort,deadline);}
    catch{if(response!=null)response.Dispose();abort.Dispose();deadline.Dispose();throw;}
   }throw new InvalidDataException("Too many update redirects.");
  }
  sealed class OwnedResponse:Stream {
   readonly HttpWebResponse response;readonly Stream stream;readonly CancellationTokenSource deadline;CancellationTokenRegistration abort;
   internal OwnedResponse(HttpWebResponse r,CancellationTokenRegistration a,CancellationTokenSource d){response=r;stream=r.GetResponseStream();abort=a;deadline=d;}
   public override int Read(byte[] b,int o,int n){return stream.Read(b,o,n);}public override bool CanRead{get{return true;}}public override bool CanSeek{get{return false;}}public override bool CanWrite{get{return false;}}public override long Length{get{throw new NotSupportedException();}}public override long Position{get{throw new NotSupportedException();}set{throw new NotSupportedException();}}public override void Flush(){}public override long Seek(long o,SeekOrigin s){throw new NotSupportedException();}public override void SetLength(long n){throw new NotSupportedException();}public override void Write(byte[] b,int o,int n){throw new NotSupportedException();}protected override void Dispose(bool d){if(d){stream.Dispose();response.Dispose();abort.Dispose();deadline.Dispose();}base.Dispose(d);}
  }
 }
}
