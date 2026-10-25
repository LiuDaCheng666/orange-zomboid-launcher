package ozl.startup.tests;
import ozl.startup.StartupRuntime;
import zombie.*;
import zombie.Lua.LuaManager;
import zombie.gameStates.*;

/** A separate JVM really installs the shipped agent; game-shaped fixtures only
 * stand in for game side effects, not for our transformer or loading model. */
public final class TransformerTest {
    static int checks;
    static void check(boolean value,String message){if(!value)throw new AssertionError(message);checks++;}
    public static void main(String[] args){
        check(StartupRuntime.snapshot()!=null&&StartupRuntime.snapshot().active,"fresh receipt installs the real premain component");
        GameWindow.DoLoadingText("Loading Mods");
        check(GameWindow.phases==1&&StartupRuntime.snapshot().stage.equals("模组"),"phase advice observes while preserving original game call");
        ZomboidFileSystem fs=new ZomboidFileSystem();fs.loadMod("NeverSeenBefore");
        check(fs.count==1&&StartupRuntime.snapshot().processedMods==1,"actual transformed mod method supplies arbitrary mod activity");
        boolean threw=false;try{fs.loadMod("throws");}catch(IllegalArgumentException original){threw=true;}
        check(threw&&StartupRuntime.snapshot().processedMods==1,"original game exceptions are preserved and never counted as success");
        check("original-return".equals(LuaManager.RunLua("/mods/NewAuthor/42/media/lua/client/Actual.lua",false))&&StartupRuntime.snapshot().processedScripts==1,"script advice preserves the game return value");
        MainScreenState menu=new MainScreenState();menu.enter();
        check(menu.entered==1&&!StartupRuntime.snapshot().active,"real transformed menu entry releases the game window");
        GameLoadingState loading=new GameLoadingState();loading.enter();loading.render();
        check(loading.entered==1&&loading.rendered==1&&StartupRuntime.snapshot().active,"world loading uses the same game presentation lifecycle");
        GameLoadingState.done=true;loading.render();
        check(!StartupRuntime.snapshot().active&&loading.rendered==2,"click-to-start completion stays visible and interactive");
        loading.exit();check(loading.exited==1&&!StartupRuntime.snapshot().active,"world exit remains original and clears presentation");
        System.out.println("PASS "+checks+" real-agent transformer behavior checks");
    }
}
