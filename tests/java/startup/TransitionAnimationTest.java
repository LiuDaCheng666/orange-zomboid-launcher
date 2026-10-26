package ozl.startup.tests;

import ozl.startup.*;
import zombie.gameStates.*;
import zombie.core.Core;
import zombie.Lua.LuaEventManager;
import org.lwjgl.glfw.GLFW;
import org.lwjgl.opengl.*;
import org.lwjglx.opengl.Display;
import java.nio.*;
import java.nio.file.*;

/** Same accepted native layout before/during/after a real instrumented Lua reset. */
public final class TransitionAnimationTest {
 static int checks;
 static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
 record Mark(int count,double x,double y){}
 static Mark mark(){
  ByteBuffer p=ByteBuffer.allocateDirect(1280*720*4);GL11.glReadPixels(0,0,1280,720,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,p);long sx=0,sy=0;int n=0;
  for(int y=180;y<560;y++)for(int x=80;x<1180;x++){int i=((719-y)*1280+x)*4,r=p.get(i)&255,g=p.get(i+1)&255,b=p.get(i+2)&255;if(r>150&&r>g*1.25&&g>b*1.1){n++;sx+=x;sy+=y;}}
  return new Mark(n,n==0?0:sx/(double)n,n==0?0:sy/(double)n);
 }
 static void clear(){GL11.glClearColor(.3f,.02f,.1f,1);GL11.glClear(GL11.GL_COLOR_BUFFER_BIT);}
 public static void main(String[] args)throws Exception{
  NativeLuaFixture f=new NativeLuaFixture(Path.of(args[0]),1280,720);StartupRuntime.serverBegin("Continuous server");ConnectToServerState owner=new ConnectToServerState();owner.enter();
  NativeConnectionPresentation.Lease layout=new NativeConnectionPresentation().statusPage(f.main.getClass().getClassLoader(),StartupRuntime.snapshot());check(layout!=null,"actual installed native UI supplies accepted bounds");
  if(!GLFW.glfwInit())throw new AssertionError("No GLFW context");GLFW.glfwWindowHint(GLFW.GLFW_VISIBLE,GLFW.GLFW_FALSE);long window=GLFW.glfwCreateWindow(1280,720,"OZL hidden transition check",0,0);check(window!=0,"hidden GL context created");
  try{
   GLFW.glfwMakeContextCurrent(window);GL.createCapabilities();Display.created=true;Display.width=1280;Display.height=720;
   long[] now={1_000_000_000L};GameRenderer renderer=new GameRenderer(()->now[0]);var rendererField=StartupRuntime.class.getDeclaredField("renderer");rendererField.setAccessible(true);rendererField.set(null,renderer);
   Session receipt=Session.read(Path.of("OrangeLauncherRuntime/startup/OrangeStartup.jar"),Path.of(System.getProperty("user.dir")),System.currentTimeMillis()/1000);check(receipt!=null,"current isolated draw receipt accepted");
   renderer.draw(StartupRuntime.snapshot(),receipt,0,0,layout.statusBottom,layout.cancelTop);now[0]+=2_000_000_000L;
   clear();renderer.draw(StartupRuntime.snapshot(),receipt,0,0,layout.statusBottom,layout.cancelTop);Mark before=mark();check(before.count>500,"native connection logo reached its mature visible size");
   long generation=StartupRuntime.generation(),epoch=StartupRuntime.snapshot().epoch;
   Core.instance.duringReset=()->{
    check(StartupRuntime.snapshot().route==LoadingModel.Route.FULL&&StartupRuntime.snapshot().epoch!=epoch,"reset invalidates UI tickets while retaining current operation");
    clear();StartupRuntime.beforeSwap();Mark during=mark();check(during.count>before.count*.85&&Math.abs(during.x-before.x)<3&&Math.abs(during.y-before.y)<3,"Lua reset preserves mature logo position and animation instead of jumping left and restarting");
   };
   owner.duringUpdate=()->{try{Core.instance.ResetLua("client","ConnectedToServer");}catch(java.io.IOException e){throw new AssertionError(e);}};owner.update();owner.duringUpdate=null;Core.instance.duringReset=null;
   clear();renderer.draw(StartupRuntime.snapshot(),receipt,0,0,layout.statusBottom,layout.cancelTop);Mark after=mark();check(after.count>before.count*.85&&Math.abs(after.x-before.x)<3&&Math.abs(after.y-before.y)<3,"native UI return preserves the same animation origin");
   check(StartupRuntime.generation()==generation&&!StartupRuntime.snapshot().world,"visual continuity does not manufacture a new session or world progress");
   StartupRuntime.serverBegin("Replacement server");clear();renderer.draw(StartupRuntime.snapshot(),receipt,0,0,layout.statusBottom,layout.cancelTop);check(mark().count<before.count*.2,"a genuinely new connection still gets its own entrance");
   LuaEventManager.triggerEvent("OnConnected");check(!StartupRuntime.snapshot().active,"character/auth handoff remains native");check(GL11.glGetError()==GL11.GL_NO_ERROR,"handoff renderer preserves GL state");
   System.out.println("PASS "+checks+" actual-GL reload continuity checks");
  }finally{Core.instance.duringReset=null;zombie.core.SpriteRenderer.instance.recycle();Display.created=false;GLFW.glfwDestroyWindow(window);GLFW.glfwTerminate();}
 }
}
