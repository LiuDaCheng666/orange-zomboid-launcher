using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
namespace PZ3D {
 sealed partial class ModernForm {
  bool PrepareLibraryFolder(ref string action,ref Dictionary<string,object> payload){
   string mapped,key,description;
   switch(action){
    case "versionChooseImport":mapped="versionImport";key="path";description="选择已经安装的游戏版本文件夹";break;
    case "versionChoosePreserve":mapped="versionPreserve";key="destination";description="选择存放独立游戏版本的位置";break;
    case "versionChooseInstall":mapped="versionInstallBegin";key="destination";description="选择新版本的保存位置";break;
    case "backupChooseDirectory":mapped="backupSettingsSave";key="directory";description="选择存档备份位置";break;
    default:return true;
   }
   if(controller.Busy)throw new IOException("请等待当前任务完成。");
   if(payload.ContainsKey(key))throw new IOException("请使用文件夹选择器。");var selected=new Dictionary<string,object>(payload);selected[key]="preview";ModernController.ValidateLibraryPayload(mapped,selected);
   using(var picker=new FolderBrowserDialog{Description=description,ShowNewFolderButton=true}){
    if(picker.ShowDialog(this)!=DialogResult.OK)return false;
    selected[key]=key=="destination"?Path.Combine(picker.SelectedPath,"ProjectZomboid-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)):picker.SelectedPath;
   }
   action=mapped;payload=selected;return true;
  }
 }
}
