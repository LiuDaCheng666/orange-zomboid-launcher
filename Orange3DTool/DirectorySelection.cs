using System;
using System.Windows.Forms;
namespace PZ3D {
 sealed partial class MainForm {
  void ChooseGameDirectory(){if(busy)throw new Exception("请等待当前操作完成再切换目录。");using(var dialog=new FolderBrowserDialog()){dialog.Description="请选择包含 ProjectZomboid64.exe 与 ProjectZomboid64.json 的游戏安装目录（不是 Steam 根目录或存档目录）";if(dialog.ShowDialog(this)!=DialogResult.OK)return;if(!GameDiscovery.IsGameFolder(dialog.SelectedPath))throw new Exception("所选目录不是完整游戏安装：需同时包含 ProjectZomboid64.exe 与 ProjectZomboid64.json。请在 Steam 游戏属性 → 已安装文件 → 浏览中找到目录。");path.Text=dialog.SelectedPath;RefreshState();if(!Program.PreviewMode)FetchWorkshop();}}
 }
}
