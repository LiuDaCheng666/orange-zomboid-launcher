package ozl.startup.tests;
import ozl.startup.StartupRuntime;
/** Loads and verifies actual 42.21 classes without initializing them, so this
 * check cannot create a game window or start Steam. */
public final class GameClassProbe {
    public static void main(String[] args)throws Exception {
        String[] names={"zombie.GameWindow","zombie.ZomboidFileSystem","zombie.Lua.LuaManager","zombie.gameStates.MainScreenState","zombie.gameStates.GameLoadingState","org.lwjglx.opengl.Display","zombie.core.opengl.RenderThread"};
        if(StartupRuntime.snapshot()==null)throw new AssertionError("No accepted agent session");
        for(String name:names){
            Class<?> type=Class.forName(name,false,ClassLoader.getSystemClassLoader());
            type.getDeclaredMethods();
            if(!StartupRuntime.snapshot().active)throw new AssertionError("Actual game transformation failed: "+name);
        }
        Class.forName(names[0],false,ClassLoader.getSystemClassLoader()).getDeclaredMethod("DoLoadingText",String.class);
        Class.forName(names[1],false,ClassLoader.getSystemClassLoader()).getDeclaredMethod("loadMod",String.class);
        Class.forName(names[5],false,ClassLoader.getSystemClassLoader()).getDeclaredMethod("swapBuffers");
        System.out.println("PASS 7 actual game-class transforms and JVM verification (no class initialization/window)");
    }
}
