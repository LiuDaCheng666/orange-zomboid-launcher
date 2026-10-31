package ozl.startup.tests;
import java.nio.file.Path;
import ozl.startup.*;
import zombie.core.SpriteRenderer;
import zombie.gameStates.*;
import zombie.ui.UIElement;
/** Models the installed UIElement float setters followed by ISUIElement:onResize. */
public final class WorkshopGeometryRecoveryTest {
 static void nativeResize(UIElement widget){
  widget.javaY=(double)(float)widget.getY().doubleValue();
  widget.javaHeight=(double)(float)widget.getHeight().doubleValue();
  widget.getTable().rawset("y",widget.getY());
  widget.getTable().rawset("height",widget.getHeight());
 }
 public static void main(String[] args)throws Exception{
  Path game=Path.of(args[0]);NativeLuaFixture fixture=new NativeLuaFixture(game,1280,720);
  ConnectToServerState owner=WorkshopDownloadTest.prepare(fixture,game);
  UIElement list=WorkshopDownloadTest.widget(fixture,"listbox"),label=WorkshopDownloadTest.widget(fixture,"label");
  double originalY=WorkshopDownloadTest.number(list,"y"),originalHeight=WorkshopDownloadTest.number(list,"height"),originalLabel=WorkshopDownloadTest.number(label,"y");
  MainScreenState.instance.render();SpriteRenderer.instance.recycle();
  nativeResize(list);nativeResize(label);
  MainScreenState.instance.render();SpriteRenderer.instance.recycle();
  WorkshopDownloadTest.check(StartupRuntime.snapshot().active&&StartupRuntime.snapshot().downloadPage,"native float geometry round trip must retain custom workshop presentation");
  nativeResize(list);nativeResize(label);
  WorkshopDownloadTest.event(owner,"Error",Long.valueOf(101),"DownloadItemResult9");
  WorkshopDownloadTest.check(!StartupRuntime.snapshot().active&&Math.abs(WorkshopDownloadTest.number(list,"y")-originalY)<.001&&Math.abs(WorkshopDownloadTest.number(list,"height")-originalHeight)<.001&&Math.abs(WorkshopDownloadTest.number(label,"y")-originalLabel)<.001,"native error must restore full list and label after native float resize");
  System.out.println("PASS workshop geometry native float resize and error restoration");
 }
}
