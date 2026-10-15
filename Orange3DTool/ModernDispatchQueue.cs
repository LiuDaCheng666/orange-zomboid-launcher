using System;
using System.Threading;
using System.Threading.Tasks;
namespace PZ3D {
 sealed class ModernDispatchQueue {
  readonly SemaphoreSlim queue=new SemaphoreSlim(1,1);readonly object gate=new object();int generation,mutations,operations;bool restarting;
  public bool TryReserveRestart(){lock(gate){if(restarting||mutations!=0)return false;restarting=true;return true;}}
  public void ReleaseRestart(){lock(gate)restarting=false;}
  public bool RestartReserved{get{lock(gate)return restarting;}}
  public bool CanRestart{get{lock(gate)return !restarting&&mutations==0;}}
  public async Task Drain(){var end=DateTime.UtcNow.AddSeconds(12);while(true){lock(gate)if(operations==0)return;if(DateTime.UtcNow>end)throw new InvalidOperationException("本机读取尚未结束，请稍后重试。");await Task.Delay(25);}}
  public async Task<object> ExecuteSnapshot(Func<object> action){lock(gate)operations++;try{return await Task.Run(action);}finally{lock(gate)operations--;}}
  public async Task<object> Execute(Func<object> action,bool bypass=false){lock(gate)operations++;try{if(bypass){Interlocked.Increment(ref generation);return await Task.Run(action);}await queue.WaitAsync();try{return await Task.Run(action);}finally{queue.Release();}}finally{lock(gate)operations--;}}
  public async Task<object> ExecuteMutation(Func<object> action){lock(gate){if(restarting)throw new InvalidOperationException("正在准备启动器重启，暂停新的修改。");mutations++;}int requested=Volatile.Read(ref generation);try{return await Execute(()=>{if(requested!=Volatile.Read(ref generation))throw new OperationCanceledException("已取消尚未开始的任务。");return action();});}finally{lock(gate)mutations--;}}
 }
}
