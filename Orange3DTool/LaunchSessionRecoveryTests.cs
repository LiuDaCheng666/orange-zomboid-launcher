using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Web.Script.Serialization;
namespace PZ3D{static class LaunchSessionRecoveryTests{
 static int checks;static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
 static void Put(string p,byte[] b){Directory.CreateDirectory(Path.GetDirectoryName(p));File.WriteAllBytes(p,b);}
 static byte[] Bytes(string s){return Encoding.UTF8.GetBytes(s);}
 static void ServerBridge(string root){
  var e=new Engine(Path.Combine(root,"server-game"),Path.Combine(root,"server-user"),Path.Combine(root,"server-data"),true);
  string nonce=Guid.NewGuid().ToString("N"),runtime=Path.Combine(e.Data,"server-sessions",LaunchProfiles.Key(e)),session=Path.Combine(runtime,"runtime","live-"+nonce.Substring(0,12)),bridge=Path.Combine(session,"bridge");
  string[] files={Path.Combine(bridge,"common","mod.info"),Path.Combine(bridge,"42","mod.info"),Path.Combine(bridge,"42","media","lua","client","OrangeLaunchBridge.lua")};
  var r=new ProfileSessionRecord{Game=e.Game,User=e.User,Token=LaunchProfiles.Key(e),Nonce=nonce,Kind="server",ServerComponents="follow-server",ServerRuntimeRoot=runtime,SessionDirectory=session,BridgeDirectory=bridge,State="launched",CreatedUtc=DateTime.UtcNow.AddMinutes(-10).ToString("o")};
  foreach(string file in files){byte[] bytes=Bytes("previous release bridge bytes");Put(file,bytes);r.Files[file]=DlssInstaller.Hash(bytes);}Put(e.Mods,Bytes("untouched player default list"));Put(e.Config,Bytes("untouched game configuration"));
  string journal=LaunchProfiles.Journal(e);var json=new JavaScriptSerializer();Put(journal,Bytes(json.Serialize(r)));
  Check(LaunchProfiles.ServerSession(e,nonce)!=null,"assetless cached worker reads a server record from an earlier release");
  string good=json.Serialize(r),foreign=Path.Combine(root,"foreign-player.txt");Put(foreign,Bytes("keep player file"));
  foreach(string bad in new[]{"extra","missing","hash","path"}){
   var invalid=json.Deserialize<ProfileSessionRecord>(good);
   if(bad=="extra")invalid.Files[foreign]=new string('A',64);
   if(bad=="missing")invalid.Files.Remove(files[0]);
   if(bad=="hash")invalid.Files[files[0]]="invalid";
   if(bad=="path"){invalid.Files.Remove(files[0]);invalid.Files[foreign]=new string('A',64);}
   Put(journal,Bytes(json.Serialize(invalid)));bool denied=false;try{LaunchProfiles.Recover(e,true);}catch{denied=true;}
   Check(denied&&LaunchProfiles.Pending(e)&&Directory.Exists(session)&&File.ReadAllText(foreign)=="keep player file","invalid server "+bad+" retains evidence and never deletes a foreign target");
  }
  Put(journal,Bytes(good));string result=LaunchProfiles.Recover(e,true);
  Check(!LaunchProfiles.Pending(e)&&!Directory.Exists(session),"assetless old server session is retired without current bundled assets");
  Check(File.ReadAllText(e.Mods)=="untouched player default list"&&File.ReadAllText(e.Config)=="untouched game configuration"&&result.Contains("已清理"),"server recovery preserves player defaults and game configuration");
 }
 public static int Main(string[] args){if(args.Length!=0)return 2;string root=Path.Combine(Path.GetTempPath(),"ozl-session-recovery-"+Guid.NewGuid().ToString("N"));try{foreach(bool legacy in new[]{false,true}){string fixture=Path.Combine(root,legacy?"legacy":"b42");var e=new Engine(Path.Combine(fixture,"game"),Path.Combine(fixture,"user"),Path.Combine(fixture,"data"),true);byte[] original=Bytes("VERSION = 1,\nmods\n{\n    mod = PlayerMod,\n}\nmaps\n{\n}\n"),applied=Bytes(LaunchProfiles.BuildDefault(Encoding.UTF8.GetString(original),"normal",new string[0]));Put(e.Config,Bytes("{\"vmArgs\":[]}"));Put(e.Mods,applied);string mod=Path.Combine(e.User,"mods","OrangeLauncherSession");string[] files=legacy?new[]{Path.Combine(mod,"mod.info"),Path.Combine(mod,"media","lua","client","OrangeLaunchBridge.lua")}:new[]{Path.Combine(mod,"common","mod.info"),Path.Combine(mod,"42","mod.info"),Path.Combine(mod,"42","media","lua","client","OrangeLaunchBridge.lua")};var owned=new Dictionary<string,string>();foreach(var file in files){byte[] b=Bytes("owned fixture bridge");Put(file,b);owned[file]=DlssInstaller.Hash(b);}string foreign=Path.Combine(mod,"keep-player.txt");Put(foreign,Bytes("player file"));var r=new ProfileSessionRecord{Game=e.Game,User=e.User,Original=Convert.ToBase64String(original),AppliedHash=DlssInstaller.Hash(applied),CreatedUtc=DateTime.UtcNow.AddMinutes(-10).ToString("o"),State="launched",Kind="server",Token=LaunchProfiles.Key(e),Nonce=Guid.NewGuid().ToString("N"),Files=owned};Put(LaunchProfiles.Journal(e),Bytes(new JavaScriptSerializer().Serialize(r)));
 string result=LaunchProfiles.Recover(e,false,r.Nonce);Check(!LaunchProfiles.Pending(e),"assetless cached worker retires ended session");Check(File.ReadAllBytes(e.Mods).SequenceEqual(original),"restores exact original default list");Check(files.All(f=>!File.Exists(f)),"removes only matching owned bridge files");Check(File.ReadAllText(foreign)=="player file","preserves additional player file");Check(result.Contains("已恢复"),"reports successful recovery");
 // An extra journal target must remain rejected even without asset lookup.
 r.Files[foreign]=DlssInstaller.Hash(File.ReadAllBytes(foreign));Put(LaunchProfiles.Journal(e),Bytes(new JavaScriptSerializer().Serialize(r)));bool denied=false;try{LaunchProfiles.Recover(e,false,r.Nonce);}catch{denied=true;}Check(denied&&File.Exists(foreign)&&LaunchProfiles.Pending(e),"foreign journal target rejected with evidence retained");}
 ServerBridge(root);Console.WriteLine("PASS assetless cached-session recovery "+checks+" checks");return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}finally{if(Directory.Exists(root)&&Path.GetFullPath(root).StartsWith(Path.Combine(Path.GetFullPath(Path.GetTempPath()),"ozl-session-recovery-"),StringComparison.OrdinalIgnoreCase)){LauncherMaintenance.NoReparse(root);Directory.Delete(root,true);}}}
}}
