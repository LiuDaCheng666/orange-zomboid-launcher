package ozl.startup;
import java.lang.reflect.*;
/** Immediate scoped presentation of original row primitives; callbacks and row heights stay native. */
public final class NativeWorkshopRows {
 private static double width(Object element)throws Exception{Object table=element.getClass().getMethod("getTable").invoke(element);return ((Number)table.getClass().getMethod("rawget",Object.class).invoke(table,"width")).doubleValue();}
 private static double right(Object element)throws Exception{double width=width(element);Object table=element.getClass().getMethod("getTable").invoke(element);java.lang.reflect.Method raw=table.getClass().getMethod("rawget",Object.class);double height=((Number)raw.invoke(table,"height")).doubleValue(),scrollHeight=((Number)element.getClass().getMethod("getScrollHeight").invoke(element)).doubleValue();Object scroll=raw.invoke(table,"vscroll");double reserve=scrollHeight>height?((Number)scroll.getClass().getMethod("rawget",Object.class).invoke(scroll,"width")).doubleValue():0;return width-8-reserve;}
 private static int measure(Object element,Object font,String text)throws Exception{ClassLoader loader=element.getClass().getClassLoader();Class<?> manager=Class.forName("zombie.ui.TextManager",false,loader),fontType=Class.forName("zombie.ui.UIFont",false,loader);return (Integer)manager.getMethod("MeasureStringX",fontType,String.class).invoke(manager.getField("instance").get(null),font,text);}
 private static String fit(Object element,Object font,String text,double maximum)throws Exception{if(measure(element,font,text)<=maximum)return text;int end=text.length();while(end>0&&measure(element,font,text.substring(0,end)+"…")>maximum)end--;return end==0?"":text.substring(0,end)+"…";}
 public static Object[] text(Object element,Object font,String text,double x,double y,double r,double g,double b,double a){try{if(!StartupRuntime.workshopList(element)||!(font instanceof Enum<?> name))return null;double width=width(element),right=right(element),reserve=Math.min(220,width*.34);
  if(name.name().equals("Large")&&x==16)return new Object[]{fit(element,font.getClass().getField("Medium").get(null),text,right-reserve-40),x,font.getClass().getField("Medium").get(null)};
  if(name.name().equals("Medium")&&x==16&&r==.8&&g==.8&&b==.8&&a==1)return new Object[]{fit(element,font.getClass().getField("Small").get(null),text,right-reserve-40),x,font.getClass().getField("Small").get(null)};
  if(name.name().equals("Medium")&&r==1&&g==1&&b==1&&a==1){double box=Math.min(reserve,measure(element,font,text)+8);Object small=font.getClass().getField("Small").get(null);String fitted=fit(element,small,text,box-8);return new Object[]{fitted,right-box+(box-measure(element,small,fitted))/2,small};}
 }catch(Throwable unavailable){StartupRuntime.rejectWorkshopRows();}return null;}
 public static double[] rect(Object element,Object texture,double x,double y,double w,double h,double r,double g,double b,double a){try{if(texture!=null||!StartupRuntime.workshopList(element))return null;
  // Exact two fills emitted by the original drawProgressBar; selection and borders differ.
  if(h>4&&h<=24&&((r==1&&g==1&&b==1&&a==.5)||(r==.15&&g==.15&&b==.15&&a==1)))return new double[0];
  if(r==.7&&g==.35&&b==.15&&a==.3)return new double[]{x,y,w,h,.42,.31,.62,.32};
  if(h>4&&h<=40&&a==1&&r==0&&((g==0&&b==.6)||(g==.6&&b==0))){double width=width(element),box=Math.min(w,Math.min(220,width*.34));return new double[]{x+w-box,y,box,h,g==.6?.12:.24,g==.6?.32:.19,g==.6?.25:.39,1};}
 }catch(Throwable unavailable){StartupRuntime.rejectWorkshopRows();}return null;}
}
