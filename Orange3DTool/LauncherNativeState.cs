using System;using System.IO;using System.Text;using System.Threading;using System.Web.Script.Serialization;
namespace PZ3D {static class LauncherNativeState {
 public static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=2097152,RecursionLimit=32};
 public static T Read<T>(string file,Func<T> empty){return Locked(file,delegate{return ReadCore(file,empty);});}
 static T ReadCore<T>(string file,Func<T> empty){
  LauncherMaintenance.NoReparse(file);if(!File.Exists(file))return empty();
  // Serialize only this record with the existing per-file mutex: replacement can
  // briefly hide its name. Other files and launcher operations remain independent.
  using(var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
   if(input.Length>2097152)throw new Exception("本地记录超出大小限制，未覆盖。");
   try{using(var reader=new StreamReader(input,new UTF8Encoding(false,true)))return Json.Deserialize<T>(reader.ReadToEnd());}
   catch(Exception error){throw new InvalidDataException("本地记录损坏，未覆盖："+Path.GetFileName(file),error);}
  }
 }
 public static void Write(string file,object value){Locked(file,delegate{WriteCore(file,value);return true;});}
 static void WriteCore(string file,object value){LauncherMaintenance.NoReparse(file);Directory.CreateDirectory(Path.GetDirectoryName(file));byte[] bytes=Encoding.UTF8.GetBytes(Json.Serialize(value));if(bytes.Length>2097152)throw new Exception("本地记录超过容量限制。");string temp=file+".new-"+Guid.NewGuid().ToString("N");try{using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}LauncherMaintenance.NoReparse(file);if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public static T Locked<T>(string file,Func<T> work){string key=DlssInstaller.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(file).ToUpperInvariant()));using(var mutex=new Mutex(false,"Local\\OrangeState-"+key)){bool held=false;try{try{held=mutex.WaitOne(5000);}catch(AbandonedMutexException){held=true;}if(!held)throw new Exception("本地记录正被其他窗口处理，请稍后重试。");return work();}finally{if(held)mutex.ReleaseMutex();}}}
}}
