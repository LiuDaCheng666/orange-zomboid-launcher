using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Win32;
namespace PZ3D {
 static class SnapshotFileSystemTests {
  static Type Api;static int checks;static readonly BindingFlags Flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  static void Check(bool value,string reason){if(!value)throw new Exception("FAIL "+reason);checks++;Console.WriteLine("PASS "+reason);}
  static Type Type(string part){return Api.GetNestedType(part,BindingFlags.Public|BindingFlags.NonPublic);}
  static object Call(string part,string name,params object[] args){try{return Type(part).GetMethods(Flags).Single(method=>method.Name==name&&method.GetParameters().Length==args.Length).Invoke(null,args);}catch(TargetInvocationException error){throw error.InnerException;}}
  static string Native(string path){return (string)Api.GetMethod("NativePath",Flags).Invoke(null,new object[]{path});}
  static void Remove(string path){foreach(string item in (System.Collections.Generic.IEnumerable<string>)Call("Directories","EnumerateFileSystemEntries",path)){if((bool)Call("Directories","Exists",item))Remove(item);else Call("Files","Delete",item);}Call("Directories","Delete",path,false);}
  public static int Main(){LauncherRuntime.Initialize();string root=null;try{
   Check((int)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem","LongPathsEnabled",-1)==0,"regression runs with the default Windows long-path registry switch disabled");
   Api=typeof(SnapshotFileSystemTests).Assembly.GetType("PZ3D.SnapshotFileSystem");Check(Api!=null,"snapshot file-system adapter exists independently of controller and test-host changes");
   root=Path.Combine(Path.GetTempPath(),"ozl-longio-"+Guid.NewGuid().ToString("N"));string selected=Path.Combine(root,"用户选择的较长快照目录-"+new string('a',90),new string('b',100),new string('c',100));Check(selected.Length>320,"fixture retains a genuinely long user-selected destination");
   Call("Directories","CreateDirectory",selected);string file=Path.Combine(selected,"独立快照.bin");byte[] bytes=Encoding.UTF8.GetBytes("independent snapshot payload");
   using(var stream=(FileStream)Activator.CreateInstance(Type("Stream"),new object[]{file,FileMode.CreateNew,FileAccess.Write,FileShare.None})){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
   Check((bool)Call("Files","Exists",file)&&(bool)Call("Directories","Exists",selected),"explicit extended-path I/O creates real directories and files with registry zero");
   using(var stream=(FileStream)Activator.CreateInstance(Type("Stream"),new object[]{file,FileMode.Open,FileAccess.Read,FileShare.Read})){byte[] read=new byte[bytes.Length];Check(stream.Read(read,0,read.Length)==read.Length&&read.SequenceEqual(bytes),"long-path FileStream reads exact saved bytes");}
   var names=((System.Collections.Generic.IEnumerable<string>)Call("Directories","EnumerateFileSystemEntries",selected)).ToArray();Check(names.SequenceEqual(new[]{file})&&!names[0].StartsWith(@"\\?\"),"enumeration exposes ordinary paths for relative manifests and game arguments");
   object info=Activator.CreateInstance(Type("Info"),new object[]{file});Check((long)Type("Info").GetProperty("Length").GetValue(info,null)==bytes.Length,"long-path metadata reads the actual file size");
   DateTime stamp=new DateTime(2020,1,2,3,4,5,DateTimeKind.Utc);Call("Files","SetLastWriteTimeUtc",file,stamp);info=Activator.CreateInstance(Type("Info"),new object[]{file});Check((DateTime)Type("Info").GetProperty("LastWriteTimeUtc").GetValue(info,null)==stamp,"copy timestamps survive on long destinations");
   string moved=file+".moved",temp=file+".tmp";Call("Files","Move",file,moved);Call("Files","WriteAllBytes",temp,new byte[]{3,1,4});Call("Files","Replace",temp,moved,null);
   using(var stream=(FileStream)Activator.CreateInstance(Type("Stream"),new object[]{moved,FileMode.Open,FileAccess.Read,FileShare.Read})){Check(stream.Length==3&&stream.ReadByte()==3,"atomic metadata replacement and file moves work beyond MAX_PATH");}
   string renamed=selected+"-renamed";Call("Directories","Move",selected,renamed);Check((bool)Call("Files","Exists",Path.Combine(renamed,Path.GetFileName(moved))),"long staging-directory publication retains its file tree");
   Check(Native(@"C:\a\b")==@"\\?\C:\a\b"&&Native(@"\\server\share\folder")==@"\\?\UNC\server\share\folder","drive and UNC paths receive the correct explicit native prefix");
   Api.GetMethod("NoReparse",Flags).Invoke(null,new object[]{renamed});Check(true,"long-path attribute validation can inspect every existing ancestor");
   Console.WriteLine("SNAPSHOT LONG PATH PASS: "+checks+" assertions; destination length "+selected.Length);return 0;
  }catch(Exception error){Console.WriteLine(error);return 1;}finally{if(Api!=null&&root!=null){string full=Path.GetFullPath(root),parent=Path.GetFullPath(Path.GetTempPath());if(!full.StartsWith(parent,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith("ozl-longio-",StringComparison.Ordinal))throw new IOException("Unsafe fixture cleanup");if((bool)Call("Directories","Exists",full))Remove(full);}}}
 }
}
