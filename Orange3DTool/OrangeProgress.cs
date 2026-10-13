using System;
using System.Drawing;
using System.Windows.Forms;
namespace PZ3D {
 sealed class OrangeProgress:ProgressBar {
  bool indeterminate;int phase;readonly Timer animation=new Timer{Interval=40};
  public bool Indeterminate{get{return indeterminate;}set{indeterminate=value;if(value)animation.Start();else animation.Stop();Invalidate();}}
  public OrangeProgress(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);animation.Tick+=(s,e)=>{phase=(phase+8)%Math.Max(1,ClientSize.Width+160);Invalidate();};}
  public new int Value{get{return base.Value;}set{base.Value=value;Invalidate();}}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);float fraction=Maximum==Minimum?0:(float)(Value-Minimum)/(Maximum-Minimum);using(var brush=new SolidBrush(ForeColor))e.Graphics.FillRectangle(brush,indeterminate?phase-160:0,0,indeterminate?160:(int)(ClientSize.Width*fraction),ClientSize.Height);using(var pen=new Pen(ForeColor))e.Graphics.DrawRectangle(pen,0,0,Math.Max(0,ClientSize.Width-1),Math.Max(0,ClientSize.Height-1));}
  protected override void Dispose(bool disposing){if(disposing)animation.Dispose();base.Dispose(disposing);}
 }
}
