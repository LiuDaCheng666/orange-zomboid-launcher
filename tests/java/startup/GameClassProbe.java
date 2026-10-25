package ozl.startup.tests;
import ozl.startup.StartupRuntime;
/** Loads and verifies actual 42.21 classes without initializing them, so this
 * check cannot create a game window or start Steam. */
public final class GameClassProbe {
    public static void main(String[] args)throws Exception {
        String[] names={"zombie.GameWindow","zombie.ZomboidFileSystem","zombie.Lua.LuaManager","zombie.gameStates.MainScreenState","zombie.gameStates.GameLoadingState","org.lwjglx.opengl.Display","zombie.core.opengl.RenderThread","zombie.core.Translator"};
        if(StartupRuntime.snapshot()==null)throw new AssertionError("No accepted agent session");
        for(String name:names){
            Class<?> type=Class.forName(name,false,ClassLoader.getSystemClassLoader());
            type.getDeclaredMethods();
            if(!StartupRuntime.snapshot().active)throw new AssertionError("Actual game transformation failed: "+name);
        }
        Class.forName(names[0],false,ClassLoader.getSystemClassLoader()).getDeclaredMethod("DoLoadingText",String.class);
        Class.forName(names[1],false,ClassLoader.getSystemClassLoader()).getDeclaredMethod("loadMod",String.class);
        Class.forName(names[5],false,ClassLoader.getSystemClassLoader()).getDeclaredMethod("swapBuffers");
        Class<?> translator=Class.forName(names[7],false,ClassLoader.getSystemClassLoader());
        int targets=0;
        for(java.lang.reflect.Method method:translator.getDeclaredMethods()){
            Class<?>[] parameters=method.getParameterTypes();
            if(method.getName().equals("getText")&&parameters.length>0&&parameters[0]==String.class&&method.getReturnType()==String.class)targets++;
        }
        if(targets==0||translator.getDeclaredMethod("getText",String.class,Object[].class).getReturnType()!=String.class)throw new AssertionError("Actual Translator getText target absent");
        System.out.println("PASS 8 actual game-class transforms and JVM verification (including "+targets+" Translator target; no class initialization/window)");
    }
}
