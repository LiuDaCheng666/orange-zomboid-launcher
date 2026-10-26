using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
namespace PZ3D {
 // Choosing a folder is read-only. The actual change still enters the native
 // mutation queue after the picker has returned.
 internal static class LibraryFolderSelection {
  internal sealed class Request {
   internal readonly string Action,Key,Description;
   readonly Dictionary<string,object> payload;
   internal Request(string action,string key,string description,Dictionary<string,object> source){Action=action;Key=key;Description=description;payload=new Dictionary<string,object>(source);}
   internal Dictionary<string,object> Select(string folder){
    if(string.IsNullOrWhiteSpace(folder)||!Path.IsPathRooted(folder))throw new IOException("请选择保存位置。");
    var selected=new Dictionary<string,object>(payload);string path=Path.GetFullPath(folder);
    selected[Key]=Key=="destination"?Path.Combine(path,(Action=="environmentCreate"?"OZL-快照-":"ProjectZomboid-")+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)):path;
    if(ModernController.EnvironmentAction(Action))ModernController.ValidateEnvironmentPayload(Action,selected);else ModernController.ValidateLibraryPayload(Action,selected);
    return selected;
   }
  }
  internal static Request Begin(string action,Dictionary<string,object> payload){
   string mapped,key,description;
   switch(action){
    case "environmentChooseCreate":mapped="environmentCreate";key="destination";description="选择游戏与模组快照保存位置";break;
    case "versionChooseImport":mapped="versionImport";key="path";description="选择已经安装的游戏版本文件夹";break;
    case "versionChoosePreserve":mapped="versionPreserve";key="destination";description="选择存放独立游戏版本的位置";break;
    case "versionChooseInstall":mapped="versionInstallBegin";key="destination";description="选择新版本的保存位置";break;
    case "backupChooseDirectory":mapped="backupSettingsSave";key="directory";description="选择存档备份位置";break;
    default:return null;
   }
   if(payload==null||payload.ContainsKey(key))throw new IOException("请使用文件夹选择器。");
   var selected=new Dictionary<string,object>(payload);selected[key]=Path.GetFullPath("preview");
   if(ModernController.EnvironmentAction(mapped)){ModernController.ValidateEnvironmentWebPayload(action,payload);ModernController.ValidateEnvironmentPayload(mapped,selected);}else ModernController.ValidateLibraryPayload(mapped,selected);
   return new Request(mapped,key,description,payload);
  }
 }
 sealed partial class ModernForm {
  bool PrepareLibraryFolder(ref string action,ref Dictionary<string,object> payload){
   var selection=LibraryFolderSelection.Begin(action,payload);if(selection==null)return true;
   using(var picker=new FolderBrowserDialog{Description=selection.Description,ShowNewFolderButton=true}){
    if(picker.ShowDialog(this)!=DialogResult.OK)return false;
    payload=selection.Select(picker.SelectedPath);
   }
   action=selection.Action;return true;
  }
 }
}
