package ozl.startup.tests;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.Path;
import java.util.*;
import ozl.startup.*;
import zombie.core.SpriteRenderer;
import zombie.gameStates.*;
import zombie.network.GameClient;
import zombie.ui.*;
/** Manual menu connection with installed workshop Lua; isolated engine fixture only. */
public final class WorkshopTakeoverDiagnosticsTest {
 static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);}
 public static void main(String[] args)throws Exception{
  Path game=Path.of(args[0]);NativeLuaFixture f=new NativeLuaFixture(game,1280,720);
  StartupRuntime.menuReady();
  GameClient.instance.doConnect("private-user","private-password","fixture.invalid","","16261","","Manual fixture",true,0,"");
  ConnectToServerState owner=new ConnectToServerState();owner.enter();owner.items(new ConnectToServerState.WorkshopItem(101,ConnectToServerState.WorkshopItemState.DownloadPending));
  f.workshop(game,owner);WorkshopDownloadTest.event(owner,"Required",new ArrayList<>(List.of("101")));
  var metadata=new NativeLuaFixture.Details("101","Fixture item","Downloading");f.metadata(metadata);
  WorkshopDownloadTest.event(owner,"Details",new ArrayList<>(List.of(metadata)));f.run("workshop:onButtonInstall()");owner.update();
  check(StartupRuntime.snapshot().active&&StartupRuntime.snapshot().downloadPage,"manual connection must enter download observation after native install");
  UIElement dialog=new UIElement(f.platform.newTable());dialog.getTable().rawset("Type","ISToolTip");UIManager.UI.add(dialog);
  ByteArrayOutputStream bytes=new ByteArrayOutputStream();PrintStream previous=System.out;
  try{System.setOut(new PrintStream(bytes,true,StandardCharsets.UTF_8));MainScreenState.instance.render();SpriteRenderer.instance.recycle();owner.update();MainScreenState.instance.render();SpriteRenderer.instance.recycle();}
  finally{System.setOut(previous);}
  String log=bytes.toString(StandardCharsets.UTF_8);
  check(log.contains("[OZL-WORKSHOP] unavailable")&&log.contains("stage=roots")&&log.contains("class=zombie.ui.UIElement")&&log.contains("type=ISToolTip")&&log.contains("visible=true"),"ownership rejection must report exact native root class/type/visibility: "+log);
  check(log.lines().filter(s->s.contains("[OZL-WORKSHOP] unavailable")).count()==1,"same operation rejection is logged once");
  check(!log.contains("private-user")&&!log.contains("private-password")&&!log.contains("fixture.invalid"),"diagnostics never include connection identity");
  check(!StartupRuntime.snapshot().active,"unknown visible root remains native and interactive");
  System.out.println("PASS manual workshop lifecycle and bounded native-root rejection diagnostics");
 }
}
