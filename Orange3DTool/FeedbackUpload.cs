using System;using System.Collections.Generic;using System.IO;using System.Net;using System.Text;using System.Threading;using System.Web.Script.Serialization;
namespace PZ3D {static class FeedbackUpload {
 // A single deadline covers connect, request bytes, response headers and body.
 internal static Dictionary<string,object> Send(HttpWebRequest request,byte[] body,CancellationToken token,int milliseconds){
  if(milliseconds<1||milliseconds>15000||body==null||body.Length>262144)throw new ArgumentException("反馈发送参数无效。");request.AllowAutoRedirect=false;request.Timeout=milliseconds;request.ReadWriteTimeout=milliseconds;
  using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){deadline.CancelAfter(milliseconds);using(deadline.Token.Register(request.Abort)){
   try{using(var output=request.GetRequestStream()){deadline.Token.ThrowIfCancellationRequested();output.Write(body,0,body.Length);}using(var response=(HttpWebResponse)request.GetResponse()){if(response.StatusCode!=HttpStatusCode.Created&&response.StatusCode!=HttpStatusCode.OK)throw new IOException("反馈发送未完成，请重试。");using(var stream=response.GetResponseStream())using(var memory=new MemoryStream()){byte[] buffer=new byte[4096];int n;while((n=stream.Read(buffer,0,buffer.Length))>0){deadline.Token.ThrowIfCancellationRequested();if(memory.Length+n>16384)throw new IOException("反馈回执过大。");memory.Write(buffer,0,n);}deadline.Token.ThrowIfCancellationRequested();return new JavaScriptSerializer{MaxJsonLength=16384,RecursionLimit=8}.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(memory.ToArray()));}}}
   catch(WebException ex){var response=ex.Response as HttpWebResponse;int status=response==null?0:(int)response.StatusCode;if(response!=null)response.Dispose();throw new IOException(status==429?"反馈发送频繁，请稍后重试。":status==413?"反馈资料过大，请减少勾选后重试。":"反馈发送未完成，请检查网络后重试。");}
  }}
 }
}}
