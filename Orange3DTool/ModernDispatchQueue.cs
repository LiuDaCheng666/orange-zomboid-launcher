using System;
using System.Threading;
using System.Threading.Tasks;
namespace PZ3D {
 sealed class ModernDispatchQueue {
  readonly SemaphoreSlim queue=new SemaphoreSlim(1,1);int generation;
  public Task<object> ExecuteSnapshot(Func<object> action){return Task.Run(action);}
  public async Task<object> Execute(Func<object> action,bool bypass=false){if(bypass){Interlocked.Increment(ref generation);return await Task.Run(action);}await queue.WaitAsync();try{return await Task.Run(action);}finally{queue.Release();}}
  public Task<object> ExecuteMutation(Func<object> action){int requested=Volatile.Read(ref generation);return Execute(()=>{if(requested!=Volatile.Read(ref generation))throw new OperationCanceledException("已取消尚未开始的任务。");return action();});}
 }
}
