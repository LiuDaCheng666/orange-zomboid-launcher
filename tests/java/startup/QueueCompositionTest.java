package ozl.startup.tests;
import ozl.startup.*;
import zombie.ui.*;
import zombie.gameStates.*;
import zombie.core.*;
import java.nio.file.*;
import java.util.*;

public final class QueueCompositionTest {
 static int checks;static void check(boolean value,String why){if(!value)throw new AssertionError(why);checks++;}
 static long drawers(){return SpriteRenderer.instance.queue.stream().filter(x->x instanceof zombie.core.textures.TextureDraw.GenericDrawer).count();}
 public static void main(String[] args)throws Exception{
  NativeLuaFixture f=new NativeLuaFixture(Path.of(args[0]),1280,720);f.connect.visible=false;f.connect.getTable().rawset("visible",false);
  StartupRuntime.serverBegin("Queue fixture");LoadingQueueState q=new LoadingQueueState();Core core=Core.instance;SpriteRenderer sprites=SpriteRenderer.instance;
  q.enter();q.render();check(sprites.queue.size()==2&&sprites.queue.get(0).equals("black")&&drawers()==1,"initial place=-1 inserts one scene after black clear without native background/UI");
  check(WorkshopDownloadTest.workshopTop(WorkshopDownloadTest.queuedTicket())==0,"initial queue has no workshop boundary");
  check(core.starts==1&&core.ends==1&&core.uiStarts==1&&core.uiEnds==1&&q.requests==1,"fallback preserves exactly one original Core frame and request");sprites.recycle();
  LoadingQueueState.onConnectionImmediate();q.render();check(drawers()==1&&sprites.queue.get(0).equals("black"),"immediate branch retains one conditional fallback");check(q.update()==GameStateMachine.StateAction.Continue&&q.redirectState() instanceof GameLoadingState,"original immediate redirect preserved");sprites.recycle();
  StartupRuntime.serverBegin("Queue fixture 2");q.enter();LoadingQueueState.onPlaceInQueue(4,new HashMap<>());q.render();check(drawers()==1&&sprites.queue.size()==3&&sprites.queue.get(1).equals("background"),"known passive queue panel retired after normal background slot; no duplicate fallback");
  check(WorkshopDownloadTest.workshopTop(WorkshopDownloadTest.queuedTicket())==0,"accepted positioned queue lease has no workshop boundary");
  check(f.draws.isEmpty()&&StartupRuntime.snapshot().queuePosition==4,"actual MainScreen Lua logo retired in positioned queue; authoritative packet retained");sprites.recycle();
  check(!StartupRuntime.skipQueue(new LoadingQueueUI()),"foreign queue panel never skipped outside accepted frame");
  q.escape=true;check(q.update()==GameStateMachine.StateAction.Continue&&q.redirectState() instanceof MainScreenState&&!StartupRuntime.snapshot().active,"native Esc cancellation yields and preserves redirect");
  StartupRuntime.serverBegin("Controller queue");q=new LoadingQueueState();q.aPressed=true;q.enter();check(q.update()==GameStateMachine.StateAction.Remain,"held controller A does not cancel on queue entry");q.aPressed=false;q.update();q.aPressed=true;check(q.update()==GameStateMachine.StateAction.Continue&&!StartupRuntime.snapshot().active,"controller A edge invokes native cancellation");
  StartupRuntime.serverBegin("Lost queue");q=new LoadingQueueState();q.enter();q.lostConnection=true;check(q.update()==GameStateMachine.StateAction.Continue&&!StartupRuntime.snapshot().active,"native lost connection yields");
  System.out.println("PASS "+checks+" queue initial/immediate/positioned/lifecycle/cancel contracts");
 }
}
