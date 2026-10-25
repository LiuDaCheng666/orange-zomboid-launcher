package zombie.gameStates;
import java.util.HashMap;
public final class LoadingQueueState extends GameState {
    private static boolean cancel,done;private static int placeInQueue=-1;
    public void enter(){cancel=done=false;placeInQueue=-1;}
    public void render(){boolean prior=zombie.ui.UIManager.useUiFbo;zombie.ui.UIManager.useUiFbo=false;try{if(placeInQueue>=0){MainScreenState.instance.renderBackground();zombie.ui.UIManager.render();zombie.core.SpriteRenderer.instance.queue.add("queue-controls");}}finally{zombie.ui.UIManager.useUiFbo=prior;}}
    public GameStateMachine.StateAction update(){return cancel||done?GameStateMachine.StateAction.Continue:GameStateMachine.StateAction.Remain;}
    public GameState redirectState(){return cancel?new MainScreenState():new GameLoadingState();}
    public static void onConnectionImmediate(){done=true;}
    public static void onPlaceInQueue(int place,HashMap<String,Object> stats){placeInQueue=place;}
    public static void cancel(){cancel=true;}
}
