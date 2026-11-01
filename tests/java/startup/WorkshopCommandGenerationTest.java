package ozl.startup.tests;
import java.nio.file.Path;
import ozl.startup.*;
import zombie.core.SpriteRenderer;
import zombie.gameStates.*;
/** An obsolete command completion cannot reject a current native download generation. */
public final class WorkshopCommandGenerationTest {
 public static void main(String[] args)throws Exception{
  Path game=Path.of(args[0]);NativeLuaFixture f=new NativeLuaFixture(game,1280,720);
  ConnectToServerState owner=WorkshopDownloadTest.prepare(f,game);
  StartupRuntime.commandAfter(owner,"install",StartupRuntime.generation()-1,new IllegalStateException("obsolete command completion"));
  owner.update();MainScreenState.instance.render();SpriteRenderer.instance.recycle();
  if(!StartupRuntime.snapshot().active||!StartupRuntime.snapshot().downloadPage)throw new AssertionError("old generation exception must not invalidate the current same-owner download");
  System.out.println("PASS obsolete command generation leaves current workshop operation intact");
 }
}
