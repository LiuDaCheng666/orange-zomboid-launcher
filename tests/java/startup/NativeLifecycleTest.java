package ozl.startup.tests;
import java.nio.file.*;
import java.util.*;
import ozl.startup.*;
import zombie.Lua.*;
import zombie.ui.*;
import zombie.gameStates.*;
import zombie.core.SpriteRenderer;

public final class NativeLifecycleTest {
 static int checks;static void check(boolean v,String why){if(!v)throw new AssertionError(why);checks++;}
 public static void main(String[] args)throws Exception{
  Path game=Path.of(args[0]);NativeLuaFixture f=new NativeLuaFixture(game,1280,720);StartupRuntime.serverBegin("Workshop native");ConnectToServerState owner=new ConnectToServerState();owner.enter();owner.update();f.workshop(game,owner);
  var nativeDispatch=LuaEventManager.original;LuaEventManager.original=(event,values)->{if(event.equals("OnServerWorkshopItems")&&values[0].equals("Required"))check(!StartupRuntime.snapshot().active,"yield occurs before original Required dispatch");nativeDispatch.accept(event,values);};
  LuaEventManager.triggerEvent("OnServerWorkshopItems","Required",new ArrayList<>(List.of("123")));check(!f.connect.visible&&!StartupRuntime.snapshot().active,"actual workshop Required hides connection and owns presentation");
  f.run("details={getIDString=function() return '123' end}");LuaEventManager.triggerEvent("OnServerWorkshopItems","Details",new ArrayList<>(List.of(f.env.rawget("details"))));
  f.run("assert(workshop.installButton.enabled == true);workshop:onButtonInstall();assert(workshop.installButton.enabled == false)");check(owner.calls==3,"native Details/Install callback reaches original FromLua once");
  LuaEventManager.triggerEvent("OnServerWorkshopItems","Progress","123",12L,24L);owner.update();MainScreenState.instance.render();check(!StartupRuntime.snapshot().active,"WorkshopUpdate does not reclaim native progress page");f.run("assert(workshop.listbox.items[1].item.downloaded == 12)");
  owner.duringUpdate=()->LuaEventManager.triggerEvent("OnServerWorkshopItems","Success");owner.update();owner.duringUpdate=null;MainScreenState.instance.render();check(!StartupRuntime.snapshot().active,"Success alone cannot resume while native connection is hidden");
  f.run("connection:setVisible(true)");MainScreenState.instance.render();check(StartupRuntime.snapshot().active,"same operation resumes only after native success and passive page closure");
  LuaEventManager.triggerEvent("OnServerWorkshopItems","Required",new ArrayList<>(List.of("123")));LuaEventManager.triggerEvent("OnServerWorkshopItems","Error","123","Unknown");check(!StartupRuntime.snapshot().active,"native workshop error yields");
  // Native error branch uses localization stubs only; callbacks and controls remain unchanged.
  StartupRuntime.serverBegin("Auth native");f.run("workshop:setVisible(false);connection:setVisible(true)");LuaEventManager.triggerEvent("OnGoogleAuthRequest");check(!StartupRuntime.snapshot().active&&f.child("googleAuthPopup").visible,"original auth popup remains authoritative");
  StartupRuntime.serverBegin("QR native");Object poison=new Object(){public String toString(){throw new AssertionError("QR payload inspected");}};LuaEventManager.triggerEvent("OnQRReceived",poison);check(!StartupRuntime.snapshot().active,"QR handoff leaves payload unread");
  StartupRuntime.serverBegin("Character native");f.run("connection.googleAuthPopup:setVisible(false);connection:setVisible(true);function connectionManagerLog(a,b) end;function checkSavePlayerExists() return false end;MapSpawnSelect={instance={hasChoices=function() return false end,useDefaultSpawnRegion=function() end}};WorldSelect={instance={hasChoices=function() return false end}};main.charCreationProfession=ISPanel:new(0,0,300,300);main.charCreationProfession:instantiate();main.charCreationProfession:setVisible(false);main:addChild(main.charCreationProfession)");
  LuaEventManager.triggerEvent("OnConnected");f.run("assert(main.charCreationProfession:getIsVisible())");check(!f.connect.visible&&!StartupRuntime.snapshot().active,"actual OnConnected keeps character/spawn handoff native");
  StartupRuntime.serverBegin("Saved player transition");f.run("main.charCreationProfession:setVisible(false);connection:setVisible(true);function checkSavePlayerExists() return true end;GameWindow={doRenderEvent=function(v) renderEvent=v end};LoadingQueueState={new=function() return 'pending-queue' end};function forceChangeState(next) pendingNext=next end");LuaEventManager.triggerEvent("OnConnected");f.run("assert(pendingNext == 'pending-queue');assert(renderEvent == false)");check(!StartupRuntime.snapshot().active,"actual saved-player OnConnected requests deferred native queue transition");
  zombie.network.GameClient.client=true;LoadingQueueState queue=new LoadingQueueState();queue.enter();check(StartupRuntime.snapshot().active,"native queue enter resumes retained server receipt");LoadingQueueState.onConnectionImmediate();GameLoadingState world=(GameLoadingState)queue.redirectState();world.enter();GameLoadingState.SendDone();check(StartupRuntime.snapshot().world&&StartupRuntime.snapshot().active,"multiplayer SendDone retains world loading until original Done");GameLoadingState.Done();check(!StartupRuntime.snapshot().active,"original Done releases native world click prompt");
  ConnectToServerState stale=owner;StartupRuntime.serverBegin("Reconnect");long generation=StartupRuntime.generation();owner=new ConnectToServerState();owner.enter();owner.update();f.run("main.charCreationProfession:setVisible(false);connection:setVisible(true)");LuaEventManager.triggerEvent("OnServerWorkshopItems","Required",new ArrayList<>(List.of("123")));stale.duringUpdate=()->LuaEventManager.triggerEvent("OnServerWorkshopItems","Success");stale.update();stale.duringUpdate=null;f.run("connection:setVisible(true)");MainScreenState.instance.render();check(StartupRuntime.generation()==generation&&!StartupRuntime.snapshot().active,"stale previous connection Success cannot resume current interaction");
  SpriteRenderer.instance.recycle();System.out.println("PASS "+checks+" extracted native workshop/auth/character lifecycle contracts");
 }
}
