package ozl.startup.tests;

import ozl.startup.*;
import zombie.gameStates.*;
import zombie.network.GameClient;
import zombie.Lua.LuaEventManager;
import zombie.ui.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import java.nio.ByteBuffer;
import org.lwjgl.glfw.GLFW;
import org.lwjgl.opengl.*;
import org.lwjglx.opengl.Display;

/** Actual premain hooks plus the installed menu Lua; no game entry point or Steam. */
public final class StartupTransitionTest {
 static int checks;
 static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
 static String b64(Path p){return Base64.getEncoder().encodeToString(p.toString().getBytes(StandardCharsets.UTF_8));}
 static Path request;static Session session;static String valid;
 static void prepare(NativeLuaFixture f,String value)throws Exception{
  StartupRuntime.begin(session);Files.writeString(request,value);
  f.run("main.delay=500;main.firstFrame=true;main.bottomPanel:setVisible(true);connection:setVisible(false);connection.failMessage=nil;connection.googleAuthPopup:setVisible(false)");
 }
 static LoadingModel.Snapshot state(){return StartupRuntime.snapshot();}
 static int[] pixel(){ByteBuffer p=ByteBuffer.allocateDirect(4);GL11.glReadPixels(8,712,1,1,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,p);return new int[]{p.get(0)&255,p.get(1)&255,p.get(2)&255};}
 static void sentinel(){GL11.glClearColor(.7f,.02f,.7f,1);GL11.glClear(GL11.GL_COLOR_BUFFER_BIT);}
 static void verifyFinalBootFrame(zombie.core.textures.TextureDraw.GenericDrawer drawer){
  if(!GLFW.glfwInit())throw new AssertionError("No GLFW context");GLFW.glfwWindowHint(GLFW.GLFW_VISIBLE,GLFW.GLFW_FALSE);long window=GLFW.glfwCreateWindow(1280,720,"OZL hidden final boot frame",0,0);check(window!=0,"hidden final-frame context created");
  try{
   GLFW.glfwMakeContextCurrent(window);GL.createCapabilities();Display.created=true;Display.width=1280;Display.height=720;
   sentinel();drawer.render();int[] p=pixel();check(p[0]<100&&p[1]>15&&p[2]<100,"already-produced safe menu frame stays covered after native post-render connect begins");
   new GameLoadingState().enter();sentinel();drawer.render();p=pixel();check(p[0]>150&&p[1]<30&&p[2]>150,"same-server world entry invalidates a retained boot frame");
   StartupRuntime.handoff(false);sentinel();drawer.render();p=pixel();check(p[0]>150&&p[1]<30&&p[2]>150,"native failure or required-input handoff immediately invalidates the final boot cover");
   StartupRuntime.serverBegin("Replacement operation");sentinel();drawer.render();p=pixel();check(p[0]>150&&p[1]<30&&p[2]>150,"old final boot frame cannot paint a replacement connection");
   check(GL11.glGetError()==GL11.GL_NO_ERROR,"final boot frame preserves GL state");
  }finally{Display.created=false;GLFW.glfwDestroyWindow(window);GLFW.glfwTerminate();}
 }
 public static void main(String[] args)throws Exception{
  Path game=Path.of(System.getProperty("user.dir")),folder=game.resolve("OrangeLauncherRuntime/startup"),user=Files.createDirectories(game.resolve("transition-user"));
  long now=System.currentTimeMillis()/1000;String nonce="1234567890abcdef1234567890abcdef";
  Files.writeString(folder.resolve("session.properties"),"version=OZL1\nsession="+nonce+"\ncreated="+now+"\nexpires="+(now+600)+"\ngame="+b64(game)+"\nuser="+b64(user)+"\nissues="+b64(folder.resolve("issues.txt"))+"\ntheme=dark\nreducedMotion=false\n");
  session=Session.read(folder.resolve("OrangeStartup.jar"),game,now);check(session!=null,"isolated receipt accepted");
  request=Files.createDirectories(user.resolve("Lua")).resolve("OrangeLauncherSession.request.txt");
  valid="ORANGE1\n"+nonce+"\n"+now+"\nserver\n\n\n127.0.0.1\n16261\nfixture-user\nfixture-password\nfixture-server-password\n0\n";
  NativeLuaFixture f=new NativeLuaFixture(Path.of(args[0]),1280,720);
  prepare(f,valid);MainScreenState.instance.enter();
  check(state().active&&state().context==LoadingModel.Context.BOOT,"direct server receipt retains boot through native 500ms menu delay");
  check(((Number)f.main.getTable().rawget("delay")).intValue()==500,"handoff does not shorten native menu delay");
  long epoch=state().epoch;MainScreenState.instance.render();
  check(state().active&&state().epoch==epoch,"pending render keeps the same boot scene and animation");
  f.run("main.delay=0");MainScreenState.instance.render();
  Object finalBootFrame=zombie.core.SpriteRenderer.instance.queue.get(zombie.core.SpriteRenderer.instance.queue.size()-1);
  check(finalBootFrame instanceof zombie.core.textures.TextureDraw.GenericDrawer,"final boot frame owns a queued cover before the post-render bridge changes model state");
  LuaEventManager.original=(event,values)->{if(event.equals("OnRenderTick"))GameClient.instance.doConnect("u","p","127.0.0.1","","16261","sp","Current server",false,1,"");};LuaEventManager.triggerEvent("OnRenderTick");LuaEventManager.original=null;
  check(state().active&&state().context==LoadingModel.Context.SERVER&&state().name.equals("Current server"),"real native connect takes over without inactive frame");
  check(!state().world&&state().stageIndex==0,"handoff invents no download or world progress");
  verifyFinalBootFrame((zombie.core.textures.TextureDraw.GenericDrawer)finalBootFrame);
  StartupRuntime.handoff(false);MainScreenState.instance.enter();check(!state().active,"later menu does not replay launch-only handoff");
  for(String invalid:new String[]{valid.replace(nonce,"abcdef1234567890abcdef1234567890"),valid.replace("\nserver\n","\nnormal\n"),valid.replace("\n"+now+"\n","\n"+(now-601)+"\n"),valid.replace("\n16261\n","\n99999\n"),""}){
   prepare(f,invalid);MainScreenState.instance.enter();check(!state().active,"invalid, other-session, normal or consumed request leaves native menu visible");
  }
  prepare(f,valid);f.run("modal=ISPanel:new(0,0,400,300);modal:instantiate();main.termsOfServiceDialog=modal");UIManager.UI.add((UIElement)((se.krka.kahlua.vm.KahluaTable)f.env.rawget("modal")).rawget("javaObject"));MainScreenState.instance.enter();check(!state().active,"terms and other root dialogs veto launch handoff");UIManager.UI.remove(1);f.run("main.termsOfServiceDialog=nil");
  prepare(f,valid);MainScreenState.instance.enter();f.run("main.delay=0");MainScreenState.instance.render();LuaEventManager.triggerEvent("OnRenderTick");check(!state().active,"missing bridge yields after first eligible native render tick, without a fabricated hold timer");
  prepare(f,valid);MainScreenState.instance.enter();f.run("main.delay=0");MainScreenState.instance.render();MainScreenState.instance.render();check(!state().active,"a disabled native render-tick event cannot leave the boot cover stuck");
  prepare(f,valid);MainScreenState.instance.enter();Files.writeString(request,"");MainScreenState.instance.render();check(!state().active,"bridge failure or consumption cannot retain an orphan boot cover");
  prepare(f,valid);MainScreenState.instance.enter();f.run("popup=ISPanel:new(0,0,300,240);popup:instantiate();main:addChild(popup)");MainScreenState.instance.render();check(!state().active,"a newly visible unknown native control releases the boot cover in that frame");f.main.controls.remove(f.main.controls.size()-1);f.run("main.children[#main.children]=nil");
  prepare(f,valid);MainScreenState.instance.enter();f.run("main.delay=0");UIManager.luaRender=()->{FilesWrite.clear(request);StartupRuntime.serverBegin("Failed direct connect");LuaEventManager.triggerEvent("OnConnectFailed","native failure");f.main.render();};MainScreenState.instance.render();UIManager.luaRender=f.main::render;check(!state().active,"native connection failure during handoff is never covered");
  prepare(f,valid);MainScreenState.instance.enter();MainScreenState.instance.failRender=true;boolean thrown=false;try{MainScreenState.instance.render();}catch(IllegalStateException expected){thrown=true;}finally{MainScreenState.instance.failRender=false;}
  check(thrown&&!state().active,"original render failure propagates and releases pending boot presentation");
  StartupRuntime.serverBegin("Native auth");LuaEventManager.triggerEvent("OnGoogleAuthRequest");check(!state().active,"authentication still yields immediately");
  zombie.core.SpriteRenderer.instance.recycle();System.out.println("PASS "+checks+" receipt-bound startup transition checks");
 }
 static final class FilesWrite {static void clear(Path path){try{Files.writeString(path,"");}catch(java.io.IOException e){throw new AssertionError(e);}}}
}
