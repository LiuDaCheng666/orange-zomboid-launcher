using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Script.Serialization;

// Read-only adapter for actual production Preview/Cancel. The real controller is
// loaded with TestMode=false; every other action and every other save is refused.
static class SaveWorkflowProgressBridge {
 static readonly object OutputGate=new object();
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=8388608};
 static object Controller;static MethodInfo Execute;static string SaveId;
 static void Send(object value){lock(OutputGate){Console.WriteLine(Json.Serialize(value));Console.Out.Flush();}}
 static async Task Handle(string line){object id=null;try{
  var request=Json.Deserialize<Dictionary<string,object>>(line);id=request["id"];
  string action=(string)request["action"];var payload=(Dictionary<string,object>)request["payload"];
  if(action!="environmentPreview"&&action!="environmentPreviewCancel")throw new IOException("Read-only bridge refuses action: "+action);
  if(action=="environmentPreview"&&(!payload.ContainsKey("saveId")||!string.Equals(payload["saveId"] as string,SaveId,StringComparison.Ordinal)))throw new IOException("Read-only bridge refuses another save");
  object result=await Task.Run(()=>Execute.Invoke(Controller,new object[]{action,payload}));Send(new{id=id,ok=true,data=result});
 }catch(Exception error){var actual=error is TargetInvocationException&&error.InnerException!=null?error.InnerException:error;Send(new{id=id,ok=false,error=actual.Message,type=actual.GetType().FullName});}}
 public static int Main(string[] args){try{
  if(args.Length!=5)throw new IOException("Expected assembly, actual game, actual user, isolated data, and exact save id");
  Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);
  var assembly=Assembly.LoadFrom(Path.GetFullPath(args[0]));var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
  assembly.GetType("PZ3D.LauncherRuntime",true).GetMethod("Initialize",flags).Invoke(null,new object[0]);
  var type=assembly.GetType("PZ3D.ModernController",true);SaveId=args[4];
  Controller=Activator.CreateInstance(type,flags,null,new object[]{args[1],args[2],args[3],false},null);Execute=type.GetMethod("Execute",flags);
  type.GetField("Event",flags).SetValue(Controller,new Action<string,object>((name,value)=>Send(new{@event=name,data=value})));
  var tasks=new List<Task>();string line;while((line=Console.ReadLine())!=null){tasks.RemoveAll(task=>task.IsCompleted);tasks.Add(Handle(line));}
  Task.WaitAll(tasks.ToArray());((IDisposable)Controller).Dispose();return 0;
 }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
