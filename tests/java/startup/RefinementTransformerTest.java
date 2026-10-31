package ozl.startup.tests;
import ozl.startup.*;
import zombie.*;
import zombie.core.*;
import zombie.network.GameClient;
import zombie.Lua.LuaEventManager;
import zombie.gameStates.*;
import zombie.ui.UIManager;
import java.util.*;

public final class RefinementTransformerTest {
    static int checks;
    static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
    static void connect(String name){GameClient.instance.doConnect("PASSWORD","USERNAME","ADDRESS","SECRET","0","PRIVATE",name,false,123,"TOKEN");}
    static final class Poison {public String toString(){throw new AssertionError("private payload read");}}
    public static void main(String[] args)throws Exception {
        GameWindow.mainThreadInit();check(GameWindow.earlyCalls==1&&StartupRuntime.snapshot().title.contains("初始化"),"actual early original method observed before mods");
        ZomboidFileSystem fs=new ZomboidFileSystem();fs.init();fs.searchFolders(new java.io.File("C:/Users/Private/media/Actual.png"));
        check(fs.indexed==1&&fs.scanned==1&&StartupRuntime.snapshot().scannedResources==1,"actual scan callback counts without changing original calls");
        GameWindow.initFonts();check(StartupRuntime.snapshot().title.contains("字体"),"actual font operation observed");GameWindow.init();
        MainScreenState.instance.enter();connect("First server");long first=StartupRuntime.snapshot().epoch;
        check(StartupRuntime.snapshot().name.equals("First server")&&StartupRuntime.snapshot().route==LoadingModel.Route.BEHIND_UI,"only canonical friendly server name enters scene");
        check(GameClient.instance.calls==1&&!StartupRuntime.snapshot().detail.contains("PASSWORD"),"original connect and credentials preserved");
        GameClient.instance.doConnect("a","b","c","d","0","f","Forwarded",false,2);
        check(StartupRuntime.snapshot().epoch==first+1,"forwarding overload starts exactly one operation");
        GameClient.instance.serverMods.add("OldServerMod");
        ConnectToServerState state=new ConnectToServerState();state.enter();check(StartupRuntime.snapshot().map.isEmpty()&&StartupRuntime.snapshot().requestedMods==-1&&StartupRuntime.snapshot().workshopProjects==-1,"connection entry never reads previous static map/mod list or not-yet-populated project list");
        check(state.update()==GameStateMachine.StateAction.Remain&&StartupRuntime.snapshot().title.contains("同步"),"actual connection state after original update");
        boolean thrown=false;try{state.FromLua("bad");}catch(IllegalArgumentException original){thrown=true;}check(thrown,"original Lua command exception preserved");
        ArrayList<String> ids=new ArrayList<>(List.of("1","2","3"));LuaEventManager.triggerEvent("OnServerWorkshopItems","Required",ids);
        check(StartupRuntime.snapshot().workshopProjects==3&&StartupRuntime.snapshot().requestedMods==-1,"workshop count is projects; mod-ID list remains unknown until initialized for this operation");
        LuaEventManager.triggerEvent("OnServerWorkshopItems","Progress","1",12L,24L);check(StartupRuntime.snapshot().downloaded==-1,"unowned progress without accepted original Install is rejected");
        check(!StartupRuntime.snapshot().active,"Required retains native ownership throughout workshop interaction");connect("Post workshop passive");state.enter();
        Poison poison=new Poison();LuaEventManager.triggerEvent("Unrelated",poison,poison,poison,poison);check(LuaEventManager.last==poison,"unrelated payload untouched and original call preserved");
        MainScreenState.instance.render();List<Object> queue=SpriteRenderer.instance.queue;
        check(queue.equals(List.of("background","controls")),"missing native Lua ownership preserves original background and controls without an OZL drawer");
        check(UIManager.useUiFbo,"CPU restores UI flag before deferred GPU command");
        MainScreenState.instance.render();check(queue.equals(List.of("background","controls","background","controls")),"two unowned in-flight frames preserve all original rendering calls without an OZL drawer");SpriteRenderer.instance.drain();SpriteRenderer.instance.recycle();
        MainScreenState.instance.renderBackground();check(queue.size()==1,"unscoped background callers never enqueue");SpriteRenderer.instance.recycle();
        MainScreenState.instance.failBackground=true;thrown=false;try{MainScreenState.instance.render();}catch(IllegalArgumentException original){thrown=true;}MainScreenState.instance.failBackground=false;
        check(thrown&&queue.size()==1,"throwing original background is preserved without owned insertion");SpriteRenderer.instance.recycle();MainScreenState.instance.renderBackground();check(queue.size()==1,"exceptional render restores frame scope");SpriteRenderer.instance.recycle();
        LuaEventManager.triggerEvent("OnQRReceived",poison);check(!StartupRuntime.snapshot().active&&LuaEventManager.last==poison,"QR yields without reading private message");
        connect("Second server");LuaEventManager.triggerEvent("OnServerWorkshopItems","Error","ItemQueryNotCompleted");check(!StartupRuntime.snapshot().active,"short workshop query error yields");
        connect("Third server");LuaEventManager.triggerEvent("OnServerWorkshopItems","Error",12L,"ItemInstallFailed");check(!StartupRuntime.snapshot().active,"item workshop error yields");
        connect("Queue server");LuaEventManager.triggerEvent("OnConnected");state.exit();LoadingQueueState q=new LoadingQueueState();q.enter();LoadingQueueState.onPlaceInQueue(4,new HashMap<>());q.render();
        check(StartupRuntime.snapshot().name.equals("Queue server")&&StartupRuntime.snapshot().queuePosition==4&&queue.equals(List.of("black","background","controls","queue-controls")),"queue restores current-operation identity; missing Lua scope keeps only native queue controls and original black clear");SpriteRenderer.instance.recycle();
        LoadingQueueState.cancel();check(q.update()==GameStateMachine.StateAction.Continue&&!StartupRuntime.snapshot().active&&q.redirectState() instanceof MainScreenState,"original queue cancellation yields and invalidates tickets");
        GameClient.instance.doConnectCoop("private-coop-id");check(StartupRuntime.snapshot().name.isEmpty(),"coop never reads stale static normal-server name");
        GameClient.instance.serverMods.clear();GameClient.instance.serverMods.addAll(List.of("Mod1","Mod2"));GameLoadingState world=new GameLoadingState();Core.gameSaveWorld="ADDRESS_encrypted-user";world.enter();world.render();
        check(StartupRuntime.snapshot().context==LoadingModel.Context.SERVER&&StartupRuntime.snapshot().requestedMods==2&&StartupRuntime.snapshot().name.isEmpty(),"real server world retains scene with requested mod count and no cache label");
        GameLoadingState.Done();check(!StartupRuntime.snapshot().active,"actual world done immediately returns original click UI");
        GameClient.client=false;Core.gameSaveWorld="C:/Users/Private/LaterSave";world.enter();world.render();check(StartupRuntime.snapshot().context==LoadingModel.Context.SAVE&&StartupRuntime.snapshot().name.equals("LaterSave"),"later local load has fresh sanitized identity");
        StartupRuntime.worldFrame(new Object());check(!StartupRuntime.snapshot().active,"unverified world lifecycle API yields original UI instead of treating missing guards as false");
        LuaEventManager.triggerEvent("OnDisconnect",poison);check(!StartupRuntime.snapshot().active,"disconnect clears presentation without inspecting message");
        GameClient.instance.failConnect=true;thrown=false;try{connect("Failed server");}catch(IllegalStateException original){thrown=original.getMessage().equals("original connect");}GameClient.instance.failConnect=false;
        check(thrown&&!StartupRuntime.snapshot().active,"original canonical connection exception preserves failure and yields presentation");
        GameClient.instance.failCoop=true;thrown=false;try{GameClient.instance.doConnectCoop("private-id");}catch(IllegalStateException original){thrown=original.getMessage().equals("original coop");}GameClient.instance.failCoop=false;
        check(thrown&&!StartupRuntime.snapshot().active,"original coop connection exception preserves failure and yields presentation");
        System.out.println("PASS "+checks+" refinement premain/lifecycle/queue contracts");
    }
}
