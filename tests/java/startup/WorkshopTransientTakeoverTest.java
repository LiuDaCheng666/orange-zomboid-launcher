package ozl.startup.tests;
import java.nio.file.Path;
import ozl.startup.*;
import zombie.core.SpriteRenderer;
import zombie.gameStates.*;
import zombie.ui.*;
/** A declined native CPU frame must not corrupt the same native download operation. */
public final class WorkshopTransientTakeoverTest {
 static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);}
 public static void main(String[] args)throws Exception{
  Path game=Path.of(args[0]);
  for(boolean overlay:new boolean[]{false,true}){
   NativeLuaFixture f=new NativeLuaFixture(game,1280,720);ConnectToServerState owner=WorkshopDownloadTest.prepare(f,game);
   UIElement page=NativeLuaFixture.element((se.krka.kahlua.vm.KahluaTable)f.env.rawget("workshop"));
   UIElement other=new UIElement(f.platform.newTable());other.getTable().rawset("Type","ISToolTip");
   if(overlay)UIManager.UI.add(other);else page.visible=false;
   MainScreenState.instance.render();SpriteRenderer.instance.recycle();
   check(!StartupRuntime.snapshot().active,"incomplete native page or visible unknown root must retain original UI");
   if(overlay)UIManager.UI.remove(other);else page.visible=true;
   owner.update();MainScreenState.instance.render();
   check(StartupRuntime.snapshot().active&&StartupRuntime.snapshot().downloadPage,"same-owner download must reacquire a fully valid native page after transient rejection, overlay="+overlay);
   check(WorkshopDownloadTest.number(WorkshopDownloadTest.widget(f,"listbox"),"y")>92,"restored native page is reflowed only after full revalidation");
   WorkshopDownloadTest.queuedTicket();SpriteRenderer.instance.recycle();
   WorkshopDownloadTest.event(owner,"Error",Long.valueOf(101),"DownloadItemResult9");owner.update();MainScreenState.instance.render();SpriteRenderer.instance.recycle();
   check(!StartupRuntime.snapshot().active,"real native Error still ends custom ownership and cannot reacquire");
  }
  NativeLuaFixture f=new NativeLuaFixture(game,1280,720);ConnectToServerState owner=WorkshopDownloadTest.prepare(f,game);
  UIElement page=NativeLuaFixture.element((se.krka.kahlua.vm.KahluaTable)f.env.rawget("workshop"));page.visible=false;
  MainScreenState.instance.render();SpriteRenderer.instance.recycle();
  boolean originalException=false;try{owner.FromLua("bad");}catch(IllegalArgumentException expected){originalException=true;}
  check(originalException,"native command exception must remain observable while the presentation is yielded");
  page.visible=true;owner.update();MainScreenState.instance.render();SpriteRenderer.instance.recycle();
  check(!StartupRuntime.snapshot().active&&!StartupRuntime.snapshot().downloadPage,"terminal native command failure while yielded must prevent later reactivation");
  ConnectToServerState old=owner;owner=WorkshopDownloadTest.prepare(f,game);
  try{old.FromLua("bad");}catch(IllegalArgumentException expected){}
  owner.update();MainScreenState.instance.render();SpriteRenderer.instance.recycle();
  check(StartupRuntime.snapshot().active&&StartupRuntime.snapshot().downloadPage,"an old owner's command exception must not invalidate the new download operation");
  System.out.println("PASS workshop transient recovery, yielded command failure, stale-owner exception and real Error handoff");
 }
}
