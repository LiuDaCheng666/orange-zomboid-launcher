using System;
using System.Drawing;
using System.Windows.Forms;
namespace PZ3D {
 sealed class GoldButton:Button {
  bool hovered;
  public GoldButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
  protected override void OnPaint(PaintEventArgs e){bool light=BackColor.GetBrightness()>0.65f;Color background=Enabled?BackColor:(light?Color.FromArgb(222,231,243):Color.FromArgb(55,57,62));if(hovered&&Enabled&&ForeColor!=Color.Black)background=light?Color.FromArgb(213,229,247):Color.FromArgb(76,71,56);e.Graphics.Clear(background);using(var p=new Pen(Enabled?FlatAppearance.BorderColor:(light?Color.FromArgb(139,165,194):Color.FromArgb(112,113,117))))e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:(light?Color.FromArgb(80,102,129):Color.FromArgb(193,195,201)),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);if(Focused&&Enabled)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(4,4,Width-8,Height-8),ForeColor,background);}
 }
 sealed class GoldCheckBox:CheckBox {
  public GoldCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Color.FromArgb(41,43,48);ForeColor=Color.FromArgb(242,243,246);UseVisualStyleBackColor=false;}
  public override Size GetPreferredSize(Size proposedSize){var s=TextRenderer.MeasureText(Text,Font);return new Size(s.Width+34,Math.Max(32,s.Height+10));}
  protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
  protected override void OnCheckedChanged(EventArgs e){Invalidate();base.OnCheckedChanged(e);}
  protected override void OnPaint(PaintEventArgs e){bool light=BackColor.GetBrightness()>0.65f;e.Graphics.Clear(BackColor);int top=(Height-18)/2;var box=new Rectangle(3,top,18,18);using(var b=new SolidBrush(Checked?(light?Color.FromArgb(57,126,207):Color.FromArgb(213,169,77)):(light?Color.FromArgb(233,240,250):Color.FromArgb(59,62,68))))e.Graphics.FillRectangle(b,box);using(var p=new Pen(light?Color.FromArgb(91,139,190):Color.FromArgb(190,158,95)))e.Graphics.DrawRectangle(p,box);if(Checked)using(var p=new Pen(light?Color.White:Color.FromArgb(40,42,46),2)){e.Graphics.DrawLines(p,new[]{new Point(7,top+9),new Point(11,top+13),new Point(18,top+5)});}TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(29,0,Math.Max(0,Width-29),Height),Enabled?ForeColor:(light?Color.FromArgb(80,102,129):Color.FromArgb(193,195,201)),TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);if(Focused&&Enabled)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(27,2,Math.Max(1,Width-29),Height-4));}
 }
}
