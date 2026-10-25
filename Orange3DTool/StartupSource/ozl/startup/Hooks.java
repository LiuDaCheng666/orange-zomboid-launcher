package ozl.startup;

import java.lang.instrument.Instrumentation;
import net.bytebuddy.agent.builder.AgentBuilder;
import net.bytebuddy.asm.Advice;
import static net.bytebuddy.matcher.ElementMatchers.*;

public final class Hooks {
    public static void install(Instrumentation instrumentation){
        new AgentBuilder.Default().disableClassFormatChanges()
            .with(AgentBuilder.InjectionStrategy.Disabled.INSTANCE)
            .with(AgentBuilder.InitializationStrategy.NoOp.INSTANCE)
            .with(AgentBuilder.RedefinitionStrategy.RETRANSFORMATION)
            .with(new AgentBuilder.Listener.Adapter(){
                @Override public void onError(String type,ClassLoader loader,net.bytebuddy.utility.JavaModule module,boolean loaded,Throwable error){StartupRuntime.failed(error);}
            })
            .type(named("zombie.GameWindow"))
            .transform((b,t,l,m,d)->b
                .visit(Advice.to(Phase.class).on(named("DoLoadingText").and(takesArguments(String.class)).and(returns(void.class))))
                .visit(Advice.to(Early.class).on(namedOneOf("mainThreadInit","init","initFonts").and(takesArguments(0)).and(returns(void.class)))))
            .type(named("zombie.core.Translator"))
            .transform((b,t,l,m,d)->b.visit(Advice.to(Translation.class).on(named("getText").and(takesArgument(0,String.class)).and(returns(String.class)))))
            .type(named("zombie.ZomboidFileSystem"))
            .transform((b,t,l,m,d)->b
                .visit(Advice.to(Mod.class).on(named("loadMod").and(takesArguments(String.class))))
                .visit(Advice.to(Index.class).on(named("init").and(takesArguments(0)).and(returns(void.class))))
                .visit(Advice.to(Resource.class).on(named("searchFolders").and(takesArguments(java.io.File.class)).and(returns(void.class)))))
            .type(named("zombie.Lua.LuaManager"))
            .transform((b,t,l,m,d)->b.visit(Advice.to(Script.class).on(named("RunLua").and(takesArgument(0,String.class)))))
            .type(named("zombie.gameStates.MainScreenState"))
            .transform((b,t,l,m,d)->b
                .visit(Advice.to(Menu.class).on(named("enter").and(takesArguments(0))))
                .visit(Advice.to(FrameScope.class).on(named("render").and(takesArguments(0))))
                .visit(Advice.to(Background.class).on(named("renderBackground").and(takesArguments(0)).and(returns(void.class)))))
            .type(named("zombie.gameStates.GameLoadingState"))
            .transform((b,t,l,m,d)->b
                .visit(Advice.to(WorldBegin.class).on(named("enter").and(takesArguments(0))))
                .visit(Advice.to(WorldEnd.class).on(namedOneOf("exit","Done").and(takesArguments(0))))
                .visit(Advice.to(WorldSent.class).on(named("SendDone").and(takesArguments(0))))
                .visit(Advice.to(WorldFrame.class).on(namedOneOf("render","update").and(takesArguments(0)))))
            .type(named("zombie.network.GameClient"))
            .transform((b,t,l,m,d)->b
                .visit(Advice.to(Connect.class).on(named("doConnect").and(takesArguments(String.class,String.class,String.class,String.class,String.class,String.class,String.class,boolean.class,int.class,String.class))))
                .visit(Advice.to(Coop.class).on(named("doConnectCoop").and(takesArguments(String.class))))
                .visit(Advice.to(Disconnect.class).on(named("disconnect").and(takesArguments(boolean.class)).or(named("doDisconnect").and(takesArguments(String.class)))))
                .visit(Advice.to(Sync.class).on(named("GameLoadingRequestData").and(takesArguments(0))))
                .visit(Advice.to(Request.class).on(named("setRequest").and(takesArguments(1)))))
            .type(named("zombie.gameStates.ConnectToServerState"))
            .transform((b,t,l,m,d)->b
                .visit(Advice.to(ConnectionState.class).on(namedOneOf("enter","update").and(takesArguments(0))))
                .visit(Advice.to(ConnectionCommand.class).on(named("FromLua").and(takesArguments(String.class))))
                .visit(Advice.to(ConnectionExit.class).on(named("exit").and(takesArguments(0))))
                .visit(Advice.to(ServerTime.class).on(named("receiveGameTime").and(takesArguments(1)).and(takesArgument(0,named("zombie.core.network.ByteBufferReader")))))
                .visit(Advice.to(ServerOptions.class).on(named("receiveServerOptions").and(takesArguments(1)).and(takesArgument(0,named("zombie.core.network.ByteBufferReader"))))))
            .type(named("zombie.gameStates.LoadingQueueState"))
            .transform((b,t,l,m,d)->b
                .visit(Advice.to(QueueBegin.class).on(named("enter").and(takesArguments(0))))
                .visit(Advice.to(FrameScope.class).on(named("render").and(takesArguments(0))))
                .visit(Advice.to(QueueUpdate.class).on(named("update").and(takesArguments(0))))
                .visit(Advice.to(QueueRedirect.class).on(named("redirectState").and(takesArguments(0))))
                .visit(Advice.to(QueuePosition.class).on(named("onPlaceInQueue").and(takesArguments(int.class,java.util.HashMap.class)))))
            .type(named("zombie.Lua.LuaEventManager"))
            .transform((b,t,l,m,d)->b.visit(Advice.to(Event.class).on(named("triggerEvent").and(takesArgument(0,String.class)).and(returns(void.class)))))
            .type(named("zombie.ui.UIElement"))
            .transform((b,t,l,m,d)->b
                .visit(Advice.to(ElementDraw.class).on(named("render").and(takesArguments(0)).and(returns(void.class))))
                .visit(Advice.to(ScaledDraw.class).on(named("DrawTextureScaledColor").and(takesArguments(9)).and(takesArgument(0,named("zombie.core.textures.Texture"))).and(takesArgument(1,Double.class)).and(returns(void.class))))
                .visit(Advice.to(ColorDraw.class).on(named("DrawTextureColor").and(takesArguments(7)).and(takesArgument(0,named("zombie.core.textures.Texture"))).and(returns(void.class))))
                .visit(Advice.to(AspectDraw.class).on(named("DrawTextureScaledAspect3").and(takesArguments(9)).and(takesArgument(0,named("zombie.core.textures.Texture"))).and(returns(void.class)))))
            .type(named("zombie.ui.LoadingQueueUI"))
            .transform((b,t,l,m,d)->b.visit(Advice.to(QueueDraw.class).on(named("render").and(takesArguments(0)).and(returns(void.class)))))
            .type(named("zombie.core.Core"))
            .transform((b,t,l,m,d)->b.visit(Advice.to(EndUI.class).on(named("EndFrameUI").and(takesArguments(0)).and(returns(void.class)))))
            .type(named("org.lwjglx.opengl.Display"))
            .transform((b,t,l,m,d)->b.visit(Advice.to(Swap.class).on(named("swapBuffers").and(takesArguments(0)))))
            .type(named("zombie.core.opengl.RenderThread"))
            .transform((b,t,l,m,d)->b.visit(Advice.to(Idle.class).on(named("flushInvokeQueue").and(takesArguments(0)))))
            .installOn(instrumentation);
    }
    public static final class Phase {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(@Advice.Argument(0)String value){StartupRuntime.phase(value);}}
    public static final class Early {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(@Advice.Origin("#m")String method){StartupRuntime.early(method);}}
    public static final class Index {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(){StartupRuntime.early("index");}}
    public static final class Resource {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(@Advice.Argument(0)java.io.File file){StartupRuntime.resource(file);}}
    public static final class Translation {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(@Advice.Argument(0)String key,@Advice.Return String label){StartupRuntime.translation(key,label);}}
    public static final class Mod {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(@Advice.Argument(0)String value){StartupRuntime.mod(value);}}
    public static final class Script {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(@Advice.Argument(0)String value){StartupRuntime.script(value);}}
    public static final class Menu {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(){StartupRuntime.menuReady();}}
    public static final class WorldBegin {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(@Advice.This Object value){StartupRuntime.worldBegin(value);}}
    public static final class WorldEnd {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(){StartupRuntime.worldEnd();}}
    public static final class WorldSent {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(@Advice.Origin Class<?> type){StartupRuntime.worldSent(type);}}
    public static final class WorldFrame {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(@Advice.This Object value){StartupRuntime.worldFrame(value);}}
    public static final class Connect {
        @Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(@Advice.Argument(2)String host,@Advice.Argument(4)String port,@Advice.Argument(6)String friendly){StartupRuntime.serverBegin(friendly,host,port);}
        @Advice.OnMethodExit(onThrowable=Throwable.class,suppress=Throwable.class)public static void exit(@Advice.Thrown Throwable error){if(error!=null)StartupRuntime.handoff(false);}
    }
    public static final class Coop {
        @Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(){StartupRuntime.serverBegin("");}
        @Advice.OnMethodExit(onThrowable=Throwable.class,suppress=Throwable.class)public static void exit(@Advice.This Object client,@Advice.Thrown Throwable error){if(error!=null)StartupRuntime.handoff(false);else StartupRuntime.coopConnected(client);}
    }
    public static final class Disconnect {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(){StartupRuntime.handoff(false);}}
    public static final class Sync {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(){StartupRuntime.sync();}}
    public static final class Request {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(@Advice.Argument(0)Object value){if(value instanceof Enum<?> actual&&actual.name().equals("Loading"))StartupRuntime.sync();}}
    public static final class ConnectionState {
        @Advice.OnMethodEnter(suppress=Throwable.class)public static Object enter(@Advice.This Object state,@Advice.Origin("#m")String method){return StartupRuntime.enterConnection(state,method);}
        @Advice.OnMethodExit(onThrowable=Throwable.class,suppress=Throwable.class)public static void exit(@Advice.This Object state,@Advice.Enter Object prior,@Advice.Thrown Throwable error){StartupRuntime.exitConnection(state,prior,error);}
    }
    public static final class ConnectionCommand {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(@Advice.Argument(0)String command){if("disconnect".equals(command))StartupRuntime.handoff(false);}}
    public static final class ConnectionExit {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(){StartupRuntime.handoff(true);}}
    public static final class QueueBegin {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(){StartupRuntime.queueBegin();}}
    public static final class QueuePosition {
        @Advice.OnMethodEnter(suppress=Throwable.class)public static long enter(){return StartupRuntime.generation();}
        @Advice.OnMethodExit(suppress=Throwable.class)public static void exit(@Advice.Enter long generation,@Advice.Argument(0)int position,@Advice.Argument(1)java.util.HashMap<?,?> info){StartupRuntime.queueInformation(generation,position,info);}
    }
    public static final class ServerTime {
        @Advice.OnMethodEnter(suppress=Throwable.class)public static long enter(){return StartupRuntime.generation();}
        @Advice.OnMethodExit(suppress=Throwable.class)public static void exit(@Advice.Enter long generation,@Advice.This Object state){StartupRuntime.serverTime(state,generation);}
    }
    public static final class ServerOptions {
        @Advice.OnMethodEnter(suppress=Throwable.class)public static long enter(){return StartupRuntime.generation();}
        @Advice.OnMethodExit(suppress=Throwable.class)public static void exit(@Advice.Enter long generation,@Advice.This Object state){StartupRuntime.serverOptions(state,generation);}
    }
    public static final class QueueUpdate {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(@Advice.This Object state){StartupRuntime.queueUpdate(state);}}
    public static final class QueueRedirect {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(@Advice.Return(typing=net.bytebuddy.implementation.bytecode.assign.Assigner.Typing.DYNAMIC)Object next){StartupRuntime.queueRedirect(next);}}
    public static final class FrameScope {
        @Advice.OnMethodEnter(suppress=Throwable.class)public static String enter(@Advice.Origin("#t")String type){return StartupRuntime.enterFrame(type);}
        @Advice.OnMethodExit(onThrowable=Throwable.class,suppress=Throwable.class)public static void exit(@Advice.Enter String prior){StartupRuntime.exitFrame(prior);}
    }
    public static final class Background {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(@Advice.This Object state){StartupRuntime.background(state);}}
    public static final class ElementDraw {@Advice.OnMethodEnter(skipOn=Advice.OnNonDefaultValue.class,suppress=Throwable.class)public static boolean enter(@Advice.This Object element){return StartupRuntime.skipElement(element);}}
    public static final class QueueDraw {@Advice.OnMethodEnter(skipOn=Advice.OnNonDefaultValue.class,suppress=Throwable.class)public static boolean enter(@Advice.This Object element){return StartupRuntime.skipQueue(element);}}
    public static final class ScaledDraw {@Advice.OnMethodEnter(skipOn=Advice.OnNonDefaultValue.class,suppress=Throwable.class)public static boolean enter(@Advice.This Object element,@Advice.Argument(0)Object texture){return StartupRuntime.skipTexture(element,texture,"SCALED");}}
    public static final class ColorDraw {@Advice.OnMethodEnter(skipOn=Advice.OnNonDefaultValue.class,suppress=Throwable.class)public static boolean enter(@Advice.This Object element,@Advice.Argument(0)Object texture){return StartupRuntime.skipTexture(element,texture,"COLOR");}}
    public static final class AspectDraw {@Advice.OnMethodEnter(skipOn=Advice.OnNonDefaultValue.class,suppress=Throwable.class)public static boolean enter(@Advice.This Object element,@Advice.Argument(0)Object texture){return StartupRuntime.skipTexture(element,texture,"ASPECT");}}
    public static final class EndUI {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(@Advice.This Object core){StartupRuntime.endFrameUI(core);}}
    public static final class Event {
        @Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(@Advice.Argument(0)String event,@Advice.Argument(value=1,optional=true)Object a,@Advice.Argument(value=2,optional=true)Object b,@Advice.Argument(value=3,optional=true)Object c,@Advice.Argument(value=4,optional=true)Object d){
            // The name branches precede every payload access. Auth/QR payloads are never read.
            if("OnGoogleAuthRequest".equals(event)||"OnQRReceived".equals(event)||"OnConnected".equals(event)){StartupRuntime.handoff(true);return;}
            if("OnConnectFailed".equals(event)||"OnDisconnect".equals(event)){StartupRuntime.handoff(false);return;}
            if("OnServerWorkshopItems".equals(event)){StartupRuntime.workshopEvent(a,b,c,d);return;}
            if("OnConnectionStateChanged".equals(event)){if(a instanceof String state)StartupRuntime.connectionEvent(state);return;}
            if("OnLoadedTileDefinitions".equals(event)||"OnLoadedMapZones".equals(event)||"OnGameTimeLoaded".equals(event))StartupRuntime.worldEvent(event);
        }
    }
    public static final class Swap {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(){StartupRuntime.beforeSwap();}}
    public static final class Idle {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(){StartupRuntime.idle();}}
}
