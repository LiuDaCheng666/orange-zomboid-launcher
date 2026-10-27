package ozl.environment;
import java.lang.instrument.*;import java.nio.file.*;import java.security.ProtectionDomain;import java.util.*;
import net.bytebuddy.agent.builder.AgentBuilder;import net.bytebuddy.asm.*;import static net.bytebuddy.matcher.ElementMatchers.*;
/** Mandatory live isolation; install before optional Buddy/presentation agents. */
public final class LiveEnvironmentAgent {
 private static volatile LiveEnvironmentManifest environment;private static final String LOADER="me.zed_0xff.zombie_buddy.Loader";
 public static void premain(String arguments,Instrumentation instrumentation){try{
  LiveEnvironmentManifest accepted=LiveEnvironmentManifest.read(Paths.get(System.getProperty("ozl.live.manifest")),System.getProperty("ozl.live.sha256"),Paths.get(System.getProperty("user.dir")));LiveTargetContract.verifyJar(accepted.game);
  for(Class<?> loaded:instrumentation.getAllLoadedClasses())if(LiveTargetContract.target(loaded.getName().replace('.','/'))||loaded.getName().equals(LOADER))throw new IllegalStateException("required live agent must run first");environment=accepted;ActiveSelectionHooks.configure(accepted.game,accepted.user,accepted.choices,accepted.override,(ids,ordinary)->accepted.normalize(ids,ordinary),ids->accepted.mergeClient(ids));
  instrumentation.addTransformer(new ClassFileTransformer(){@Override public byte[] transform(ClassLoader loader,String name,Class<?> previous,ProtectionDomain domain,byte[] bytes){if(LiveTargetContract.target(name)||LOADER.replace('.','/').equals(name))try{guard(loader,domain,LOADER.replace('.','/').equals(name));if(LiveTargetContract.target(name))LiveTargetContract.verify(name,bytes);else BuddyContract.verify(bytes);}catch(Throwable failure){stop("unsupported runtime contract",failure);}return null;}});
  final java.lang.reflect.Method choices=LiveEnvironmentAgent.class.getMethod("choices");
  new AgentBuilder.Default().disableClassFormatChanges().with(AgentBuilder.InjectionStrategy.Disabled.INSTANCE).with(AgentBuilder.InitializationStrategy.NoOp.INSTANCE)
   .with(new AgentBuilder.Listener.Adapter(){@Override public void onError(String name,ClassLoader loader,net.bytebuddy.utility.JavaModule module,boolean loaded,Throwable failure){stop("live transformation failed",failure);}})
   .type(namedOneOf("zombie.ZomboidFileSystem","zombie.modding.ActiveMods","zombie.modding.ActiveModsFile",LOADER))
   .transform((builder,type,loader,module,domain)->{
    if(type.getName().equals(LOADER))return builder.visit(Advice.to(ExecutionGate.class).on(namedOneOf("loadJar","addJarToClasspath").and(isStatic()).and(returns(boolean.class))));
    builder=ActiveSelectionHooks.apply(builder,type.getName());
    if(!type.getName().equals("zombie.ZomboidFileSystem"))return builder;
    return builder      .visit(Advice.to(Discovery.class).on(named("getAllModFolders").and(takesArguments(List.class)).and(returns(void.class))))
      .visit(MemberSubstitution.strict().method(named("getMyDocumentFolder").and(isDeclaredBy(named("zombie.core.Core"))).and(takesArguments(0)).and(returns(String.class)).and(isStatic())).replaceWith(choices).on(namedOneOf("readDefaultModsTxt","readLoadedDotTxt","saveModsFile")));}).installOn(instrumentation);
  System.out.println("[OZL-LIVE] current sources isolated");
 }catch(Throwable failure){stop("live initialization failed",failure);}}
 private static void guard(ClassLoader loader,ProtectionDomain domain,boolean buddy)throws Exception{if(loader==null||Class.forName(LiveEnvironmentAgent.class.getName(),false,loader)!=LiveEnvironmentAgent.class||domain==null||domain.getCodeSource()==null)throw new IllegalStateException("agent not visible to target loader");Path actual=SafePaths.canonical(Paths.get(domain.getCodeSource().getLocation().toURI()),true,false);Path expected=buddy?environment.buddy:environment.game.resolve("projectzomboid.jar");if(expected==null||!actual.equals(expected))throw new IllegalStateException("foreign runtime source");if(buddy&&!LiveEnvironmentManifest.sha(actual).equals(LiveEnvironmentManifest.BUDDY_HASH))throw new IllegalStateException("Buddy changed");}
 public static void filter(List<String> roots){try{environment.filter(roots);}catch(Throwable failure){stop("current mod discovery failed",failure);}}
 public static String choices(){try{SafePaths.canonical(environment.choices.resolve("mods"),true,true);return environment.choices.toString();}catch(Throwable failure){stop("owned choices missing",failure);return null;}}
 public static boolean denied(Path jar,String pkg){return environment==null||!environment.allows(jar,pkg);}
 private static void stop(String reason,Throwable failure){System.err.println("[OZL-LIVE] isolation failure: "+reason+" ("+failure.getClass().getSimpleName()+": "+failure.getMessage()+")");System.err.flush();Runtime.getRuntime().halt(78);}
 public static final class Discovery {@Advice.OnMethodExit public static void exit(@Advice.Argument(0)List<String> roots){LiveEnvironmentAgent.filter(roots);}}
 public static final class ExecutionGate {
  @Advice.OnMethodEnter(skipOn=Advice.OnNonDefaultValue.class) public static boolean enter(@Advice.Argument(0)Path jar,@Advice.Argument(1)String pkg){return LiveEnvironmentAgent.denied(jar,pkg);}
  @Advice.OnMethodExit public static void exit(@Advice.Enter boolean denied,@Advice.Return(readOnly=false)boolean result){if(denied)result=false;}
 }
}
