package zombie.gameStates;
public final class GameLoadingState {
    public static boolean worldVersionError,unexpectedError,playerWrongIP,done;
    public static String gameLoadingString="正在读取世界";
    public int entered,rendered,exited;
    public void enter(){entered++;}public void render(){rendered++;}public void exit(){exited++;}
}
