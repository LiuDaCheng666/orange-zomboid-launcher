package orange.ads;
import java.lang.instrument.Instrumentation;
import net.bytebuddy.agent.builder.AgentBuilder;
import net.bytebuddy.asm.Advice;
import static net.bytebuddy.matcher.ElementMatchers.*;
public final class Hook {
 public static void install(Instrumentation inst){new AgentBuilder.Default().disableClassFormatChanges().with(AgentBuilder.Listener.StreamWriting.toSystemOut().withTransformationsOnly()).type(named("zombie.gameStates.IngameState")).transform((builder,type,loader,module,domain)->builder.visit(Advice.to(AfterUpdate.class).on(named("update").and(takesArguments(0))))).type(named("zombie.ui.UIManager")).transform((builder,type,loader,module,domain)->builder.visit(Advice.to(AfterRender.class).on(named("render").and(takesArguments(0))))).installOn(inst);}
 public static final class AfterRender { @Advice.OnMethodExit(suppress=Throwable.class) public static void exit(){RuntimeBridge.renderNotice();} }
 public static final class AfterUpdate {
  @Advice.OnMethodExit(suppress=Throwable.class) public static void exit(){RuntimeBridge.afterUpdate();}
 }
}
