using System;
using System.Collections.Generic;
using System.Threading;
namespace PZ3D {
 sealed partial class ModernController {
  sealed class EnvironmentPreviewJob {
   internal string Id,SaveId;internal Engine Engine;internal long Generation;
   internal readonly CancellationTokenSource Cancel=new CancellationTokenSource();
  }
  readonly object environmentPreviewGate=new object();
  readonly HashSet<string> environmentPreviewCancelled=new HashSet<string>(StringComparer.Ordinal);
  long environmentPreviewGeneration;
  EnvironmentPreviewJob environmentPreviewJob;
  internal static bool EnvironmentPreviewAction(string action){return action=="environmentPreview"||action=="environmentPreviewCancel";}
  void CancelEnvironmentPreviews(){lock(environmentPreviewGate){environmentPreviewGeneration++;environmentPreviewCancelled.Clear();if(environmentPreviewJob!=null)environmentPreviewJob.Cancel.Cancel();}}
  internal void EnvironmentReport(string saveId,string jobId,SaveEnvironmentProgress progress){
   if(jobId==null)Report(progress.value==100&&progress.stage!="complete"?99:progress.value,progress.text);
   var callback=Event;if(callback!=null)callback("environmentProgress",new{saveId=saveId,jobId=jobId,stage=progress.stage,text=progress.text,value=progress.value,completedFiles=progress.completedFiles,totalFiles=progress.totalFiles,completedBytes=progress.completedBytes,totalBytes=progress.totalBytes});
  }
  object ExecuteEnvironmentPreview(string action,Dictionary<string,object> payload){
   ValidateEnvironmentPayload(action,payload);
   if(action=="environmentPreviewCancel"){
    string requested=Text(payload,"jobId");
    lock(environmentPreviewGate){if(Volatile.Read(ref disposed)){environmentPreviewCancelled.Clear();throw new ObjectDisposedException("ModernController");}if(environmentPreviewJob!=null&&environmentPreviewJob.Id==requested)environmentPreviewJob.Cancel.Cancel();else environmentPreviewCancelled.Add(requested);}
    return new{jobId=requested,cancelled=true};
   }
   var job=new EnvironmentPreviewJob{Id=payload.ContainsKey("jobId")?Text(payload,"jobId"):Guid.NewGuid().ToString("N"),SaveId=Text(payload,"saveId")};
   // Register before waiting for the controller gate. The parallel dispatcher
   // may deliver cancellation before this invocation is scheduled at all.
   lock(environmentPreviewGate){if(Volatile.Read(ref disposed)){job.Cancel.Dispose();throw new ObjectDisposedException("ModernController");}job.Generation=environmentPreviewGeneration;if(environmentPreviewCancelled.Remove(job.Id))job.Cancel.Cancel();else{if(environmentPreviewJob!=null)environmentPreviewJob.Cancel.Cancel();environmentPreviewJob=job;}}
   try {
    job.Cancel.Token.ThrowIfCancellationRequested();lock(gate){if(disposed)throw new ObjectDisposedException("ModernController");job.Engine=engine;}job.Cancel.Token.ThrowIfCancellationRequested();
    return new SaveEnvironments(job.Engine).Preview(job.SaveId,job.Cancel.Token,progress=>{
     job.Cancel.Token.ThrowIfCancellationRequested();lock(environmentPreviewGate){if(environmentPreviewJob!=job||job.Generation!=environmentPreviewGeneration)throw new OperationCanceledException();}
     EnvironmentReport(job.SaveId,job.Id,progress);
    });
   }finally{lock(environmentPreviewGate){if(environmentPreviewJob==job)environmentPreviewJob=null;}job.Cancel.Dispose();}
  }
 }
}
