package ozl.startup.tests;
import java.nio.file.*;
import ozl.startup.*;
import zombie.Lua.LuaEventManager;
import zombie.gameStates.*;
/** Installed event advice, fixture state identities; never auto-accepts native terms. */
public final class BootInteractionTest {
 static int checks;static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
 public static void main(String[] args)throws Exception{
  Session session=Session.read(Path.of("OrangeLauncherRuntime/startup/OrangeStartup.jar"),Path.of(System.getProperty("user.dir")),System.currentTimeMillis()/1000);
  check(session!=null,"real receipt accepted");StartupRuntime.begin(session);
  LuaEventManager.triggerEvent("OnGameStateEnter",new GameState());check(StartupRuntime.snapshot().active,"unrelated state leaves boot observations active");
  TermsOfServiceState terms=new TermsOfServiceState();int calls=LuaEventManager.calls;
  LuaEventManager.triggerEvent("OnGameStateEnter",terms);
  check(!StartupRuntime.snapshot().active,"native terms page receives display instead of being covered by boot overlay");
  check(LuaEventManager.calls==calls+1&&LuaEventManager.last==terms,"original native handler receives unchanged state exactly once");
  StartupRuntime.phase("late resource");check(!StartupRuntime.snapshot().active,"late boot observations cannot cover terms again");
  StartupRuntime.menuReady();StartupRuntime.serverBegin("after native terms");check(StartupRuntime.snapshot().active&&StartupRuntime.snapshot().context==LoadingModel.Context.SERVER,"future connection loading remains available after boot handoff");
  LuaEventManager.triggerEvent("OnGameStateEnter",terms);check(StartupRuntime.snapshot().active,"unrelated terms identity cannot end a server operation");
  for(String event:new String[]{"OnGoogleAuthRequest","OnQRReceived","OnConnected"}){StartupRuntime.serverBegin("auth");LuaEventManager.triggerEvent(event);check(!StartupRuntime.snapshot().active,"native auth handoff retained: "+event);}
  StartupRuntime.serverBegin("cancel");LuaEventManager.triggerEvent("OnConnectFailed");check(!StartupRuntime.snapshot().active,"native failed/cancel handoff retained");
  System.out.println("PASS "+checks+" boot interaction and native handoff contracts");
 }
}
