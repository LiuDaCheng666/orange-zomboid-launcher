package zombie.ui;
import zombie.core.SpriteRenderer;
public final class UIManager {
 public static se.krka.kahlua.vm.KahluaThread thread;
 public static se.krka.kahlua.vm.KahluaThread getDefaultThread(){return thread;}
 public static boolean useUiFbo=true;
 public static Runnable luaRender;
 public static final java.util.ArrayList<UIElement> UI=new java.util.ArrayList<>();
 public static java.util.ArrayList<UIElement> getUI(){return UI;}
 public static Object tableget(se.krka.kahlua.vm.KahluaTable table,Object key){
  Object value=table.rawget(key);if(value!=null)return value;
  se.krka.kahlua.vm.KahluaTable meta=table.getMetatable();
  if(meta!=null&&meta.rawget("__index") instanceof se.krka.kahlua.vm.KahluaTable parent)return tableget(parent,key);
  return null;
 }
 public static void render(){if(luaRender!=null)luaRender.run();else SpriteRenderer.instance.queue.add("controls");}
}
