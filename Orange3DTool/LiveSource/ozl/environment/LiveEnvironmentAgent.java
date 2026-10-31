package ozl.environment;
import java.lang.instrument.*;import java.nio.file.*;import java.security.ProtectionDomain;import java.util.*;
import net.bytebuddy.agent.builder.AgentBuilder;import net.bytebuddy.asm.*;import static net.bytebuddy.matcher.ElementMatchers.*;
/** Mandatory live isolation; install before optional Buddy/presentation agents. */
public final class LiveEnvironmentAgent {
 private static volatile LiveEnvironmentManifest environment;private static final ThreadLocal<DiscoveryScope> discoveryScope=new ThreadLocal<DiscoveryScope>();private static final ThreadLocal<LoadScope> clientLoad=new ThreadLocal<LoadScope>();private static final String LOADER="me.zed_0xff.zombie_buddy.Loader";
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
    return builder.visit(Advice.to(ClientLoad.class).on(named("loadMods").and(takesArguments(String.class)).and(returns(void.class))))
      .visit(Advice.to(DependencyDiscovery.class).on(named("loadModsAux").and(takesArguments(List.class,List.class)).and(returns(String.class))))
      .visit(Advice.to(Discovery.class).on(named("getAllModFolders").and(takesArguments(List.class)).and(returns(void.class))))
      .visit(MemberSubstitution.strict().method(named("getMyDocumentFolder").and(isDeclaredBy(named("zombie.core.Core"))).and(takesArguments(0)).and(returns(String.class)).and(isStatic())).replaceWith(choices).on(namedOneOf("readDefaultModsTxt","readLoadedDotTxt","saveModsFile")));}).installOn(instrumentation);
  System.out.println("[OZL-LIVE] current sources isolated");
 }catch(Throwable failure){stop("live initialization failed",failure);}}
 private static void guard(ClassLoader loader,ProtectionDomain domain,boolean buddy)throws Exception{if(loader==null||Class.forName(LiveEnvironmentAgent.class.getName(),false,loader)!=LiveEnvironmentAgent.class||domain==null||domain.getCodeSource()==null)throw new IllegalStateException("agent not visible to target loader");Path actual=SafePaths.canonical(Paths.get(domain.getCodeSource().getLocation().toURI()),true,false);Path expected=buddy?environment.buddy:environment.game.resolve("projectzomboid.jar");if(expected==null||!actual.equals(expected))throw new IllegalStateException("foreign runtime source");if(buddy&&!LiveEnvironmentManifest.sha(actual).equals(LiveEnvironmentManifest.BUDDY_HASH))throw new IllegalStateException("Buddy changed");}
 public static void filter(List<String> roots){filter(roots,null);}
 public static void filter(List<String> roots,Object filesystem){LoadScope scope=clientLoad.get();long started=scope==null?0:System.nanoTime();try{ClassLoader loader=ClassLoader.getSystemClassLoader();boolean client=ActiveSelectionHooks.client(loader);Set<String> serverIds=new HashSet<String>();if(client){Class<?> type=Class.forName("zombie.network.GameClient",false,loader);Object owner=type.getField("instance").get(null);Object values=type.getField("serverMods").get(owner);if(!(values instanceof List))throw new IllegalStateException("invalid server selection");for(Object value:(List<?>)values){if(!(value instanceof String))throw new IllegalStateException("invalid server identity");serverIds.add(LiveEnvironmentManifest.id((String)value));}}DiscoveryScope discovery=discoveryScope.get();if(discovery!=null&&discovery.matches(filesystem,roots,client,serverIds,environment.clientSelectionReady)){roots.clear();roots.addAll(discovery.output);}else{List<String> input=discovery==null?null:new ArrayList<String>(roots);environment.filter(roots,client,serverIds);if(discovery!=null)discovery.remember(filesystem,input,roots,client,serverIds,environment.clientSelectionReady);}}catch(Throwable failure){stop("current mod discovery failed",failure);}finally{if(scope!=null){scope.discoveryCalls++;scope.discoveryNanos+=System.nanoTime()-started;}}}
 public static String choices(){try{SafePaths.canonical(environment.choices.resolve("mods"),true,true);return environment.choices.toString();}catch(Throwable failure){stop("owned choices missing",failure);return null;}}
 // The native client branch is scoped to this verified load call, never a permanent permission.
 // Reuse only within one synchronous native operation. Never retain source metadata across loads.
 private static final class DiscoveryScope {
  Object owner;List<String> input,output;Set<String> serverIds;boolean client,ready;
  boolean matches(Object owner,List<String> roots,boolean client,Set<String> ids,boolean ready){return output!=null&&this.owner==owner&&this.client==client&&this.ready==ready&&serverIds.equals(ids)&&input.equals(roots);}
  void remember(Object owner,List<String> input,List<String> roots,boolean client,Set<String> ids,boolean ready){this.owner=owner;this.input=input;this.output=new ArrayList<String>(roots);this.client=client;this.ready=ready;this.serverIds=new HashSet<String>(ids);}
 }
 public static Object beginDiscovery(){DiscoveryScope previous=discoveryScope.get();discoveryScope.set(new DiscoveryScope());return previous;}
 public static void endDiscovery(Object previous){if(previous==null)discoveryScope.remove();else discoveryScope.set((DiscoveryScope)previous);}
 private static final class LoadScope {final Object previousDiscovery;final boolean client;final long started;int discoveryCalls;long discoveryNanos;LoadScope(boolean client,long started){this.client=client;this.started=started;this.previousDiscovery=beginDiscovery();}}
 public static Object beginClientLoad(ClassLoader loader){try{long started=System.nanoTime();LoadScope previous=clientLoad.get();clientLoad.set(new LoadScope(ActiveSelectionHooks.client(loader),started));return previous;}catch(Throwable failure){stop("current loading phase failed",failure);return null;}}
 public static void endClientLoad(Object previous){LoadScope scope=clientLoad.get();if(scope!=null)endDiscovery(scope.previousDiscovery);if(previous==null)clientLoad.remove();else clientLoad.set((LoadScope)previous);if(scope!=null)System.out.println("[OZL-LIVE] load-summary totalMs="+((System.nanoTime()-scope.started)/1000000L)+" discoveryCalls="+scope.discoveryCalls+" discoveryMs="+(scope.discoveryNanos/1000000L)+" client="+scope.client);}
 public static boolean denied(Path jar,String pkg){LoadScope scope=clientLoad.get();return environment==null||!environment.usesBuddyApproval()&&!(scope!=null&&scope.client&&!environment.vanilla)&&!environment.allows(jar,pkg);}
 private static void stop(String reason,Throwable failure){System.err.println("[OZL-LIVE] isolation failure: "+reason+" ("+failure.getClass().getSimpleName()+": "+failure.getMessage()+")");System.err.flush();Runtime.getRuntime().halt(78);}
 public static final class ClientLoad {
  @Advice.OnMethodEnter public static Object enter(@Advice.This Object filesystem){return LiveEnvironmentAgent.beginClientLoad(filesystem.getClass().getClassLoader());}
  @Advice.OnMethodExit(onThrowable=Throwable.class) public static void exit(@Advice.Enter Object previous){LiveEnvironmentAgent.endClientLoad(previous);}
 }
 public static final class DependencyDiscovery {
  @Advice.OnMethodEnter public static Object enter(){return LiveEnvironmentAgent.beginDiscovery();}
  @Advice.OnMethodExit(onThrowable=Throwable.class) public static void exit(@Advice.Enter Object previous){LiveEnvironmentAgent.endDiscovery(previous);}
 }
 public static final class Discovery {@Advice.OnMethodExit public static void exit(@Advice.This Object filesystem,@Advice.Argument(0)List<String> roots){LiveEnvironmentAgent.filter(roots,filesystem);}}
 public static final class ExecutionGate {
  @Advice.OnMethodEnter(skipOn=Advice.OnNonDefaultValue.class) public static boolean enter(@Advice.Argument(0)Path jar,@Advice.Argument(1)String pkg){return LiveEnvironmentAgent.denied(jar,pkg);}
  @Advice.OnMethodExit public static void exit(@Advice.Enter boolean denied,@Advice.Return(readOnly=false)boolean result){if(denied)result=false;}
 }
}
