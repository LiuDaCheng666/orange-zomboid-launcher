using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CheckBox = PZ3D.GoldCheckBox;
namespace PZ3D {
 sealed partial class MainForm {
  CheckBox baseOption,shootOption,chineseOption;
  bool componentBase,componentShoot,componentChinese;
  void SetupComponentPage(TabControl tabs){
   var root=(TableLayoutPanel)Controls[0];var operation=(TableLayoutPanel)buttons.Parent;
   operation.Controls.Remove(models);buttons.Controls.Remove(models);buttons.Controls.Clear();
   var choices=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};
   baseOption=new CheckBox{Text="基础 3D + Buddy",Checked=true,AutoSize=true,Margin=new Padding(5,4,10,4)};
   models.Text="额外 3D 物件模型";models.Margin=new Padding(5,4,10,4);
   shootOption=new CheckBox{Text="Shoot to Thrill 作者枪械模组",Checked=true,AutoSize=true,Margin=new Padding(5,4,10,4)};
   chineseOption=new CheckBox{Text="游戏内中文（老余汉化）",Checked=false,AutoSize=true,Margin=new Padding(5,4,10,4)};
   RememberComponentChoice(shootOption,"shoot-selected.txt");RememberComponentChoice(chineseOption,"chinese-selected.txt");
   choices.Controls.AddRange(new Control[]{baseOption,models,shootOption,chineseOption});
   AddShootUsageControls(choices);
   operation.Controls.Clear();operation.RowStyles.Clear();operation.RowCount=4;
   operation.RowStyles.Add(new RowStyle(SizeType.Absolute,62));operation.RowStyles.Add(new RowStyle(SizeType.Absolute,48));operation.RowStyles.Add(new RowStyle(SizeType.Absolute,30));operation.RowStyles.Add(new RowStyle(SizeType.Absolute,24));
   operation.Controls.Add(choices,0,0);operation.Controls.Add(buttons,0,1);operation.Controls.Add(stage,0,2);operation.Controls.Add(progress,0,3);root.RowStyles[4].Height=175;
   var install=Button("安装",()=>ComponentAction(InstallComponents,"正在安装勾选的组件"));install.BackColor=Color.FromArgb(255,176,0);install.ForeColor=Color.Black;buttons.Controls.Add(install);
   buttons.Controls.Add(Button("卸载",()=>{if(MessageBox.Show(this,"卸载当前勾选的组件。\n勾选基础 3D 时，会同时关闭所有依赖它的组件。只卸载枪械或汉化时，请取消其他勾选。\n保留 Steam 下载文件、光影、个人配置和存档。是否继续？","卸载所选组件",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)==DialogResult.OK)ComponentAction(UninstallComponents,"正在卸载勾选组件的启用配置");}));
   buttons.Controls.Add(Button("检查更新",()=>ComponentAction(UpdateComponents,"正在核对所选组件更新")));
   tabs.TabPages[0].Text="一键安装与文件记录";
   AddText((FlowLayoutPanel)tabs.TabPages[5].Controls[0],LegalContent.OptionalCredits);
   var change=(TableLayoutPanel)tabs.TabPages[0].Controls[0];change.Controls.Remove(status);change.RowStyles[0].Height=142;var header=new Panel{Dock=DockStyle.Fill};status.Dock=DockStyle.Top;status.Height=58;shootState=new Label{Dock=DockStyle.Fill,AutoEllipsis=true};header.Controls.Add(shootState);header.Controls.Add(status);change.Controls.Add(header,0,0);
   var tip=new ToolTip{AutoPopDelay=20000};tip.SetToolTip(baseOption,"安装可选组件时自动补齐基础依赖；卸载基础会连带关闭枪械和汉化。");tip.SetToolTip(shootOption,"安装作者 Shoot to Thrill 多人版，并清理本工具已退役的本地瞄准补丁。完整多人功能需要服务器与客户端模组匹配。");tip.SetToolTip(chineseOption,"游戏语言需简体中文；首次需批准 laoyu.javacn。作者：老余。不会覆盖 Viewpoint JAR。");Theme(operation);Theme(header);install.BackColor=Color.FromArgb(255,176,0);install.ForeColor=Color.Black;UpdateShootState();
  }
  void RememberComponentChoice(CheckBox option,string file){string p=Path.Combine(data,file);bool value;if(File.Exists(p)&&bool.TryParse(File.ReadAllText(p),out value))option.Checked=value;option.CheckedChanged+=(s,e)=>File.WriteAllText(p,option.Checked.ToString());}
  void ComponentAction(Func<string> action,string initial){componentBase=baseOption.Checked;componentShoot=shootOption.Checked;componentChinese=chineseOption.Checked;if(!componentBase&&!models.Checked&&!componentShoot&&!componentChinese)throw new Exception("请先勾选要操作的组件。");Run(action,initial);}
  ulong[] ComponentItems(){var ids=new List<ulong>(InstallWorkflow.Requested(operationModels));if(componentShoot)ids.Add(ShootClient.Item);if(componentChinese)ids.Add(ChinesePatch.Item);return ids.ToArray();}
  string InstallComponents(){var e=GetEngine();e.Guard();string basis=ShootClient.BaseReady(e)&&e.HasView?"基础3D与Buddy已启用，保留现有组件。":InstallSelected3D(false);var work=new Dictionary<string,Func<string>>();if(componentChinese)work.Add("游戏内中文",()=>{string source=InstallWorkflow.Source(e,ChinesePatch.Item);try{ChinesePatch.Validate(source);}catch{source=DownloadSelected(new[]{ChinesePatch.Item})[ChinesePatch.Item];}return new ChinesePatch().Enable(e,source);});if(operationModels)work.Add("额外物件模型",()=>InstallSelected3D(true));if(componentShoot)work.Add("作者枪械模组",EnableShootClient);return basis+"\r\n"+ComponentBatch.Run(work)+"\r\n中文需游戏语言为简体中文。完整多人枪械功能需要服务器与客户端模组匹配；配置完成不是游戏内效果验证。";}
  void RememberPresentationBackup(PresentationBridge bridge){if(bridge.LastBackup!=null){shootBackup=bridge.LastBackup;File.WriteAllText(Path.Combine(data,"shoot-backup-path.txt"),shootBackup);}}
  string UninstallComponents(){var e=GetEngine();e.Guard();var result=new List<string>();try{if(componentBase){result.Add(new ChinesePatch().Disable(e));result.Add(e.Switch(false,false));}else{if(componentShoot){var store=new ShootClient();result.Add(store.Disable(e));RememberShootBackup(store);}if(componentChinese)result.Add(new ChinesePatch().Disable(e));if(operationModels){if(e.Enabled)result.Add(e.Switch(true,false));else result.Add("基础 3D 已关闭，物件包没有加载。");}}}catch(Exception ex){throw new Exception("卸载未全部完成；此前已完成的停用不会自动恢复。请核对下列记录和当前状态。\r\n"+string.Join("\r\n",result)+"\r\n失败原因："+ex.Message,ex);}return string.Join("\r\n",result)+"\r\n卸载完成：撤销启用，保留下载文件、光影、个人设置及存档。";}
  string UpdateComponents(){var e=GetEngine();e.Guard();bool wasBase=e.Enabled,wasShoot=ShootClient.Enabled(e),wasChinese=ChinesePatch.Enabled(e),wasModels=InstallWorkflow.KeepModels(e,false);var work=new Dictionary<string,Func<string>>();if(componentShoot)work.Add("旧本地瞄准补丁清理",()=>{var bridge=new PresentationBridge();try{return bridge.Retire(e);}finally{RememberPresentationBackup(bridge);}});work.Add("基础依赖更新",()=>{DownloadSelected(InstallWorkflow.Requested(false));return wasBase&&downloadChanged?e.Switch(true,wasModels):"基础依赖已核对；启用状态保留。";});if(componentChinese)work.Add("汉化更新",()=>{DownloadSelected(new[]{ChinesePatch.Item});return wasChinese?new ChinesePatch().Enable(e,InstallWorkflow.Source(e,ChinesePatch.Item)):"汉化文件已核对；未自动启用。";});if(operationModels)work.Add("物件模型更新",()=>{DownloadSelected(new[]{3810302175UL});return wasBase&&wasModels&&downloadChanged?e.Switch(true,true):"物件包文件已核对；启用状态保留。";});if(componentShoot)work.Add("作者枪械更新",()=>{DownloadSelected(new[]{ShootClient.Item});return wasShoot?new ShootClient().Enable(e,InstallWorkflow.Source(e,ShootClient.Item)):"枪械文件已核对；未自动启用。";});return ComponentBatch.Run(work)+"\r\n更新不会自动启用原本未启用的组件。";}
 }
}
