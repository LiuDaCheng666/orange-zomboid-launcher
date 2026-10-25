package zombie;
public final class GameWindow {
    public static int phases;
    public static boolean serverDisconnected;
    public static void DoLoadingText(String value){phases++;}
    public static int earlyCalls;
    public static void mainThreadInit(){earlyCalls++;}
    public static void init(){earlyCalls++;}
    public static void initFonts(){earlyCalls++;}
}
