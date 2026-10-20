using System;
using System.Drawing;
using System.Windows.Forms;
namespace PZ3D {
 sealed partial class MainForm {
  internal const string ReticleHelp="枪械使用说明\r\n\r\n使用作者原生操作：默认 O 开关第一人称，右键瞄准，Ctrl+V 切换腰射 / 精瞄；实际按键以游戏内绑定为准。Viewpoint 其他视角快捷键请在游戏设置中查看。\r\n联机使用时，服务器需启用 Shoot to Thrill，客户端安装枪械组件并完成 Java 授权。\r\n本工具的旧本地瞄准补丁已退役。主动安装或更新枪械组件时会清理旧补丁及其私有设置，保留作者枪械、Viewpoint、汉化和个人校准。已加载到游戏的旧 Java 需你下次完全重启游戏后退出。";
  void AddShootUsageControls(FlowLayoutPanel choices){choices.Controls.Add(new Label{Text="枪械操作：右键瞄准；Ctrl+V 切换腰射 / 精瞄（以游戏内绑定为准）。",AutoSize=true,Margin=new Padding(5,4,0,1)});}
 }
}
