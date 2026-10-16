using System;
using System.Drawing;
using System.Windows.Forms;
namespace PZ3D {
 sealed partial class MainForm {
  internal const string ReticleHelp="枪械使用说明\r\n\r\n使用作者原生操作：默认 O 开关第一人称，右键瞄准，Ctrl+V 切换腰射 / 精瞄；实际按键以游戏内绑定为准。Viewpoint 其他视角快捷键请在游戏设置中查看。\r\n完整多人射击与开镜功能需要服务器与客户端使用相互匹配的作者模组；仅在客户端安装不能保证完整战斗、弹道或开镜效果。请按服务器规则核对模组与 Java 授权。\r\n本工具的旧本地瞄准补丁已退役。主动安装或更新枪械组件时会清理旧补丁及其私有设置，保留作者枪械、Viewpoint、汉化和个人校准。已加载到游戏的旧 Java 需你下次完全重启游戏后退出。配置完成不代表本次进服已生效。";
  void AddShootUsageControls(FlowLayoutPanel choices){choices.Controls.Add(new Label{Text="枪械操作：右键瞄准；Ctrl+V 切换腰射 / 精瞄（以游戏内绑定为准）。",AutoSize=true,Margin=new Padding(5,4,0,1)});}
 }
}
