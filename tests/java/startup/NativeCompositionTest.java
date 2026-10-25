package ozl.startup.tests;
import java.nio.file.*;
import ozl.startup.*;
import zombie.ui.*;
import zombie.gameStates.MainScreenState;
import zombie.core.SpriteRenderer;
import zombie.Lua.*;
import zombie.gameStates.*;
import java.util.*;

/** Fails against the old backdrop-only implementation after executing actual native Lua. */
public final class NativeCompositionTest {
 static int checks;static void check(boolean value,String why){if(!value)throw new AssertionError(why);checks++;}
 public static void main(String[] args)throws Exception{
  NativeLuaFixture fixture=new NativeLuaFixture(Path.of(args[0]),1280,720);
  StartupRuntime.serverBegin("Composition fixture");MainScreenState.instance.render();
  check(fixture.main.renders==1&&fixture.connect.renders==1,"native parents retain traversal");
  check(fixture.child("connectLabel").renders==1&&fixture.child("backBtn").renders==1,"native status and Abort always traverse");
  check(fixture.draws.stream().noneMatch(d->d.texture==fixture.main.getTable().rawget("logoTexture")),"single scene retires actual MainScreen Lua logo");
  check(fixture.draws.stream().noneMatch(d->d.element==fixture.connect),"single scene retires actual .7W x .8H panel/arrows");
  check(fixture.child("title").renders==0&&fixture.child("richText").renders==0,"known native decoration children retired");
  check(!StartupRuntime.skipElement(fixture.child("title"))&&!StartupRuntime.skipTexture(fixture.main,fixture.main.getTable().rawget("logoTexture"),"SCALED"),"no prior-frame lease survives native frame exit");
  try(var rejected=new java.net.URLClassLoader(new java.net.URL[0],null)){check(!QueuedBackdrop.enqueue(rejected,new QueuedBackdrop.Ticket(StartupRuntime.snapshot(),"MENU")),"foreign loader cannot report accepted draw or arm a presentation lease");}
  UIElement unknown=new UIElement(fixture.platform.newTable());fixture.connect.controls.add(unknown);fixture.draws.clear();MainScreenState.instance.render();
  check(fixture.draws.stream().anyMatch(d->d.texture==fixture.main.getTable().rawget("logoTexture")),"unknown child fails open to native logo/panel");fixture.connect.controls.remove(unknown);
  UIElement page=new UIElement(fixture.platform.newTable());fixture.main.controls.add(page);fixture.draws.clear();MainScreenState.instance.render();check(!fixture.draws.isEmpty(),"unknown visible page keeps native drawing");fixture.main.controls.remove(page);
  Object title=fixture.connect.getTable().rawget("title");fixture.connect.getTable().rawset("title",null);fixture.draws.clear();MainScreenState.instance.render();check(!fixture.draws.isEmpty(),"missing named API/table fails open");fixture.connect.getTable().rawset("title",title);
  fixture.draws.clear();UIElement.drawing=d->{fixture.draws.add(d);};
  fixture.main.nativeRender=()->{fixture.main.DrawTextureScaledColor(new zombie.core.textures.Texture("foreign",20,20),1d,1d,20d,20d,1d,1d,1d,1d);unknown.DrawTextureScaledColor((zombie.core.textures.Texture)fixture.main.getTable().rawget("logoTexture"),1d,1d,20d,20d,1d,1d,1d,1d);};
  MainScreenState.instance.render();check(fixture.draws.size()==2,"foreign texture and foreign element using same logo remain untouched");fixture.main.nativeRender=null;
  fixture.draws.clear();String prior=StartupRuntime.enterFrame("zombie.gameStates.MainScreenState");UIManager.useUiFbo=true;MainScreenState.instance.renderBackground();fixture.main.render();StartupRuntime.exitFrame(prior);check(fixture.draws.size()>=6,"unsupported FBO route keeps full native presentation");
  fixture.draws.clear();var originalCall=UIElement.call;UIElement.call=(e,name)->{if(e==fixture.main&&name.equals("prerender"))LuaManager.env=fixture.platform.newTable();originalCall.accept(e,name);};MainScreenState.instance.render();check(fixture.draws.size()>=6,"ResetLua replacement invalidates same-frame lease");LuaManager.env=fixture.env;UIElement.call=originalCall;
  MainScreenState.instance.failRender=true;boolean thrown=false;try{MainScreenState.instance.render();}catch(IllegalStateException original){thrown=true;}MainScreenState.instance.failRender=false;check(thrown&&!StartupRuntime.skipElement(fixture.child("title")),"native exception and frame cleanup preserved");
  ConnectToServerState owner=new ConnectToServerState();owner.enter();owner.update();LuaEventManager.triggerEvent("OnServerWorkshopItems","Required",new ArrayList<>(List.of("1")));
  check(!StartupRuntime.snapshot().active,"Required yields before original workshop interaction");
  LuaEventManager.triggerEvent("OnServerWorkshopItems","Progress","1",1L,2L);owner.update();check(!StartupRuntime.snapshot().active,"WorkshopUpdate does not resume while native interaction remains open");
  fixture.call("onBackButton");check(fixture.cancellations==1&&!fixture.connect.visible,"original native Abort callback cancels exactly once");
  SpriteRenderer.instance.recycle();System.out.println("PASS "+checks+" actual native Lua composition contracts");
 }
}
