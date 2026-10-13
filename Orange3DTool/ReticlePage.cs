using System;
using System.Drawing;
using System.Windows.Forms;
namespace PZ3D {
 sealed partial class MainForm {
  internal const string ReticleHelp="原版瞄准与扩展开镜切换\r\n\r\n主页面默认选择 Viewpoint 原版：安装后保留原来的右键瞄准，不默认让射击扩展接管。默认没有切换快捷键。\r\n点击“切换快捷键”输入框，按想用的组合键录入（如 CTRL+F8，仅示例，不保证不冲突）；退格清空。完全退出游戏，勾选枪械组件并点安装，更新已安装补丁和参数。无需卸载整个3D。\r\n进入服务器按 O 进入第一人称。按自选快捷键，在原版瞄准和扩展开镜之间切换；同一个键可切回。游戏内显示3秒模式提示，按住不重复切换，输入聊天文字时不触发。\r\n原版模式关闭射击扩展的瞄准接管并重置其瞄准状态，不修改服务器伤害。扩展模式仍属实验，不保证所有枪械/瞄具正确；异常时切回原版。模组接口校验通过不是游戏画面已验证。快捷键只用于这个本地补丁，不等于作者的 Ctrl+V 腰射切换键。";
  ComboBox aimDefault;TextBox aimShortcut;
  void AddReticleControls(FlowLayoutPanel choices){var saved=ReticleSettings.Load(data);var row=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Margin=new Padding(5,1,0,1)};row.Controls.Add(new Label{Text="默认瞄准",AutoSize=true,Margin=new Padding(0,5,5,0)});aimDefault=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=150};aimDefault.Items.AddRange(new object[]{"Viewpoint 原版","射击扩展（实验）"});aimDefault.SelectedIndex=saved.StartNative?0:1;row.Controls.Add(aimDefault);row.Controls.Add(new Label{Text="切换快捷键",AutoSize=true,Margin=new Padding(8,5,5,0)});aimShortcut=new TextBox{Width=130,Text=saved.KeyText??"",AccessibleDescription="点击后按字母、数字或功能键组合录入；退格清空，默认不绑定。"};aimShortcut.KeyDown+=(s,e)=>{e.SuppressKeyPress=true;if(e.KeyCode==Keys.Back||e.KeyCode==Keys.Delete){aimShortcut.Text="";return;}if(e.KeyCode==Keys.ControlKey||e.KeyCode==Keys.ShiftKey||e.KeyCode==Keys.Menu)return;string key=e.KeyCode.ToString().ToUpperInvariant();if(key.Length==2&&key[0]=='D'&&char.IsDigit(key[1]))key=key.Substring(1);aimShortcut.Text=(e.Control?"CTRL+":"")+(e.Shift?"SHIFT+":"")+(e.Alt?"ALT+":"")+key;};row.Controls.Add(aimShortcut);row.Controls.Add(new Label{Text="空白=未绑定；改后退出游戏并点安装",AutoSize=true,Margin=new Padding(8,5,0,0)});choices.Controls.Add(row);var tip=new ToolTip{AutoPopDelay=20000};tip.SetToolTip(aimShortcut,aimShortcut.AccessibleDescription);}
  void SaveReticleChoice(){new ReticleSettings{StartNative=aimDefault.SelectedIndex==0,KeyText=aimShortcut.Text}.Save(data);}
 }
}
