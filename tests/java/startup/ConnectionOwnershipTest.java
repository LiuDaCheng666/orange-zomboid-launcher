package ozl.startup.tests;

import java.nio.*;
import java.nio.file.*;
import java.util.*;
import ozl.startup.*;
import zombie.Lua.*;
import zombie.core.*;
import zombie.gameStates.*;
import zombie.ui.*;
import org.lwjgl.glfw.GLFW;
import org.lwjgl.opengl.*;
import org.lwjglx.opengl.Display;

/** Production premain/renderer + installed native Lua; no engine, network or visible window. */
public final class ConnectionOwnershipTest {
 static int checks;static final List<String> failures=new ArrayList<>();
 static void check(boolean ok,String why){checks++;System.out.println((ok?"PASS ":"FAIL ")+why);if(!ok)failures.add(why);}
 static LoadingModel.Snapshot state(){return StartupRuntime.snapshot();}
 static final SpriteRenderer sprites=SpriteRenderer.instance;
 static final long[] clock={0};
 static long drawers(){return sprites.queue.stream().filter(x->x instanceof zombie.core.textures.TextureDraw.GenericDrawer).count();}
 static zombie.core.textures.TextureDraw.GenericDrawer drawer(){return (zombie.core.textures.TextureDraw.GenericDrawer)sprites.queue.stream().filter(x->x instanceof zombie.core.textures.TextureDraw.GenericDrawer).findFirst().orElseThrow();}
 static void sentinel(){GL11.glClearColor(.7f,.02f,.7f,1);GL11.glClear(GL11.GL_COLOR_BUFFER_BIT);}
 static boolean untouched(){ByteBuffer p=ByteBuffer.allocateDirect(4);GL11.glReadPixels(8,712,1,1,GL11.GL_RGBA,GL11.GL_UNSIGNED_BYTE,p);return (p.get(0)&255)>150&&(p.get(1)&255)<30&&(p.get(2)&255)>150;}
 static double n(UIElement e,String key){return ((Number)e.getTable().rawget(key)).doubleValue();}
 static void nativeDrawing(NativeLuaFixture f){
  GameRenderer text=new GameRenderer();
  sprites.background=()->{GL11.glClearColor(.08f,.11f,.14f,1);GL11.glClear(GL11.GL_COLOR_BUFFER_BIT);};
  UIElement.drawing=d->{f.draws.add(d);sprites.queue.add((Runnable)()->{
   double x=d.element==f.connect?n(f.connect,"x"):0,y=d.element==f.connect?n(f.connect,"y"):0;
   if(d.texture==f.main.getTable().rawget("logoTexture")){text.text("PROJECT",d.x,d.y+30,32,0xffe4e4e4,false);text.text("ZOMBOID",d.x,d.y+70,48,0xffe4e4e4,false);}
   else NativeLuaGlProbe.rect(x+d.x,y+d.y,d.w,d.h,d.r,d.g,d.b,d.a);
  });};
  for(String name:new String[]{"title","serverName1","serverName","userName1","userName","richText","connectLabel","backBtn"}){
   UIElement child=f.child(name);child.nativeRender=()->sprites.queue.add((Runnable)()->{
    double x=n(f.connect,"x")+n(child,"x"),y=n(f.connect,"y")+n(child,"y"),h=n(child,"height");
    String label=name.equals("connectLabel")?"Getting server information...":name.equals("backBtn")?"Abort":name.equals("title")?"Connect to server":name.equals("richText")?"Native connection reminder":"Fixture identity";
    if(name.equals("backBtn"))NativeLuaGlProbe.rect(x,y,100,h,.1,.1,.1,.9);
    text.text(label,x-text.measure(label,16)/2,y+Math.min(h,20),16,0xffffffff,false);
   });
  }
 }
 static void frame(Path output,String name)throws Exception{MainScreenState.instance.render();sprites.drain();if(drawers()>0){clock[0]+=2_000_000_000L;sprites.drain();}NativeLuaGlProbe.capture(1280,720,output.resolve(name+".png"));}
 public static void main(String[] args)throws Exception{
  Path output=Path.of(args[1]);Files.createDirectories(output);check(state()!=null,"premain accepted isolated receipt");
  check(GLFW.glfwInit(),"GLFW initialized in background");GLFW.glfwWindowHint(GLFW.GLFW_VISIBLE,GLFW.GLFW_FALSE);long window=GLFW.glfwCreateWindow(1280,720,"OZL ownership hidden regression",0,0);check(window!=0,"hidden context created");
  try{
   GLFW.glfwMakeContextCurrent(window);GL.createCapabilities();Display.created=true;Display.width=1280;Display.height=720;GL11.glViewport(0,0,1280,720);
   GL11.glMatrixMode(GL11.GL_PROJECTION);GL11.glLoadIdentity();GL11.glOrtho(0,1280,720,0,-1,1);GL11.glMatrixMode(GL11.GL_MODELVIEW);GL11.glLoadIdentity();GL11.glEnable(GL11.GL_BLEND);GL11.glBlendFunc(GL11.GL_SRC_ALPHA,GL11.GL_ONE_MINUS_SRC_ALPHA);
   var renderer=StartupRuntime.class.getDeclaredField("renderer");renderer.setAccessible(true);renderer.set(null,new GameRenderer(()->clock[0]));
   NativeLuaFixture f=new NativeLuaFixture(Path.of(args[0]),1280,720);nativeDrawing(f);StartupRuntime.serverBegin("Owned connection");ConnectToServerState owner=new ConnectToServerState();owner.enter();
   frame(output,"ownership-valid");check(drawers()==1&&f.draws.isEmpty(),"owned passive page retains one OZL scene and suppresses native decoration");sprites.recycle();

   UIElement unknown=new UIElement(f.platform.newTable());f.connect.controls.add(unknown);f.draws.clear();
   frame(output,"ownership-rejected");check(drawers()==0,"unowned page queues no OZL scene");check(f.draws.stream().anyMatch(d->d.element==f.connect)&&f.child("backBtn").renders==2,"unowned native modal and Abort remain usable");sprites.recycle();
   int resets=Core.instance.resets;Core.instance.duringReset=()->check(state().route!=LoadingModel.Route.FULL,"unowned connected ResetLua never acquires full overlay");
   owner.duringUpdate=()->{try{Core.instance.ResetLua("client","ConnectedToServer");}catch(java.io.IOException e){throw new AssertionError(e);}};owner.update();owner.duringUpdate=null;Core.instance.duringReset=null;
   check(Core.instance.resets==resets+1,"unowned original ResetLua still runs exactly once");sprites.recycle();f.connect.controls.remove(unknown);

   var original=UIElement.call;
   UIElement.call=(element,method)->{if(element==f.main&&method.equals("prerender"))f.connect.controls.add(unknown);original.accept(element,method);};
   try{MainScreenState.instance.render();var stale=drawer();sentinel();stale.render();check(untouched(),"same-frame ownership rejection revokes already queued scene");check(!f.draws.isEmpty(),"same-frame ownership rejection preserves native commands");}
   finally{UIElement.call=original;f.connect.controls.remove(unknown);sprites.recycle();}
   UIElement.call=(element,method)->{if(element==f.main&&method.equals("prerender"))LuaManager.env=f.platform.newTable();original.accept(element,method);};
   try{MainScreenState.instance.render();var stale=drawer();sentinel();stale.render();check(untouched(),"same-frame Lua environment replacement revokes queued scene");}
   finally{LuaManager.env=f.env;UIElement.call=original;sprites.recycle();}

   f.main.nativeRender=()->LuaManager.env=f.platform.newTable();
   try{MainScreenState.instance.render();var stale=drawer();sentinel();stale.render();check(untouched(),"ownership loss after last native draw is caught before frame publication");}
   finally{LuaManager.env=f.env;f.main.nativeRender=null;sprites.recycle();}

   MainScreenState.instance.failRender=true;boolean threw=false;try{MainScreenState.instance.render();}catch(IllegalStateException e){threw=e.getMessage().equals("original render");}finally{MainScreenState.instance.failRender=false;}
   var failedFrame=drawer();sentinel();failedFrame.render();check(threw&&untouched(),"failed native frame preserves original error and revokes scene");sprites.recycle();

   MainScreenState.instance.render();var retained=drawer();sprites.recycle();f.call("onBackButton");check(f.cancellations==1&&!f.connect.visible,"installed native Abort cancels once and hides its connection panel");
   frame(output,"ownership-cancelled");check(drawers()==0,"cancelled hidden page cannot retain OZL background");sprites.recycle();MainScreenState.instance.enter();sentinel();retained.render();check(!state().active&&untouched(),"return to menu invalidates previous connection drawer");
   long oldGeneration=state().generation;StartupRuntime.serverBegin("Reconnect");owner=new ConnectToServerState();owner.enter();f.run("main.bottomPanel:setVisible(false);connection:setVisible(true)");f.draws.clear();
   frame(output,"ownership-reconnected");check(state().generation!=oldGeneration&&drawers()==1&&f.draws.isEmpty(),"healthy reconnect reacquires current native page without changing layout");sprites.recycle();

   MainScreenState.instance.render();var first=drawer();MainScreenState.instance.render();sentinel();first.render();check(!untouched(),"normal frame cleanup preserves legitimate in-flight owned scene");sprites.recycle();
   LuaManager.env=f.platform.newTable();MainScreenState.instance.render();check(drawers()==0,"missing Lua ownership never manufactures a fallback scene");LuaManager.env=f.env;sprites.recycle();
   f.connect.visible=false;f.connect.getTable().rawset("visible",false);StartupRuntime.serverBegin("Queue ownership");LoadingQueueState q=new LoadingQueueState();q.enter();q.render();check(drawers()==1,"valid native initial queue retains existing background");sprites.recycle();
   f.main.controls.add(unknown);q.render();check(drawers()==0,"queue without native page ownership keeps only native commands");sprites.recycle();f.main.controls.remove(unknown);
   LoadingQueueState.onPlaceInQueue(4,new HashMap<>());f.main.controls.add(unknown);f.main.nativeRender=()->f.main.controls.remove(unknown);
   try{q.render();check(drawers()==0,"a rejected positioned queue cannot acquire a scene after native controls in the same frame");}
   finally{f.main.nativeRender=null;f.main.controls.remove(unknown);sprites.recycle();}
   q.render();check(drawers()==1&&sprites.queue.indexOf("background")==1&&sprites.queue.indexOf(drawer())==2&&!sprites.queue.contains("queue-controls"),"recovered queue acquires the next frame at the original background slot");sprites.recycle();
   check(GL11.glGetError()==GL11.GL_NO_ERROR,"ownership scenarios preserve valid GL state");
   if(!failures.isEmpty())throw new AssertionError(failures.size()+" failures: "+String.join("; ",failures));
   System.out.println("PASS "+checks+" connection ownership contracts");
  }finally{Core.instance.duringReset=null;LuaEventManager.original=null;sprites.recycle();Display.created=false;GLFW.glfwDestroyWindow(window);GLFW.glfwTerminate();}
 }
}
