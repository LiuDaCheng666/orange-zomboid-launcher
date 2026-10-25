package zombie.gameStates;
public final class GameLoadingState extends GameState {
    public static boolean worldVersionError,unexpectedError,playerWrongIP,done;
    public static String gameLoadingString="正在读取世界";
    public int entered,rendered,exited;
    public static boolean convertingWorld;public static int convertingFileCount,convertingFileMax;
    public void enter(){entered++;done=false;}public void render(){rendered++;}public void exit(){exited++;}
    public static void Done(){done=true;}public static void SendDone(){done=true;}
    public GameStateMachine.StateAction update(){return done?GameStateMachine.StateAction.Continue:GameStateMachine.StateAction.Remain;}
}
