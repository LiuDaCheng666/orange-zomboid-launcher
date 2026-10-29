package ozl.startup;
import java.awt.*;
import java.awt.image.BufferedImage;

/** Real font metrics: long lines must fit without hundreds of measurements per frame. */
public final class LoadingTextCostTest {
 static int checks;
 static void check(boolean value,String why){if(!value)throw new AssertionError(why);checks++;}
 static final class Canvas implements LoadingScene.Canvas {
  final Graphics2D graphics=new BufferedImage(1,1,BufferedImage.TYPE_INT_ARGB).createGraphics();
  int measurements;
  public double measure(String text,double size){measurements++;return graphics.getFontMetrics(new Font("Microsoft YaHei",Font.PLAIN,16).deriveFont((float)size)).stringWidth(text);}
  public void text(String v,double x,double y,double s,int c,boolean r){}
  public void gradient(double x,double y,double w,double h,int t,int b){}
  public void glow(double x,double y,double r,int c,int e){}
  public void arc(double x,double y,double r,double t,double s,double l,int c){}
  public void line(double x,double y,double xx,double yy,double t,int c){}
  public void logo(double x,double y,double s,double a,double angle){}
 }
 static boolean validUnicode(String text){for(int i=0;i<text.length();i++){char c=text.charAt(i);if(Character.isHighSurrogate(c)){if(++i>=text.length()||!Character.isLowSurrogate(text.charAt(i)))return false;}else if(Character.isLowSurrogate(c))return false;}return true;}
 public static void main(String[] args){
  boolean baseline=args.length>0&&args[0].equals("baseline");Canvas canvas=new Canvas();
  String longText="正在加载模组资源/".repeat(60);longText=longText.substring(0,512);
  String fitted=LoadingScene.fit(canvas,longText,28,360);int calls=canvas.measurements;
  System.out.println("METRIC longLineMeasurements="+calls);
  check(canvas.measure(fitted,28)<=360,"long line fits the actual font width");
  check(fitted.endsWith("…")&&longText.startsWith(fitted.substring(0,fitted.length()-1)),"truncation retains original prefix and ellipsis");
  if(!baseline)check(calls<=16,"512-character clipping stays within 16 font measurements rather than scanning hundreds of suffixes");
  check(LoadingScene.fit(canvas,"短文本",20,500).equals("短文本"),"already fitting text remains untouched");
  check(LoadingScene.fit(canvas,"abc",20,0).isEmpty(),"zero width emits no text");
  check(LoadingScene.fit(canvas,"abc",20,1).isEmpty(),"less than an ellipsis width emits no text");
  for(String value:new String[]{"🦝".repeat(40),"A🦝B橙子".repeat(20),"abcdefghijklmnopqrstuvwxyz","模组配置资源日志".repeat(20)}){
   for(int width=15;width<300;width+=7){String got=LoadingScene.fit(canvas,value,22,width);check(validUnicode(got),"clipping keeps Unicode scalar boundaries");check(canvas.measure(got,22)<=width,"clipped output stays within actual width");}
  }
  if(!baseline)check(validUnicode(LoadingScene.fit(canvas,"a".repeat(510)+"🦝x",20,100000)),"source-length clipping never leaves a lone Unicode surrogate before ellipsis");
  canvas.graphics.dispose();System.out.println("PASS "+checks+" text fitting and cost contracts");
 }
}
