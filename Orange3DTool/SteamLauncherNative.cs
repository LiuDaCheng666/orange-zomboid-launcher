using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PZ3D {
 // Flat Steam exports are cdecl; this implementation requires the game's x64 UGC021 ABI.
 sealed class SteamLauncherNative : IDisposable {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
  [DllImport("kernel32.dll",CharSet=CharSet.Ansi)]static extern IntPtr GetProcAddress(IntPtr module,string name);
  [DllImport("kernel32.dll")]static extern bool FreeLibrary(IntPtr module);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool Init();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void VoidCall();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr GetInterface();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr StringPointer(IntPtr self);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate uint UIntCall(IntPtr self);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate ulong ULongCall(IntPtr self);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool BoolCall(IntPtr self);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool OwnsApp(IntPtr self,uint app);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate uint Count(IntPtr self,[MarshalAs(UnmanagedType.I1)]bool includeDisabled);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate uint Items(IntPtr self,[Out,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=2)]ulong[] ids,uint capacity,[MarshalAs(UnmanagedType.I1)]bool includeDisabled);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate ulong DetailQuery(IntPtr self,[In,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=2)]ulong[] ids,uint count);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate ulong SearchQuery(IntPtr self,int order,int type,uint creator,uint consumer,uint page);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool QueryText(IntPtr self,ulong query,IntPtr text);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool QueryFlag(IntPtr self,ulong query,[MarshalAs(UnmanagedType.I1)]bool value);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate ulong SendQuery(IntPtr self,ulong query);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool QueryRelease(IntPtr self,ulong query);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool QueryResult(IntPtr self,ulong query,uint index,IntPtr bytes);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool QueryPreview(IntPtr self,ulong query,uint index,IntPtr buffer,uint size);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool QueryChildren(IntPtr self,ulong query,uint index,[Out,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=4)]ulong[] ids,uint count);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate uint ItemState(IntPtr self,ulong id);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate ulong Subscribe(IntPtr self,ulong id);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool Download(IntPtr self,ulong id,[MarshalAs(UnmanagedType.I1)]bool highPriority);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool DownloadInfo(IntPtr self,ulong id,out ulong done,out ulong total);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool InstallInfo(IntPtr self,ulong id,out ulong size,IntPtr folder,uint capacity,out uint stamp);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool Completed(IntPtr self,ulong call,[MarshalAs(UnmanagedType.I1)]out bool failure);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool Result(IntPtr self,ulong call,IntPtr bytes,int size,int callback,[MarshalAs(UnmanagedType.I1)]out bool failure);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int GetPipe();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void PipeCall(int pipe);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool NextCallback(int pipe,out Callback callback);
  [StructLayout(LayoutKind.Sequential,Pack=8)]struct Callback {public int User,Id;public IntPtr Data;public int Size;}
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr ServerRequest(IntPtr self,uint app,IntPtr filters,uint count,IntPtr response);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void ServerHandleCall(IntPtr self,IntPtr request);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int ServerCount(IntPtr self,IntPtr request);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate IntPtr ServerDetails(IntPtr self,IntPtr request,int index);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]delegate bool ServerRefreshing(IntPtr self,IntPtr request);
  // On Windows x64 C++ member calls use the same ABI, with the object pointer first.
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void ServerResponse(IntPtr self,IntPtr request,int value);
  sealed class ServerSink : IDisposable {
   public IntPtr Address;IntPtr table;ServerResponse responded,failed,complete;public int Completion=-1;
   public ServerSink(){responded=(self,request,value)=>{};failed=(self,request,value)=>{};complete=(self,request,value)=>{Completion=value;};table=Marshal.AllocHGlobal(IntPtr.Size*3);Address=Marshal.AllocHGlobal(IntPtr.Size);Marshal.WriteIntPtr(table,0,Marshal.GetFunctionPointerForDelegate(responded));Marshal.WriteIntPtr(table,IntPtr.Size,Marshal.GetFunctionPointerForDelegate(failed));Marshal.WriteIntPtr(table,IntPtr.Size*2,Marshal.GetFunctionPointerForDelegate(complete));Marshal.WriteIntPtr(Address,table);}
   public void Dispose(){if(Address!=IntPtr.Zero){Marshal.FreeHGlobal(Address);Address=IntPtr.Zero;}if(table!=IntPtr.Zero){Marshal.FreeHGlobal(table);table=IntPtr.Zero;}GC.KeepAlive(responded);GC.KeepAlive(failed);GC.KeepAlive(complete);}
  }
  IntPtr dll,ugc,utils,user,apps,friends;bool initialized;VoidCall shutdown;CancellationToken cancel;string oldApp,oldGame,game;int pipe;PipeCall frame,free;NextCallback next;Completed completed;Result result;
  readonly Dictionary<ulong,uint> downloads=new Dictionary<ulong,uint>();
  T Function<T>(string export)where T:class{IntPtr function=GetProcAddress(dll,export);if(function==IntPtr.Zero)throw new Exception("游戏 Steam API 缺少 "+export+"；请更新 Steam 和游戏。");return (T)(object)Marshal.GetDelegateForFunctionPointer(function,typeof(T));}
  IntPtr Interface(string name){IntPtr value=Function<GetInterface>(name)();if(value==IntPtr.Zero)throw new Exception("Steam 接口不可用："+name);return value;}
  public SteamLauncherNative(string gamePath,CancellationToken token){
   cancel=token;cancel.ThrowIfCancellationRequested();game=Path.GetFullPath(gamePath);
   if(!Environment.Is64BitProcess)throw new Exception("Steam 工坊需要 64 位启动器。");
   if(!GameDiscovery.IsGameFolder(game))throw new Exception("请先选择有效的 Project Zomboid 游戏安装。");
   string library=Path.Combine(game,"steam_api64.dll");if(!File.Exists(library)||(File.GetAttributes(library)&FileAttributes.ReparsePoint)!=0)throw new Exception("未找到游戏自带的 steam_api64.dll。");
   dll=LoadLibraryEx(library,IntPtr.Zero,0x1100);if(dll==IntPtr.Zero)throw new Exception("无法加载游戏 Steam API，错误 "+Marshal.GetLastWin32Error()+"。");
   oldApp=Environment.GetEnvironmentVariable("SteamAppId");oldGame=Environment.GetEnvironmentVariable("SteamGameId");
   try{
    Environment.SetEnvironmentVariable("SteamAppId","108600");Environment.SetEnvironmentVariable("SteamGameId","108600");shutdown=Function<VoidCall>("SteamAPI_Shutdown");string initName=GetProcAddress(dll,"SteamAPI_Init")!=IntPtr.Zero?"SteamAPI_Init":"SteamAPI_InitSafe";
    if(!Function<Init>(initName)())throw new Exception("Steam 连接失败；请在桌面 Steam 登录拥有 Project Zomboid 的账号。");initialized=true;
    utils=Interface("SteamAPI_SteamUtils_v010");user=Interface("SteamAPI_SteamUser_v023");apps=Interface("SteamAPI_SteamApps_v008");ugc=Interface("SteamAPI_SteamUGC_v021");friends=Interface("SteamAPI_SteamFriends_v018");
    RequireOnline();Function<VoidCall>("SteamAPI_ManualDispatch_Init")();pipe=Function<GetPipe>("SteamAPI_GetHSteamPipe")();if(pipe==0)throw new Exception("Steam 回调通道不可用。");frame=Function<PipeCall>("SteamAPI_ManualDispatch_RunFrame");next=Function<NextCallback>("SteamAPI_ManualDispatch_GetNextCallback");free=Function<PipeCall>("SteamAPI_ManualDispatch_FreeLastCallback");completed=Function<Completed>("SteamAPI_ISteamUtils_IsAPICallCompleted");result=Function<Result>("SteamAPI_ISteamUtils_GetAPICallResult");
   }catch{Dispose();throw;}
  }
  void RequireOnline(){cancel.ThrowIfCancellationRequested();if(Function<UIntCall>("SteamAPI_ISteamUtils_GetAppID")(utils)!=SteamLauncherServices.App||!Function<BoolCall>("SteamAPI_ISteamUser_BLoggedOn")(user)||!Function<OwnsApp>("SteamAPI_ISteamApps_BIsSubscribedApp")(apps,SteamLauncherServices.App))throw new Exception("需要 Steam 在线登录并拥有 App108600；没有请求订阅或下载。");}
  void Pump(){cancel.ThrowIfCancellationRequested();frame(pipe);Callback callback;while(next(pipe,out callback)){try{if(callback.Id==3406&&callback.Size>=24&&Marshal.ReadInt32(callback.Data,0)==108600)downloads[unchecked((ulong)Marshal.ReadInt64(callback.Data,8))]=unchecked((uint)Marshal.ReadInt32(callback.Data,16));}finally{free(pipe);}}}
  byte[] WaitCall(ulong call,int size,int callback,int seconds){
   if(call==0)throw new Exception("Steam 未接受请求。");DateTime end=DateTime.UtcNow.AddSeconds(seconds);bool failure;
   while(!completed(utils,call,out failure)){Pump();if(DateTime.UtcNow>end)throw new Exception("Steam 请求超时；请重试。");Thread.Sleep(50);}if(failure)throw new Exception("Steam 网络请求失败；请重试。");
   IntPtr buffer=Marshal.AllocHGlobal(size);try{if(!result(utils,call,buffer,size,callback,out failure)||failure)throw new Exception("Steam 返回的请求结果不可验证。");var bytes=new byte[size];Marshal.Copy(buffer,bytes,0,size);return bytes;}finally{Marshal.FreeHGlobal(buffer);}
  }
  byte[] ReadItem(ulong handle,uint index){IntPtr buffer=Marshal.AllocHGlobal(SteamLauncherServices.UgcBytes);try{var bytes=new byte[SteamLauncherServices.UgcBytes];Marshal.Copy(bytes,0,buffer,bytes.Length);if(!Function<QueryResult>("SteamAPI_ISteamUGC_GetQueryUGCResult")(ugc,handle,index,buffer))throw new Exception("无法读取工坊条目详情。");Marshal.Copy(buffer,bytes,0,bytes.Length);return bytes;}finally{Marshal.FreeHGlobal(buffer);}}
  string Preview(ulong handle,uint index){IntPtr buffer=Marshal.AllocHGlobal(4096);try{Marshal.WriteByte(buffer,0);if(!Function<QueryPreview>("SteamAPI_ISteamUGC_GetQueryUGCPreviewURL")(ugc,handle,index,buffer,4096))return "";var bytes=new byte[4096];Marshal.Copy(buffer,bytes,0,bytes.Length);return SteamLauncherServices.Utf8(bytes,0,bytes.Length);}finally{Marshal.FreeHGlobal(buffer);}}
  ulong[] Children(ulong handle,uint index,byte[] item){uint count=BitConverter.ToUInt32(item,9768);if(count>1000)throw new Exception("工坊依赖数量超出本次查询范围。");var children=new ulong[count];if(count>0&&!Function<QueryChildren>("SteamAPI_ISteamUGC_GetQueryUGCChildren")(ugc,handle,index,children,count))throw new Exception("Steam 未返回完整工坊依赖。");return children;}
  sealed class QueryPage {public List<object> Items=new List<object>();public Dictionary<ulong,byte[]> Raw=new Dictionary<ulong,byte[]>();public uint Total;}
  QueryPage Query(ulong handle,bool children,bool subscriptions=false){
   if(handle==0||handle==ulong.MaxValue)throw new Exception("Steam 无法创建工坊查询。");
   try{
    if(!Function<QueryFlag>("SteamAPI_ISteamUGC_SetReturnLongDescription")(ugc,handle,true))throw new Exception("Steam 无法请求完整工坊描述。");if(children&&!Function<QueryFlag>("SteamAPI_ISteamUGC_SetReturnChildren")(ugc,handle,true))throw new Exception("Steam 无法请求工坊依赖。");
    var response=WaitCall(Function<SendQuery>("SteamAPI_ISteamUGC_SendQueryUGCRequest")(ugc,handle),280,3401,25);
    if(BitConverter.ToUInt64(response,0)!=handle||BitConverter.ToInt32(response,8)!=1)throw new Exception("Steam 工坊查询失败，EResult="+BitConverter.ToInt32(response,8)+"。");uint count=BitConverter.ToUInt32(response,12);if(count>1000)throw new Exception("Steam 工坊查询返回数量异常。");var page=new QueryPage{Total=BitConverter.ToUInt32(response,16)};
    for(uint i=0;i<count;i++){cancel.ThrowIfCancellationRequested();byte[] bytes=ReadItem(handle,i);ulong id=BitConverter.ToUInt64(bytes,0);uint state=Function<ItemState>("SteamAPI_ISteamUGC_GetItemState")(ugc,id);var item=subscriptions?SteamLauncherServices.ParseSubscribedUgc(bytes,state,Preview(handle,i),new ulong[0]):SteamLauncherServices.ParseUgc(bytes,state,Preview(handle,i),children?Children(handle,i,bytes):new ulong[0]);page.Items.Add(item);if(page.Raw.ContainsKey(id))throw new Exception("Steam 工坊查询含重复条目。");page.Raw.Add(id,bytes);}return page;
   }finally{Function<QueryRelease>("SteamAPI_ISteamUGC_ReleaseQueryUGCRequest")(ugc,handle);}
  }
  QueryPage Details(ulong[] ids,bool children,bool subscriptions=false){return Query(Function<DetailQuery>("SteamAPI_ISteamUGC_CreateQueryUGCDetailsRequest")(ugc,ids,(uint)ids.Length),children,subscriptions);}
  object Subscriptions(Action<int,string> progress){
   var count=Function<Count>("SteamAPI_ISteamUGC_GetNumSubscribedItems");var get=Function<Items>("SteamAPI_ISteamUGC_GetSubscribedItems");uint total=count(ugc,true);if(total>10000)throw new Exception("订阅数量超过本次完整查询的安全范围。");var ids=new ulong[total];uint returned=total==0?0:get(ugc,ids,total,true);var confirm=new ulong[total];uint twice=total==0?0:get(ugc,confirm,total,true);
   if(returned!=total||twice!=total||count(ugc,true)!=total||ids.Any(id=>id==0)||ids.Distinct().Count()!=ids.Length||!ids.OrderBy(id=>id).SequenceEqual(confirm.OrderBy(id=>id)))throw new Exception("Steam 订阅列表变化或不完整，请重试。");
   var items=new List<object>();for(int at=0;at<ids.Length;at+=50){if(progress!=null)progress(-1,"正在读取已订阅条目 · "+Math.Min(ids.Length,at+50)+" / "+ids.Length);ulong[] batch=ids.Skip(at).Take(50).ToArray();var found=Details(batch,false,true);if(found.Raw.Count!=batch.Length||batch.Any(id=>!found.Raw.ContainsKey(id)))throw new Exception("Steam 未返回完整订阅条目详情，请重试。");items.AddRange(found.Items);}RequireOnline();return new{items=items,message=total==0?"当前 Steam 账号的 App108600 订阅列表为空。":"已读取当前 Steam 账号的完整订阅列表。"};
  }
  object Search(string query,int page){
   ulong handle=Function<SearchQuery>("SteamAPI_ISteamUGC_CreateQueryAllUGCRequestPage")(ugc,query.Length==0?1:11,0,SteamLauncherServices.App,SteamLauncherServices.App,(uint)page);
   if(query.Length>0&&handle!=0&&handle!=ulong.MaxValue){IntPtr text=Marshal.AllocHGlobal(Encoding.UTF8.GetByteCount(query)+1);try{var bytes=Encoding.UTF8.GetBytes(query+"\0");Marshal.Copy(bytes,0,text,bytes.Length);if(!Function<QueryText>("SteamAPI_ISteamUGC_SetSearchText")(ugc,handle,text))throw new Exception("Steam 拒绝工坊搜索文本。");}catch{Function<QueryRelease>("SteamAPI_ISteamUGC_ReleaseQueryUGCRequest")(ugc,handle);throw;}finally{Marshal.FreeHGlobal(text);}}
   var result=Query(handle,false);return new{items=result.Items,page=page,totalPages=(int)Math.Ceiling(result.Total/50.0),total=result.Total,message="Steam App108600 工坊实时查询。"};
  }
  bool Installed(ulong id,out string folder){
   IntPtr buffer=Marshal.AllocHGlobal(4096);try{Marshal.WriteByte(buffer,0);ulong size;uint stamp;bool found=Function<InstallInfo>("SteamAPI_ISteamUGC_GetItemInstallInfo")(ugc,id,out size,buffer,4096,out stamp);var bytes=new byte[4096];Marshal.Copy(buffer,bytes,0,bytes.Length);folder=found?SteamLauncherServices.Utf8(bytes,0,bytes.Length):"";string expected=Path.GetFullPath(Path.Combine(game,"..","..","workshop","content","108600",id.ToString(CultureInfo.InvariantCulture)));return found&&Directory.Exists(folder)&&string.Equals(Path.GetFullPath(folder).TrimEnd('\\'),expected.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase);}finally{Marshal.FreeHGlobal(buffer);}
  }
  void Mutate(ulong id,Action<int,string> progress){
   RequireOnline();if(GameProcessGuard.IsRunning(game))throw new Exception("请退出游戏后订阅或下载工坊模组。");var state=Function<ItemState>("SteamAPI_ISteamUGC_GetItemState");
   SteamWorkshop.EnsureSubscribed(id,item=>state(ugc,item),item=>{if(progress!=null)progress(-1,"正在确认所选条目的 Steam 订阅");byte[] subscribed=WaitCall(Function<Subscribe>("SteamAPI_ISteamUGC_SubscribeItem")(ugc,item),16,1313,30);if(BitConverter.ToInt32(subscribed,0)!=1||BitConverter.ToUInt64(subscribed,8)!=item)throw new Exception("Steam 订阅被拒绝，EResult="+BitConverter.ToInt32(subscribed,0)+"。");});
   string folder;if(SteamWorkshop.CanReuse(state(ugc,id))&&Installed(id,out folder)){if(progress!=null)progress(100,"所选工坊条目已订阅且安装为最新版本。");return;}
   downloads.Remove(id);if(!Function<Download>("SteamAPI_ISteamUGC_DownloadItem")(ugc,id,false))throw new Exception("Steam 拒绝下载所选条目。");DateTime end=DateTime.UtcNow.AddSeconds(420),last=DateTime.MinValue;var downloadInfo=Function<DownloadInfo>("SteamAPI_ISteamUGC_GetItemDownloadInfo");
   while(DateTime.UtcNow<end){Pump();uint code;if(downloads.TryGetValue(id,out code)){if(!SteamWorkshop.SuccessResult(SteamLauncherServices.App,id,code,id))throw new Exception("Steam 下载失败，EResult="+code+"。已确认的订阅可能保留；未激活模组。");if(Installed(id,out folder)){if(progress!=null)progress(100,"Steam 已确认下载和 App108600 安装目录。");return;}}
    ulong done,total;bool known=downloadInfo(ugc,id,out done,out total);if(progress!=null&&(DateTime.UtcNow-last).TotalMilliseconds>=900){progress(!known||total==0?-1:(int)Math.Min(99,100.0*done/total),SteamWorkshop.FormatDownloadProgress(id,known?done:0,known?total:0));last=DateTime.UtcNow;}Thread.Sleep(100);
   }throw new Exception("Steam 下载等待超时；已确认的订阅可能保留，请在 Steam 检查进度。未激活模组。");
  }
  object Hall(string query,Action<int,string> progress){
   IntPtr servers=Interface("SteamAPI_SteamMatchmakingServers_v002"),request=IntPtr.Zero;var release=Function<ServerHandleCall>("SteamAPI_ISteamMatchmakingServers_ReleaseRequest");var cancelRequest=Function<ServerHandleCall>("SteamAPI_ISteamMatchmakingServers_CancelQuery");var refreshing=Function<ServerRefreshing>("SteamAPI_ISteamMatchmakingServers_IsRefreshing");
   using(var sink=new ServerSink())try{
    request=Function<ServerRequest>("SteamAPI_ISteamMatchmakingServers_RequestInternetServerList")(servers,SteamLauncherServices.App,IntPtr.Zero,0,sink.Address);if(request==IntPtr.Zero)throw new Exception("Steam 无法请求公共服务器列表。");DateTime end=DateTime.UtcNow.AddSeconds(25);bool timedOut=false;
    do{Pump();if(sink.Completion>=0||!refreshing(servers,request))break;if(DateTime.UtcNow>end){timedOut=true;break;}Thread.Sleep(50);}while(true);
    if(sink.Completion==1)throw new Exception("Steam 公共服务器查询未成功，请重试。");
    int count=Function<ServerCount>("SteamAPI_ISteamMatchmakingServers_GetServerCount")(servers,request);if(count<0||count>30000)throw new Exception("Steam 服务器数量异常。");var details=Function<ServerDetails>("SteamAPI_ISteamMatchmakingServers_GetServerDetails");var found=new List<object>();
    for(int i=0;i<count;i++){cancel.ThrowIfCancellationRequested();IntPtr pointer=details(servers,request,i);if(pointer==IntPtr.Zero)continue;var bytes=new byte[SteamLauncherServices.ServerBytes];Marshal.Copy(pointer,bytes,0,bytes.Length);object row=SteamLauncherServices.ParseServer(bytes);if(row==null)continue;string name=SteamLauncherServices.Utf8(bytes,172,64),description=SteamLauncherServices.Utf8(bytes,78,64);if(query.Length>0&&name.IndexOf(query,StringComparison.OrdinalIgnoreCase)<0&&description.IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;found.Add(row);if(found.Count>=1000)break;}
    if(timedOut&&found.Count==0)throw new Exception("Steam 公共服务器查询超时且尚未收到有效服务器，请重试。");
    if(progress!=null)progress(-1,"Steam 公共服务器已回应 · "+found.Count+" 项");return new{servers=found,message=timedOut?"Steam 已返回部分有效服务器；本次查询达到 25 秒时限，可刷新重试。":found.Count>=1000?"已显示本次查询的前 1000 个有效服务器，可使用名称筛选。":found.Count==0?"Steam 本次查询没有返回匹配的有效服务器。":"已读取 Steam App108600 公共服务器列表。"};
   }finally{if(request!=IntPtr.Zero){try{cancelRequest(servers,request);}finally{release(servers,request);}}}
  }
  public object Execute(string action,Dictionary<string,object> payload,Action<int,string> progress){
   if(action=="connection"){IntPtr name=Function<StringPointer>("SteamAPI_ISteamFriends_GetPersonaName")(friends);return new{available=true,accountName=PointerUtf8(name,512),steamId=Function<ULongCall>("SteamAPI_ISteamUser_GetSteamID")(user).ToString(CultureInfo.InvariantCulture),message="已连接桌面 Steam；每次请求结束后释放游戏身份。"};}
   if(action=="subscriptions")return Subscriptions(progress);
   if(action=="search")return Search(Convert.ToString(payload["query"]),Convert.ToInt32(payload["page"]));
   if(action=="details"){ulong id=ulong.Parse(Convert.ToString(payload["id"]),CultureInfo.InvariantCulture);var page=Details(new[]{id},true);if(page.Items.Count!=1||!page.Raw.ContainsKey(id))throw new Exception("Steam 未返回所选工坊条目。");return new{item=page.Items[0],message="工坊详情来自 Steam。"};}
   if(action=="download"){ulong id=ulong.Parse(Convert.ToString(payload["id"]),CultureInfo.InvariantCulture);SteamLauncherServices.DownloadVerified(id,(bool)payload["confirmed"],item=>{var page=Details(new[]{item},false);byte[] bytes;if(page.Raw.Count!=1||!page.Raw.TryGetValue(item,out bytes))throw new Exception("Steam 详情 ID 无法验证。");return bytes;},item=>Mutate(item,progress));return new{id=id.ToString(CultureInfo.InvariantCulture),message="Steam 已订阅并确认所选条目安装；可在模组配置中手动启用。"};}
   if(action=="hall")return Hall(Convert.ToString(payload["query"]),progress);throw new Exception("不支持的 Steam 操作。");
  }
  static string PointerUtf8(IntPtr pointer,int maximum){if(pointer==IntPtr.Zero)return "";int length=0;while(length<maximum&&Marshal.ReadByte(pointer,length)!=0)length++;var bytes=new byte[length];Marshal.Copy(pointer,bytes,0,length);return Encoding.UTF8.GetString(bytes);}
  public void Dispose(){try{if(initialized&&shutdown!=null)shutdown();}finally{initialized=false;Environment.SetEnvironmentVariable("SteamAppId",oldApp);Environment.SetEnvironmentVariable("SteamGameId",oldGame);if(dll!=IntPtr.Zero){FreeLibrary(dll);dll=IntPtr.Zero;}}}
 }
}
