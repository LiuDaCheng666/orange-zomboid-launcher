package ozl.startup.tests;
import ozl.startup.StartupRuntime;
/** Signature verification only: none of these installed classes are initialized. */
public final class LuaReloadApiProbe {
 public static void main(String[] args)throws Exception{
  ClassLoader loader=ClassLoader.getSystemClassLoader();
  Class<?> core=Class.forName("zombie.core.Core",false,loader),render=Class.forName("zombie.core.opengl.RenderThread",false,loader);
  if(core.getDeclaredMethod("ResetLua",String.class,String.class).getReturnType()!=void.class||core.getDeclaredMethod("ResetLua",boolean.class,String.class).getReturnType()!=void.class||render.getDeclaredMethod("flushInvokeQueue").getReturnType()!=void.class)throw new AssertionError("Changed installed Lua reload boundary");
  if(StartupRuntime.snapshot()==null||!StartupRuntime.snapshot().active||StartupRuntime.snapshot().epoch!=1)throw new AssertionError("API verification initialized game behavior or failed transformation");
  System.out.println("PASS installed Core.ResetLua and render-thread hook signatures without initialization");
 }
}
