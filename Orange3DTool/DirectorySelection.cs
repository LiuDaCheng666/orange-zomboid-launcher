using System;
using System.Windows.Forms;
namespace PZ3D {
 sealed partial class MainForm {
  void ChooseGameDirectory(){if(busy)throw new Exception("请等待当前操作完成再切换目录。");using(var dialog=new FolderBrowserDialog()){dialog.Description="请选择 Project Zomboid 游戏文件夹或 Steam 游戏库";if(dialog.ShowDialog(this)!=DialogResult.OK)return;string selected=GameDiscovery.ResolveSelection(dialog.SelectedPath);if(selected.Length==0)throw new Exception("未找到游戏，请选择 Project Zomboid 游戏文件夹或 Steam 游戏库。");path.Text=selected;RefreshState();if(!Program.PreviewMode)FetchWorkshop();}}
 }
}
