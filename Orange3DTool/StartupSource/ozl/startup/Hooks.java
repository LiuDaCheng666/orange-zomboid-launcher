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
            .transform((builder,type,loader,module,domain)->builder.visit(Advice.to(Phase.class).on(named("DoLoadingText").and(takesArguments(String.class)))))
            .type(named("zombie.ZomboidFileSystem"))
            .transform((builder,type,loader,module,domain)->builder.visit(Advice.to(Mod.class).on(named("loadMod").and(takesArguments(String.class)))))
            .type(named("zombie.Lua.LuaManager"))
            .transform((builder,type,loader,module,domain)->builder.visit(Advice.to(Script.class).on(named("RunLua").and(takesArgument(0,String.class)))))
            .type(named("zombie.gameStates.MainScreenState"))
            .transform((builder,type,loader,module,domain)->builder.visit(Advice.to(Menu.class).on(named("enter").and(takesArguments(0)))))
            .type(named("zombie.gameStates.GameLoadingState"))
            .transform((builder,type,loader,module,domain)->builder
                .visit(Advice.to(WorldBegin.class).on(named("enter").and(takesArguments(0))))
                .visit(Advice.to(WorldEnd.class).on(named("exit").and(takesArguments(0))))
                .visit(Advice.to(WorldFrame.class).on(named("render").and(takesArguments(0)))))
            .type(named("org.lwjglx.opengl.Display"))
            .transform((builder,type,loader,module,domain)->builder.visit(Advice.to(Swap.class).on(named("swapBuffers").and(takesArguments(0)))))
            .type(named("zombie.core.opengl.RenderThread"))
            .transform((builder,type,loader,module,domain)->builder.visit(Advice.to(Idle.class).on(named("flushInvokeQueue").and(takesArguments(0)))))
            .installOn(instrumentation);
    }
    public static final class Phase {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(@Advice.Argument(0)String value){StartupRuntime.phase(value);}}
    public static final class Mod {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(@Advice.Argument(0)String value){StartupRuntime.mod(value);}}
    public static final class Script {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(@Advice.Argument(0)String value){StartupRuntime.script(value);}}
    public static final class Menu {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(){StartupRuntime.menuReady();}}
    public static final class WorldBegin {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(){StartupRuntime.worldBegin();}}
    public static final class WorldEnd {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(){StartupRuntime.worldEnd();}}
    public static final class WorldFrame {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(@Advice.This Object value){StartupRuntime.worldFrame(value);}}
    public static final class Swap {@Advice.OnMethodEnter(suppress=Throwable.class)public static void enter(){StartupRuntime.beforeSwap();}}
    public static final class Idle {@Advice.OnMethodExit(suppress=Throwable.class)public static void exit(){StartupRuntime.idle();}}
}
