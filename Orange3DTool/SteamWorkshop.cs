using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
namespace PZ3D {
 static class SteamWorkshop {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibraryEx(string name,IntPtr file,uint flags);
  [DllImport("kernel32.dll",CharSet=CharSet.Ansi)]static extern IntPtr GetProcAddress(IntPtr module,string name);
  [DllImport("kernel32.dll")]static extern bool FreeLibrary(IntPtr module);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool Init();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void VoidCall();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr GetInterface();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate uint ItemState(IntPtr self,ulong id);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate ulong Subscribe(IntPtr self,ulong id);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool Completed(IntPtr self,ulong call,[MarshalAs(UnmanagedType.I1)]out bool failed);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool Result(IntPtr self,ulong call,IntPtr buffer,int size,int callback,[MarshalAs(UnmanagedType.I1)]out bool failed);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int GetPipe();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void PipeCall(int pipe);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool Next(int pipe,out Callback cb);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool Download(IntPtr self,ulong id,[MarshalAs(UnmanagedType.I1)]bool priority);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool DownloadInfo(IntPtr self,ulong id,out ulong done,out ulong total);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool InstallInfo(IntPtr self,ulong id,out ulong size,StringBuilder folder,uint capacity,out uint time);
  [StructLayout(LayoutKind.Sequential,Pack=8)]struct Callback {public int User,Id;public IntPtr Data;public int Size;}
  static T Load<T>(IntPtr dll,string name)where T:class{IntPtr p=GetProcAddress(dll,name);if(p==IntPtr.Zero)throw new Exception("Steam API 不提供 "+name+"，请更新游戏后重试。");return (T)(object)Marshal.GetDelegateForFunctionPointer(p,typeof(T));}
  public static bool SuccessResult(uint app,ulong id,uint result,ulong requested){return app==108600&&id==requested&&result==1;}
  public static string ComponentName(ulong id){return id==3619862853?"ZombieBuddy 加载器":id==3809306528?"Viewpoint 3D 模组":id==3810302175?"额外 3D 物件包":id.ToString();}
  public static string FormatDownloadProgress(ulong id,ulong done,ulong total){return total==0?"等待 Steam 返回下载量 · "+ComponentName(id):"Steam 下载 · "+ComponentName(id)+" · "+(done/1048576.0).ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+" / "+(total/1048576.0).ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+" MB";}
  public static void EnsureSubscribed(ulong id,Func<ulong,uint> state,Action<ulong> subscribe){if((state(id)&1)!=0)return;subscribe(id);if((state(id)&1)==0)throw new Exception("Steam 尚未确认订阅："+id+"。已下载不等于已订阅，不能标记为可加载。");}
  public static bool CanReuse(uint state){return (state&5)==5&&(state&(8|16|32))==0;}
  public static Dictionary<ulong,string> Fetch(string game,ulong[] ids,Action<int,string> progress,CancellationToken cancel,int seconds=420,bool subscriptionsOnly=false,bool force=false){
   if(progress!=null)progress(-1,"正在连接 Steam · 初始化工坊接口");
   if(Environment.Is64BitProcess==false)throw new Exception("工坊下载需要 64 位工具。");string library=Path.Combine(game,"steam_api64.dll");if(!File.Exists(library))throw new Exception("找不到游戏的 steam_api64.dll。");
   IntPtr dll=LoadLibraryEx(Path.GetFullPath(library),IntPtr.Zero,0x1100);if(dll==IntPtr.Zero)throw new Exception("无法载入游戏自带 Steam API，错误 "+Marshal.GetLastWin32Error());bool initialized=false;VoidCall shutdown=null;string prior=Environment.GetEnvironmentVariable("SteamAppId");string priorGame=Environment.GetEnvironmentVariable("SteamGameId");
   try{Environment.SetEnvironmentVariable("SteamAppId","108600");Environment.SetEnvironmentVariable("SteamGameId","108600");var init=Load<Init>(dll,GetProcAddress(dll,"SteamAPI_Init")==IntPtr.Zero?"SteamAPI_InitSafe":"SteamAPI_Init");shutdown=Load<VoidCall>(dll,"SteamAPI_Shutdown");if(!init())throw new Exception("Steam 初始化失败：请登录 Steam，并确认当前账号拥有游戏；未下载任何组件。");initialized=true;
    IntPtr ugc=IntPtr.Zero;for(int n=21;n>=15;n--){IntPtr p=GetProcAddress(dll,"SteamAPI_SteamUGC_v"+n.ToString("D3"));if(p!=IntPtr.Zero){ugc=((GetInterface)Marshal.GetDelegateForFunctionPointer(p,typeof(GetInterface)))();if(ugc!=IntPtr.Zero)break;}}if(ugc==IntPtr.Zero)throw new Exception("Steam UGC 接口不可用。");
    var download=Load<Download>(dll,"SteamAPI_ISteamUGC_DownloadItem");var info=Load<DownloadInfo>(dll,"SteamAPI_ISteamUGC_GetItemDownloadInfo");var install=Load<InstallInfo>(dll,"SteamAPI_ISteamUGC_GetItemInstallInfo");Load<VoidCall>(dll,"SteamAPI_ManualDispatch_Init")();int pipe=Load<GetPipe>(dll,"SteamAPI_GetHSteamPipe")();var frame=Load<PipeCall>(dll,"SteamAPI_ManualDispatch_RunFrame");var next=Load<Next>(dll,"SteamAPI_ManualDispatch_GetNextCallback");var free=Load<PipeCall>(dll,"SteamAPI_ManualDispatch_FreeLastCallback");var paths=new Dictionary<ulong,string>();
    var state=Load<ItemState>(dll,"SteamAPI_ISteamUGC_GetItemState");var subscribe=Load<Subscribe>(dll,"SteamAPI_ISteamUGC_SubscribeItem");IntPtr utils=Load<GetInterface>(dll,"SteamAPI_SteamUtils_v010")();var completed=Load<Completed>(dll,"SteamAPI_ISteamUtils_IsAPICallCompleted");var result=Load<Result>(dll,"SteamAPI_ISteamUtils_GetAPICallResult");
    for(int i=0;i<ids.Length;i++){ulong id=ids[i];cancel.ThrowIfCancellationRequested();if(progress!=null)progress(-1,"确认工坊订阅并下载 · "+ComponentName(id));
     EnsureSubscribed(id,item=>state(ugc,item),item=>{ulong call=subscribe(ugc,item);if(call==0)throw new Exception("Steam 无法发起订阅："+item);DateTime end=DateTime.UtcNow.AddSeconds(45);bool failed;while(!completed(utils,call,out failed)){cancel.ThrowIfCancellationRequested();if(DateTime.UtcNow>end)throw new Exception("Steam 订阅确认超时："+item);frame(pipe);Callback pending;while(next(pipe,out pending))free(pipe);Thread.Sleep(100);}if(failed)throw new Exception("Steam 订阅请求失败："+item);IntPtr buffer=Marshal.AllocHGlobal(16);try{if(!result(utils,call,buffer,16,1313,out failed)||failed)throw new Exception("无法验证 Steam 订阅结果："+item);uint code=(uint)Marshal.ReadInt32(buffer);ulong returned=(ulong)Marshal.ReadInt64(buffer,8);if(code!=1||returned!=item)throw new Exception("Steam 订阅被拒绝："+item+"，EResult="+code);}finally{Marshal.FreeHGlobal(buffer);}});
     if(subscriptionsOnly||(!force&&CanReuse(state(ugc,id)))){var existing=new StringBuilder(4096);ulong bytes;uint time;if(!install(ugc,id,out bytes,existing,(uint)existing.Capacity,out time)||!Directory.Exists(existing.ToString()))throw new Exception("订阅成功，但未找到安装文件："+id);paths.Add(id,existing.ToString());if(progress!=null)progress((i+1)*100/ids.Length,"本地已是最新，无需下载 · "+ComponentName(id));continue;}
     if(!download(ugc,id,false))throw new Exception("Steam 拒绝下载条目 "+id+"；请确认登录和工坊访问权限。");DateTime deadline=DateTime.UtcNow.AddSeconds(seconds),lastProgress=DateTime.MinValue;bool success=false;
     while(!success&&DateTime.UtcNow<deadline){cancel.ThrowIfCancellationRequested();frame(pipe);Callback cb;while(next(pipe,out cb)){try{if(cb.Id==3406&&cb.Size>=20){uint app=(uint)Marshal.ReadInt32(cb.Data,0);ulong item=(ulong)Marshal.ReadInt64(cb.Data,8);uint downloadResult=(uint)Marshal.ReadInt32(cb.Data,16);if(app==108600&&item==id){if(!SuccessResult(app,item,downloadResult,id))throw new Exception("Steam 下载失败：条目 "+id+"，EResult="+downloadResult+"（例如 15 为访问被拒绝）。当前游戏文件未替换。");success=true;}}}finally{free(pipe);}}
      ulong done,total;bool hasInfo=info(ugc,id,out done,out total);if(progress!=null&&(DateTime.UtcNow-lastProgress).TotalSeconds>=1){progress(!hasInfo||total==0?-1:(int)(100*(i+Math.Min(1,(double)done/total))/ids.Length),FormatDownloadProgress(id,hasInfo?done:0,hasInfo?total:0));lastProgress=DateTime.UtcNow;}if(!success)Thread.Sleep(100);
     }if(!success)throw new Exception("等待 Steam 下载超时："+id+"。未把旧文件标成更新完成。");var folder=new StringBuilder(4096);ulong size;uint stamp;if(!install(ugc,id,out size,folder,(uint)folder.Capacity,out stamp)||!Directory.Exists(folder.ToString()))throw new Exception("Steam 回报成功，但安装目录尚不可验证："+id);paths.Add(id,folder.ToString());
    }if(progress!=null)progress(100,"工坊下载成功并确认安装目录");return paths;
   }finally{if(initialized&&shutdown!=null)shutdown();Environment.SetEnvironmentVariable("SteamAppId",prior);Environment.SetEnvironmentVariable("SteamGameId",priorGame);FreeLibrary(dll);}
  }
 }
}
