using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace PZ3D {
 public sealed class CoreUpdateStatus {
  public int protocol=1;
  public long revision,receivedBytes,totalBytes,speedBytes;
  public List<CoreUpdateResult> history=new List<CoreUpdateResult>();
  public string state="Idle",currentVersion=AppVersion.Current,latestVersion="",checkedUtc="",notes="",currentFile="",message="手动检查启动器更新",taskId="";
 }
 // Transport is an in-process dependency. Neither bridge payloads nor disk files
 // can supply a feed URL or a trust key.
 public sealed class CoreUpdateService:IDisposable {
  readonly string root,key;readonly Func<CancellationToken,byte[][]> lookup;readonly Func<string,CancellationToken,Stream> open;
  readonly object gate=new object();CoreUpdateStatus status=new CoreUpdateStatus();CoreManifest candidate;Task worker;CancellationTokenSource cancellation;Timer healthPoll;bool disposed;
  public Action<CoreUpdateStatus> Progress;
  public CoreUpdateService(string root):this(root,EmbeddedKey(),CoreReleaseNetwork.Lookup,CoreReleaseNetwork.Open){}
  static string EmbeddedKey(){try{return CoreUpdateTrust.PublicKeyXml;}catch{return null;}}
  internal CoreUpdateService(string root,string key,Func<CancellationToken,byte[][]> lookup,Func<string,CancellationToken,Stream> open){this.root=CoreDeployment.Root(root);this.key=key;this.lookup=lookup;this.open=open;if(key==null){status.state="Unavailable";status.message="此旧版构建没有内置更新信任，请安装完整签名版本。";}else{RefreshHistory();if(status.state=="PendingHealth")healthPoll=new Timer(ObserveHealth,null,250,250);}}
  void ObserveHealth(object unused){CoreUpdateStatus snapshot=null;lock(gate){if(disposed)return;long revision=status.revision;try{RefreshHistory();}catch(Exception e){status.state="Conflicted";status.message=e.Message;status.revision++;}if(status.revision!=revision)snapshot=Clone();if(status.state!="PendingHealth"&&healthPoll!=null){healthPoll.Dispose();healthPoll=null;}}var callback=Progress;if(snapshot!=null&&callback!=null)try{callback(snapshot);}catch{}}
  void RefreshHistory(){if(key==null||!File.Exists(CoreDeployment.Safe(root,CoreDeployment.RecordName)))return;var history=CoreUpdateTransaction.History(root,key);bool changed=CoreDeployment.Json.Serialize(history)!=CoreDeployment.Json.Serialize(status.history);status.history=history;if(history.Count>0&&new[]{"Idle","PendingHealth","Applying","RollingBack","Succeeded","RolledBack","Conflicted"}.Contains(status.state)){var last=history[0];status.taskId=last.TaskId;status.state=last.State;status.latestVersion=last.Version;status.message=last.State=="Succeeded"?"启动器更新成功，健康验证已完成。":last.State=="RolledBack"?"更新已回退，上一版本已恢复。 "+last.Message:last.State=="Conflicted"?"更新恢复受阻，已保留恢复文件。 "+last.Message:"更新正在完成健康验证或恢复。 "+last.Message;}if(changed)status.revision++;}
  public CoreUpdateStatus Status(){lock(gate){RefreshHistory();return Clone();}}
  CoreUpdateStatus Clone(){return new JavaScriptSerializer().Deserialize<CoreUpdateStatus>(new JavaScriptSerializer().Serialize(status));}
  void Publish(Action<CoreUpdateStatus> change){CoreUpdateStatus copy;lock(gate){change(status);status.revision++;copy=Clone();}var callback=Progress;if(callback!=null)try{callback(copy);}catch{}}
  void Log(string phase,string message){CoreUpdateDiagnostics.Write(root,status.taskId,phase,message);}
  public CoreUpdateStatus Check(){return Start("Checking",token=>{
   Publish(s=>{s.latestVersion="";s.notes="";s.taskId="";s.message="正在检查已签名的正式发布";});candidate=null;Log("check","Started");
   var publication=lookup(token);token.ThrowIfCancellationRequested();
   if(publication==null){Publish(s=>{s.state="Unavailable";s.checkedUtc=DateTime.UtcNow.ToString("o");s.message="官方更新渠道暂未发布可用版本。";});Log("check","Unavailable");return;}
   if(publication.Length!=2)throw new InvalidDataException("Release envelope is incomplete.");
   var verified=CoreReleaseProtocol.Verify(publication[0],publication[1],key,AppVersion.Current);token.ThrowIfCancellationRequested();candidate=verified;
   Publish(s=>{s.latestVersion=verified.version;s.notes=verified.notes;s.checkedUtc=DateTime.UtcNow.ToString("o");s.state=CoreReleaseProtocol.IsNewer(verified.version,AppVersion.Current)?"Available":"Latest";s.message=s.state=="Available"?"可更新至 "+verified.version:"已验证当前正式版本";});Log("check","Verified "+verified.version);
  });}
  public CoreUpdateStatus Download(){lock(gate){if(status.state=="Ready")return Clone();if(candidate==null)throw new InvalidOperationException("请先检查并验证正式发布。");}return Start("Downloading",token=>{
   // A Windows mutex is thread-affine: this entire synchronous delegate,
   // including nested Plan/Stage and disposal, stays on one worker thread.
   using(CoreDeployment.Acquire(root)) {
    var plan=CoreUpdateTransaction.Plan(root,candidate,key);var bytes=new Dictionary<string,byte[]>();long completed=0;var watch=Stopwatch.StartNew();
    Publish(s=>{s.totalBytes=plan.Bytes;s.receivedBytes=0;s.speedBytes=0;s.message="正在下载并核验启动器文件";});Log("download","Started "+plan.Bytes);
    foreach(var file in plan.Changed) {
     token.ThrowIfCancellationRequested();Publish(s=>s.currentFile=file.path);
     string cache=CoreDeployment.Runtime+"/CoreDownloads/"+file.sha256.ToLowerInvariant();string path=CoreDeployment.Safe(root,cache);byte[] data;
     if(CoreDeployment.Matches(path,file)){data=CoreDeployment.ReadBytes(path,(int)CoreReleaseProtocol.MaxFileBytes);Log("reuse",file.path);}
     else {
      using(var input=open(file.url,token))using(var output=new MemoryStream())using(var sha=SHA256.Create()) {
       var buffer=new byte[65536];long count=0;int read;
       while((read=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();count+=read;if(count>file.length)throw new InvalidDataException("文件超出签名长度："+file.path);sha.TransformBlock(buffer,0,read,null,0);output.Write(buffer,0,read);long received=completed+count;Publish(s=>{s.receivedBytes=received;s.speedBytes=(long)(received/Math.Max(.1,watch.Elapsed.TotalSeconds));});}
       sha.TransformFinalBlock(new byte[0],0,0);string hash=BitConverter.ToString(sha.Hash).Replace("-","");
       if(count!=file.length||!string.Equals(hash,file.sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("文件长度或哈希不符："+file.path);
       data=output.ToArray();CoreDeployment.Atomic(root,cache,data);
      }
     }
     token.ThrowIfCancellationRequested();bytes.Add(file.path,data);completed+=file.length;Publish(s=>s.receivedBytes=completed);Log("verify",file.path);
    }
    token.ThrowIfCancellationRequested();string task=CoreUpdateTransaction.Stage(root,candidate,bytes,key);Publish(s=>{s.taskId=task;s.state="Ready";s.message="下载已验证，等待确认重启";s.currentFile="";});Log("staged",task);
   }
  });}
  CoreUpdateStatus Start(string phase,Action<CancellationToken> action){lock(gate){if(disposed)throw new ObjectDisposedException("CoreUpdateService");if(worker!=null&&!worker.IsCompleted)return Clone();if(status.state=="Ready"||status.state=="Restarting")return Clone();if(key==null)return Clone();if(cancellation!=null)cancellation.Dispose();cancellation=new CancellationTokenSource();var token=cancellation.Token;status.state=phase;status.revision++;worker=Task.Run(()=>{try{action(token);}catch(OperationCanceledException){Log("cancel","Cancelled");Publish(s=>{s.state="Cancelled";s.message="已取消，当前启动器文件未改变。";});}catch(Exception e){if(token.IsCancellationRequested){Log("cancel","Cancelled");Publish(s=>{s.state="Cancelled";s.message="已取消，当前启动器文件未改变。";});}else{Log("error",e.Message);Publish(s=>{s.state="Error";s.message=e.Message;});}}});return Clone();}}
  public CoreUpdateStatus Cancel(){lock(gate){if(cancellation!=null)cancellation.Cancel();return Clone();}}
  internal string PreparedTask(){lock(gate){if(status.state!="Ready"||worker!=null&&!worker.IsCompleted)throw new InvalidOperationException("更新尚未准备完成。");return status.taskId;}}
  internal void HandoffStarted(){Publish(s=>{s.state="Restarting";s.message="正在安全重启启动器";});Log("apply","Handoff");}
  public void Dispose(){lock(gate){disposed=true;if(cancellation!=null)cancellation.Cancel();if(healthPoll!=null){healthPoll.Dispose();healthPoll=null;}}}
 }
 internal static class CoreUpdateDiagnostics {
  static readonly object gate=new object();
  internal static void Write(string root,string task,string phase,string message){try{CoreDeployment.ReadOwner(root);string path=CoreDeployment.Safe(root,CoreDeployment.Runtime+"/CoreDiagnostics/history.jsonl");Directory.CreateDirectory(Path.GetDirectoryName(path));lock(gate){using(var stream=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.Read))using(var writer=new StreamWriter(stream,CoreDeployment.Utf8)){writer.WriteLine(new JavaScriptSerializer().Serialize(new{utc=DateTime.UtcNow.ToString("o"),taskId=task,phase=phase,message=message}));writer.Flush();stream.Flush(true);}}}catch{}}
 }
 internal static class CoreReleaseNetwork {
  const string Latest="https://api.github.com/repos/LiuDaCheng666/orange-zomboid-launcher/releases/latest";
  internal static bool Allowed(Uri uri){return uri.Scheme=="https"&&uri.IsDefaultPort&&uri.UserInfo==""&&uri.Fragment==""&&(uri.Host=="api.github.com"||uri.Host=="github.com"||uri.Host=="release-assets.githubusercontent.com"||uri.Host=="objects.githubusercontent.com");}
  public static byte[][] Lookup(CancellationToken token){return Lookup(token,Open);}
  internal static byte[][] Lookup(CancellationToken token,Func<string,CancellationToken,Stream> open){byte[] raw;try{using(var stream=open(Latest,token))raw=ReadBounded(stream,CoreReleaseProtocol.MaxManifestBytes,token);}catch(WebException e){var response=e.Response as HttpWebResponse;if(response==null)throw;int code=(int)response.StatusCode;response.Dispose();if(code==404)return null;if(code!=403&&code!=429)throw;return LatestEnvelope(token,open);}
   var map=new JavaScriptSerializer{MaxJsonLength=CoreReleaseProtocol.MaxManifestBytes}.Deserialize<Dictionary<string,object>>(CoreDeployment.Utf8.GetString(raw));object draft,pre,tag;
   if(map==null||!map.TryGetValue("draft",out draft)||!(draft is bool)||(bool)draft||!map.TryGetValue("prerelease",out pre)||!(pre is bool)||(bool)pre||!map.TryGetValue("tag_name",out tag)||!(tag is string)||!((string)tag).StartsWith("v",StringComparison.Ordinal))throw new InvalidDataException("正式发布元数据无效。");
   string version=((string)tag).Substring(1);CoreReleaseProtocol.ParseVersion(version);string prefix=CoreReleaseProtocol.ReleaseBase+"v"+version+"/";
   using(var manifest=open(prefix+"update-manifest.json",token))using(var signature=open(prefix+"update-manifest.sig",token))return new[]{ReadBounded(manifest,CoreReleaseProtocol.MaxManifestBytes,token),ReadBounded(signature,1024,token)};
  }
  static byte[][] LatestEnvelope(CancellationToken token,Func<string,CancellationToken,Stream> open){
   const string prefix="https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/latest/download/";
   try{token.ThrowIfCancellationRequested();using(var manifest=open(prefix+"update-manifest.json",token)){token.ThrowIfCancellationRequested();using(var signature=open(prefix+"update-manifest.sig",token))return new[]{ReadBounded(manifest,CoreReleaseProtocol.MaxManifestBytes,token),ReadBounded(signature,1024,token)};}}
   catch(WebException e){var response=e.Response as HttpWebResponse;if(response==null)throw;int code=(int)response.StatusCode;response.Dispose();if(code==404)return null;throw;}
  }
  static byte[] ReadBounded(Stream stream,int max,CancellationToken token){using(var bytes=new MemoryStream()){var buffer=new byte[16384];int n;while((n=stream.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();if(bytes.Length+n>max)throw new InvalidDataException("发布响应过大。");bytes.Write(buffer,0,n);}return bytes.ToArray();}}
  public static Stream Open(string url,CancellationToken token){Uri uri=new Uri(url);for(int redirect=0;redirect<=3;redirect++){
    token.ThrowIfCancellationRequested();if(!Allowed(uri))throw new InvalidDataException("拒绝非可信 HTTPS 下载地址。");
    var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;request.Timeout=20000;request.ReadWriteTimeout=20000;request.UserAgent="OrangeLauncher/"+AppVersion.Current;request.Accept=uri.Host=="api.github.com"?"application/vnd.github+json":"application/octet-stream";if(uri.Host=="api.github.com")request.Headers["X-GitHub-Api-Version"]="2026-03-10";
    var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(60000);var abort=deadline.Token.Register(request.Abort);HttpWebResponse response=null;
    try {response=(HttpWebResponse)request.GetResponse();int code=(int)response.StatusCode;if(code>=300&&code<=399){string location=response.Headers["Location"];response.Dispose();abort.Dispose();deadline.Dispose();if(string.IsNullOrEmpty(location)||redirect==3)throw new InvalidDataException("下载重定向无效或次数过多。");uri=new Uri(uri,location);continue;}if(code!=200)throw new InvalidDataException("下载响应无效。");return new ResponseStream(response,abort,deadline);}
    catch{if(response!=null)response.Dispose();abort.Dispose();deadline.Dispose();throw;}
   }throw new InvalidDataException("下载重定向过多。");}
  sealed class ResponseStream:Stream {
   readonly HttpWebResponse response;readonly Stream inner;readonly CancellationTokenSource deadline;CancellationTokenRegistration abort;
   internal ResponseStream(HttpWebResponse response,CancellationTokenRegistration abort,CancellationTokenSource deadline){this.response=response;this.abort=abort;this.deadline=deadline;inner=response.GetResponseStream();}
   public override int Read(byte[] b,int o,int n){return inner.Read(b,o,n);}public override bool CanRead{get{return true;}}public override bool CanSeek{get{return false;}}public override bool CanWrite{get{return false;}}public override long Length{get{throw new NotSupportedException();}}public override long Position{get{throw new NotSupportedException();}set{throw new NotSupportedException();}}public override void Flush(){}public override long Seek(long o,SeekOrigin so){throw new NotSupportedException();}public override void SetLength(long n){throw new NotSupportedException();}public override void Write(byte[] b,int o,int n){throw new NotSupportedException();}protected override void Dispose(bool disposing){if(disposing){inner.Dispose();response.Dispose();abort.Dispose();deadline.Dispose();}base.Dispose(disposing);}
  }
 }
}
