package zombie.gameStates;
import java.util.HashMap;
public final class LoadingQueueState extends GameState {
    private static boolean cancel,done;private static int placeInQueue=-1;
    private static final zombie.ui.LoadingQueueUI ui=new zombie.ui.LoadingQueueUI();
    public boolean escape,aPressed,lostConnection;private boolean aButtonDown;public int requests;
    public void enter(){cancel=done=false;placeInQueue=-1;aButtonDown=aPressed;requests++;}
    public void render(){zombie.core.Core core=zombie.core.Core.instance;core.StartFrame();core.EndFrame();boolean prior=zombie.ui.UIManager.useUiFbo;zombie.ui.UIManager.useUiFbo=false;try{core.StartFrameUI();zombie.core.SpriteRenderer.instance.queue.add("black");if(placeInQueue>=0){MainScreenState.instance.renderBackground();zombie.ui.UIManager.render();ui.render();}core.EndFrameUI();}finally{zombie.ui.UIManager.useUiFbo=prior;}}
    public GameStateMachine.StateAction update(){if(lostConnection||escape||aPressed&&!aButtonDown)cancel=true;aButtonDown=aPressed;return cancel||done?GameStateMachine.StateAction.Continue:GameStateMachine.StateAction.Remain;}
    public GameState redirectState(){return cancel?new MainScreenState():new GameLoadingState();}
    public static void onConnectionImmediate(){done=true;}
    public static void onPlaceInQueue(int place,HashMap<String,Object> stats){placeInQueue=place;}
    public static void cancel(){cancel=true;}
}
