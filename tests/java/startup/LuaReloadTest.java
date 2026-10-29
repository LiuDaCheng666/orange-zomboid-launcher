package ozl.startup.tests;

import ozl.startup.*;
import zombie.core.Core;
import zombie.core.SpriteRenderer;
import zombie.core.opengl.RenderThread;
import zombie.gameStates.ConnectToServerState;
import zombie.gameStates.MainScreenState;
import zombie.Lua.LuaManager;
import zombie.Lua.LuaEventManager;
import org.lwjglx.opengl.Display;
import org.lwjgl.glfw.GLFW;
import org.lwjgl.opengl.*;
import java.nio.*;
import java.nio.file.*;
import java.awt.image.BufferedImage;
import javax.imageio.ImageIO;
import java.util.concurrent.atomic.AtomicLong;

/** Actual premain hooks + renderer, with only the game's synchronous reset work
 * replaced. No game entry point, Steam initialization, or visible window. */
public final class LuaReloadTest {
 static int checks;
 static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
 static LoadingModel.Snapshot state(){return StartupRuntime.snapshot();}
 static ConnectToServerState connect(String name){StartupRuntime.serverBegin(name);ConnectToServerState owner=new ConnectToServerState();owner.enter();return owner;}
 static void reset() {try{Core.instance.ResetLua("client","ConnectedToServer");}catch(java.io.IOException e){throw new AssertionError(e);}}
 static int orange(Path path)throws Exception{
  ByteBuffer pixels=ByteBuffer.allocateDirect(1440*900*4);GL11.glReadPixels(0,0,1440,900,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,pixels);
  BufferedImage image=new BufferedImage(1440,900,BufferedImage.TYPE_INT_RGB);int orange=0;
  for(int y=0;y<900;y++)for(int x=0;x<1440;x++){int at=((899-y)*1440+x)*4,r=pixels.get(at)&255,g=pixels.get(at+1)&255,b=pixels.get(at+2)&255;image.setRGB(x,y,(r<<16)|(g<<8)|b);if(x>280&&x<440&&y>275&&y<490&&r>150&&r>g*1.25&&g>b*1.1)orange++;}
  ImageIO.write(image,"png",path.toFile());return orange;
 }
 static int changedOrange(Path a,Path b)throws Exception{BufferedImage first=ImageIO.read(a.toFile()),second=ImageIO.read(b.toFile());int changed=0;for(int y=210;y<490;y++)for(int x=150;x<490;x++){int p=first.getRGB(x,y),q=second.getRGB(x,y);boolean before=((p>>16)&255)>150&&((p>>16)&255)>((p>>8)&255)*1.25&&((p>>8)&255)>(p&255)*1.1,after=((q>>16)&255)>150&&((q>>16)&255)>((q>>8)&255)*1.25&&((q>>8)&255)>(q&255)*1.1;if(before!=after)changed++;}return changed;}
 static void tick() {try{Thread.sleep(45);}catch(InterruptedException e){throw new AssertionError(e);}RenderThread.flushInvokeQueue();}
 public static void main(String[] args)throws Exception{
  check(state()!=null,"premain accepted isolated receipt");
  Path output=Path.of(args[1]);Files.createDirectories(output);
  if(!GLFW.glfwInit())throw new AssertionError("No GLFW context");GLFW.glfwWindowHint(GLFW.GLFW_VISIBLE,GLFW.GLFW_FALSE);
  long window=GLFW.glfwCreateWindow(1440,900,"OZL Lua reload regression",0,0);check(window!=0,"hidden GL context created");
  try{
   GLFW.glfwMakeContextCurrent(window);GL.createCapabilities();Display.created=true;
   AtomicLong clock=new AtomicLong();var renderer=StartupRuntime.class.getDeclaredField("renderer");renderer.setAccessible(true);renderer.set(null,new GameRenderer(clock::get));
   MainScreenState.instance.enter();ConnectToServerState owner=connect("First server");
   MainScreenState.instance.render();var stale=(zombie.core.textures.TextureDraw.GenericDrawer)SpriteRenderer.instance.queue.get(1);long oldEpoch=state().epoch,generation=state().generation;
   Core.instance.duringReset=()->{
    check(state().active&&state().route==LoadingModel.Route.FULL&&!state().world,"server reset animates while CPU sprite production is suspended");
    check(state().epoch!=oldEpoch&&state().generation==generation&&state().name.equals("First server"),"reload invalidates old UI tickets and retains current server identity");
    check(!RenderThread.isWaitForRenderState(),"fixture is inside installed nonwaiting reset interval");
    GL11.glClearColor(.7f,.05f,.03f,1);GL11.glClear(GL11.GL_COLOR_BUFFER_BIT);stale.render(new zombie.core.textures.TextureDraw());
    ByteBuffer pixel=ByteBuffer.allocateDirect(4);GL11.glReadPixels(720,450,1,1,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,pixel);check((pixel.get(0)&255)>150&&(pixel.get(1)&255)<30,"previous Lua UI drawer cannot paint during reset");
    int queued=SpriteRenderer.instance.queue.size(),swaps=Display.swaps,stage=state().stageIndex;tick();clock.set(800_000_000L);tick();int first;
    try{first=orange(output.resolve("reload-first.png"));clock.set(2_050_000_000L);}catch(Exception e){throw new AssertionError(e);}
    LuaManager.RunLua("media/lua/client/Reloaded.lua",false);tick();
    try{check(orange(output.resolve("reload-grown.png"))>200&&first>200&&changedOrange(output.resolve("reload-first.png"),output.resolve("reload-grown.png"))>500,"approved full-size orange visibly rotates on render-thread idle without new Lua/UI frames");}catch(Exception e){throw new AssertionError(e);}
    check(Display.swaps>=swaps+2&&SpriteRenderer.instance.queue.size()==queued,"idle animation swaps only and never manufactures CPU or Lua UI commands");
    check(state().processedScripts==1&&state().generation==generation&&state().stageIndex==stage&&!state().world,"actual Lua work updates activity without inventing stage progress or completing the connection");
   };
   owner.duringUpdate=LuaReloadTest::reset;owner.update();owner.duringUpdate=null;Core.instance.duringReset=null;
   check(state().active&&state().route==LoadingModel.Route.BEHIND_UI&&state().generation==generation,"successful reset returns to queued native connection UI in same operation");
   SpriteRenderer.instance.recycle();int swaps=Display.swaps;tick();check(Display.swaps==swaps,"post-reset idle never paints over native controls");
   StartupRuntime.handoff(false);owner=connect("Second server");long second=state().generation;
   Core.instance.duringReset=()->{check(state().route==LoadingModel.Route.FULL&&state().name.equals("Second server")&&state().processedScripts==0,"second server reload owns fresh state");throw new IllegalStateException("original reset failure");};owner.duringUpdate=LuaReloadTest::reset;
   owner.duringUpdate=()->{boolean threw=false;try{reset();}catch(IllegalStateException e){threw=e.getMessage().equals("original reset failure");check(!state().active&&state().route==LoadingModel.Route.NONE,"reset finally releases presentation before original connection error handler");}check(threw,"original reset exception propagates unchanged to game caller");};owner.update();
   Core.instance.duringReset=null;SpriteRenderer.instance.recycle();
   owner=connect("Replaced operation");Core.instance.duringReset=()->{StartupRuntime.handoff(false);connect("New operation");};owner.duringUpdate=LuaReloadTest::reset;owner.update();Core.instance.duringReset=null;
   check(state().generation!=second&&state().name.equals("New operation")&&state().route==LoadingModel.Route.BEHIND_UI,"old reset completion cannot change a newer connection");
   owner=connect("Native handoff");Core.instance.duringReset=()->LuaEventManager.triggerEvent("OnConnected");owner.duringUpdate=LuaReloadTest::reset;owner.update();Core.instance.duringReset=null;check(!state().active,"reset completion cannot reactivate a character/auth handoff");
   connect("Unowned reset");
   Core.instance.duringReset=()->check(state().route==LoadingModel.Route.BEHIND_UI,"unowned connected reason cannot take over another UI");reset();Core.instance.duringReset=null;
   owner=connect("Options unaffected");Core.instance.duringReset=()->check(state().route==LoadingModel.Route.BEHIND_UI,"unrelated options reset does not take over connection presentation");
   owner.duringUpdate=()->{try{Core.instance.ResetLua(false,"optionsChangedApplied");}catch(java.io.IOException e){throw new AssertionError(e);}};owner.update();Core.instance.duringReset=null;
   StartupRuntime.handoff(false);Core.instance.duringReset=()->check(!state().active,"menu reset does not reactivate a stale connection");reset();Core.instance.duringReset=null;
   check(GL11.glGetError()==GL11.GL_NO_ERROR,"reload renderer preserves valid GL state");System.out.println("PASS "+checks+" scoped Lua reload lifecycle and real idle animation checks");
  }finally{Core.instance.duringReset=null;LuaEventManager.original=null;SpriteRenderer.instance.recycle();Display.created=false;GLFW.glfwDestroyWindow(window);GLFW.glfwTerminate();}
 }
}
