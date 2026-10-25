package ozl.startup.tests;
import ozl.startup.StartupRuntime;
import zombie.*;
import zombie.Lua.LuaManager;
import zombie.gameStates.*;
import zombie.core.Translator;

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
        localizedPhases();
        MainScreenState menu=new MainScreenState();menu.enter();
        check(menu.entered==1&&!StartupRuntime.snapshot().active,"real transformed menu entry releases the game window");
        GameLoadingState loading=new GameLoadingState();loading.enter();loading.render();
        check(loading.entered==1&&loading.rendered==1&&StartupRuntime.snapshot().active,"world loading uses the same game presentation lifecycle");
        GameLoadingState.done=true;loading.render();
        check(!StartupRuntime.snapshot().active&&loading.rendered==2,"click-to-start completion stays visible and interactive");
        loading.exit();check(loading.exited==1&&!StartupRuntime.snapshot().active,"world exit remains original and clears presentation");
        System.out.println("PASS "+checks+" real-agent transformer behavior checks");
    }
    static void localizedPhases(){
        String[] keys={"Scripts","Clothing","ModelsAnimations","OnGameBoot","Texturepack","Lua"};
        int[] indices={3,2,2,5,2,4};
        String[] stages={"脚本","资源","资源","就绪","资源","界面"};
        String[] japanese={"スクリプトを読み込み中","衣服を準備中","モデルとアニメーションを準備中","ゲーム起動イベントを実行中","テクスチャを準備中：未知作者Mod名","インターフェースを準備中"};
        String[] chinese={"正在读取脚本","正在读取服装","正在读取模型与动画","正在启动游戏事件","正在读取材质包：未知作者Mod名","正在准备用户界面"};
        for(int locale=0;locale<2;locale++){
            Translator.chinese=locale==1;
            for(int i=0;i<keys.length;i++){
                GameWindow.DoLoadingText("Loading Mods");
                int calls=Translator.calls;
                String text=Translator.getText("UI_Loading_"+keys[i],"未知作者Mod名");
                check(text.equals((locale==0?japanese:chinese)[i])&&Translator.calls==calls+1,"translation advice preserves original localized result and invocation");
                check(StartupRuntime.snapshot().stageIndex==1,"observing a translation alone never advances a phase");
                GameWindow.DoLoadingText(text);
                check(StartupRuntime.snapshot().stageIndex==indices[i]&&StartupRuntime.snapshot().stage.equals(stages[i]),"localized "+keys[i]+" advances via actual transformed Translator and DoLoadingText");
            }
        }
        GameWindow.DoLoadingText("Loading Mods");
        GameWindow.DoLoadingText(Translator.getText("UI_Loading_Unknown"));
        GameWindow.DoLoadingText(Translator.getText("UI_MainScreen_Scripts"));
        check(StartupRuntime.snapshot().stageIndex==1,"unrelated UI keys cannot register loading phases");
        String noArgs=Translator.getText("UI_Loading_Scripts");
        GameWindow.DoLoadingText(noArgs);
        check(noArgs.equals(chinese[0])&&StartupRuntime.snapshot().stageIndex==3,"String-first String-return overload preserves translation and registers phase");
        boolean threw=false;try{Translator.getText("throws");}catch(IllegalArgumentException original){threw=original.getMessage().equals("original translation failure");}
        check(threw&&Translator.getText(9)==10,"original translation exception and unrelated overload remain intact");
        String oldest=Translator.getText("UI_Loading_Texturepack","Bounded-0"),latest="";
        for(int i=1;i<=64;i++)latest=Translator.getText("UI_Loading_Texturepack","Bounded-"+i);
        GameWindow.DoLoadingText("Loading Mods");GameWindow.DoLoadingText(oldest);
        check(StartupRuntime.snapshot().stageIndex==1,"translated-label mapping evicts old dynamic pack labels after its fixed bound");
        GameWindow.DoLoadingText(latest);
        check(StartupRuntime.snapshot().stageIndex==2,"bounded mapping retains current arbitrary pack labels");
        GameWindow.DoLoadingText("Loading Translations");
        check(StartupRuntime.snapshot().stage.equals("语言"),"early literal English translation phase still works");
    }
}
