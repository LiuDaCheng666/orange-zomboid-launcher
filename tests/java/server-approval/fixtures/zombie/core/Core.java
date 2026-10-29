package zombie.core;
public final class Core {
    private static final Core instance = new Core();
    public static Core getInstance() { return instance; }
    public static String getMyDocumentFolder() { return System.getProperty("ozl.fixture.user"); }
    public int getScreenWidth() { return 1366; }
}
