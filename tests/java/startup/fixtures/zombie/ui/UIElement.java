package zombie.ui;
import java.util.*;
import se.krka.kahlua.vm.KahluaTable;
import zombie.core.textures.Texture;
/** Fixture only. Traversal is the verified UIElement prerender/children/render order. */
public class UIElement {
 public static java.util.function.BiConsumer<UIElement,String> call;
 public static java.util.function.Consumer<Draw> drawing;
 public static final class Draw {
  public final UIElement element;public final Texture texture;public final double x,y,w,h,r,g,b,a;
  Draw(UIElement e,Texture t,double x,double y,double w,double h,double r,double g,double b,double a){element=e;texture=t;this.x=x;this.y=y;this.w=w;this.h=h;this.r=r;this.g=g;this.b=b;this.a=a;}
 }
 final KahluaTable table;public final ArrayList<UIElement> controls=new ArrayList<>();
 public boolean visible=true;public int renders;public Runnable nativeRender;
 public UIElement(KahluaTable table){this.table=table;table.rawset("javaObject",this);}
 public KahluaTable getTable(){return table;}public ArrayList<UIElement> getControls(){return controls;}
 public Boolean isVisible(){return visible;}
 public void render(){if(!visible)return;renders++;if(call!=null)call.accept(this,"prerender");for(UIElement child:new ArrayList<>(controls))child.render();if(call!=null)call.accept(this,"render");if(nativeRender!=null)nativeRender.run();}
 public void DrawTextureScaledColor(Texture t,Double x,Double y,Double w,Double h,Double r,Double g,Double b,Double a){emit(t,x,y,w,h,r,g,b,a);}
 public void DrawTextureColor(Texture t,double x,double y,double r,double g,double b,double a){emit(t,x,y,t.width,t.height,r,g,b,a);}
 public void DrawTextureScaledAspect3(Texture t,double x,double y,double w,double h,double r,double g,double b,double a){emit(t,x,y,w,h,r,g,b,a);}
 void emit(Texture t,double x,double y,double w,double h,double r,double g,double b,double a){if(drawing!=null)drawing.accept(new Draw(this,t,x,y,w,h,r,g,b,a));}
}
