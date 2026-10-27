package ozl.environment;
import java.lang.instrument.*;import java.nio.file.*;import java.security.ProtectionDomain;import java.util.*;
import net.bytebuddy.agent.builder.AgentBuilder;import net.bytebuddy.asm.*;import static net.bytebuddy.matcher.ElementMatchers.*;
/** Mandatory live isolation; install before optional Buddy/presentation agents. */
public final class LiveEnvironmentAgent {
 private static volatile LiveEnvironmentManifest environment;private static LiveActiveSelection selection;private static final String LOADER="me.zed_0xff.zombie_buddy.Loader";
 public static void premain(String arguments,Instrumentation instrumentation){try{
  LiveEnvironmentManifest accepted=LiveEnvironmentManifest.read(Paths.get(System.getProperty("ozl.live.manifest")),System.getProperty("ozl.live.sha256"),Paths.get(System.getProperty("user.dir")));LiveTargetContract.verifyJar(accepted.game);
  for(Class<?> loaded:instrumentation.getAllLoadedClasses())if(LiveTargetContract.target(loaded.getName().replace('.','/'))||loaded.getName().equals(LOADER))throw new IllegalStateException("required live agent must run first");environment=accepted;selection=new LiveActiveSelection(accepted);
  instrumentation.addTransformer(new ClassFileTransformer(){@Override public byte[] transform(ClassLoader loader,String name,Class<?> previous,ProtectionDomain domain,byte[] bytes){if(LiveTargetContract.target(name)||LOADER.replace('.','/').equals(name))try{guard(loader,domain,LOADER.replace('.','/').equals(name));if(LiveTargetContract.target(name))LiveTargetContract.verify(name,bytes);else BuddyContract.verify(bytes);}catch(Throwable failure){stop("unsupported runtime contract",failure);}return null;}});
  final java.lang.reflect.Method choices=LiveEnvironmentAgent.class.getMethod("choices"),load=LiveEnvironmentAgent.class.getMethod("loadSelected",Object.class,List.class);
  new AgentBuilder.Default().disableClassFormatChanges().with(AgentBuilder.InjectionStrategy.Disabled.INSTANCE).with(AgentBuilder.InitializationStrategy.NoOp.INSTANCE)
   .with(new AgentBuilder.Listener.Adapter(){@Override public void onError(String name,ClassLoader loader,net.bytebuddy.utility.JavaModule module,boolean loaded,Throwable failure){stop("live transformation failed",failure);}})
   .type(namedOneOf("zombie.ZomboidFileSystem","zombie.modding.ActiveMods","zombie.modding.ActiveModsFile",LOADER))
   .transform((builder,type,loader,module,domain)->{
    if(type.getName().equals(LOADER))return builder.visit(Advice.to(ExecutionGate.class).on(namedOneOf("loadJar","addJarToClasspath").and(isStatic()).and(returns(boolean.class))));
    if(type.getName().equals("zombie.modding.ActiveMods"))return builder.visit(Advice.to(Copied.class).on(named("copyFrom").and(takesArguments(1))));
    if(type.getName().equals("zombie.modding.ActiveModsFile"))return builder.visit(Advice.to(ReadSource.class).on(named("read").and(takesArguments(2)))).visit(Advice.to(WriteSource.class).on(named("write").and(takesArguments(2))));
    return builder.visit(Advice.to(BeforeLoad.class).on(named("loadMods").and(takesArguments(String.class))))
      .visit(Advice.to(DefaultRead.class).on(named("readDefaultModsTxt").and(takesArguments(0))))
      .visit(MemberSubstitution.strict().method(named("loadMods").and(isDeclaredBy(named("zombie.ZomboidFileSystem"))).and(takesArguments(List.class)).and(returns(void.class))).replaceWith(load).on(named("loadMods").and(takesArguments(String.class))))
      .visit(Advice.to(Discovery.class).on(named("getAllModFolders").and(takesArguments(List.class)).and(returns(void.class))))
      .visit(MemberSubstitution.strict().method(named("getMyDocumentFolder").and(isDeclaredBy(named("zombie.core.Core"))).and(takesArguments(0)).and(returns(String.class)).and(isStatic())).replaceWith(choices).on(namedOneOf("readDefaultModsTxt","readLoadedDotTxt","saveModsFile")));}).installOn(instrumentation);
  System.out.println("[OZL-LIVE] current sources isolated");
 }catch(Throwable failure){stop("live initialization failed",failure);}}
 private static void guard(ClassLoader loader,ProtectionDomain domain,boolean buddy)throws Exception{if(loader==null||Class.forName(LiveEnvironmentAgent.class.getName(),false,loader)!=LiveEnvironmentAgent.class||domain==null||domain.getCodeSource()==null)throw new IllegalStateException("agent not visible to target loader");Path actual=SafePaths.canonical(Paths.get(domain.getCodeSource().getLocation().toURI()),true,false);Path expected=buddy?environment.buddy:environment.game.resolve("projectzomboid.jar");if(expected==null||!actual.equals(expected))throw new IllegalStateException("foreign runtime source");if(buddy&&!LiveEnvironmentManifest.sha(actual).equals(LiveEnvironmentManifest.BUDDY_HASH))throw new IllegalStateException("Buddy changed");}
 public static void filter(List<String> roots){try{environment.filter(roots);}catch(Throwable failure){stop("current mod discovery failed",failure);}}
 public static String choices(){try{SafePaths.canonical(environment.choices.resolve("mods"),true,true);return environment.choices.toString();}catch(Throwable failure){stop("owned choices missing",failure);return null;}}
 @SuppressWarnings("unchecked") private static List<String> mods(Object active)throws Exception{java.lang.reflect.Field field=active.getClass().getDeclaredField("mods");field.setAccessible(true);return (List<String>)field.get(active);}
 private static String id(Object active)throws Exception{java.lang.reflect.Field field=active.getClass().getDeclaredField("id");field.setAccessible(true);return (String)field.get(active);}
 private static boolean client(ClassLoader loader)throws Exception{Class<?> type=Class.forName("zombie.network.GameClient",false,loader);java.lang.reflect.Field field=type.getField("client");if(field.getType()!=boolean.class||!java.lang.reflect.Modifier.isStatic(field.getModifiers()))throw new IllegalStateException("invalid client phase");return field.getBoolean(null);}
 public static void defaultLoaded(Object active){try{selection.defaultLoaded(mods(active));}catch(Throwable failure){stop("default selection failed",failure);}}
 public static void copied(Object target,Object source,String id,List<String> ids){try{selection.copied(target,source,id,ids,client(target.getClass().getClassLoader()));}catch(Throwable failure){stop("current selection failed",failure);}}
 public static void readSource(String file,Object active){try{selection.readSource(file,active);}catch(Throwable failure){stop("save metadata provenance failed",failure);}}
 public static String writeSource(String file,Object active){try{return selection.writePath(file,active,id(active));}catch(Throwable failure){stop("save metadata destination failed",failure);return file;}}
 public static void beforeLoad(Object filesystem,String name){try{ClassLoader loader=filesystem.getClass().getClassLoader();if(!"currentGame".equals(name)||client(loader))return;Class<?> active=Class.forName("zombie.modding.ActiveMods",false,loader);Object selected=active.getMethod("getById",String.class).invoke(null,name);selection.loading(name,mods(selected),false);}catch(Throwable failure){stop("local selection preparation failed",failure);}}
 public static void loadSelected(Object filesystem,List<String> ids){try{boolean network=client(filesystem.getClass().getClassLoader());if(network)environment.mergeClient(ids);;filesystem.getClass().getMethod("loadMods",List.class).invoke(filesystem,ids);}catch(Throwable failure){stop("selected phase loading failed",failure);}}

 public static boolean denied(Path jar,String pkg){return environment==null||!environment.allows(jar,pkg);}
 private static void stop(String reason,Throwable failure){System.err.println("[OZL-LIVE] isolation failure: "+reason+" ("+failure.getClass().getSimpleName()+": "+failure.getMessage()+")");System.err.flush();Runtime.getRuntime().halt(78);}
 public static final class Discovery {@Advice.OnMethodExit public static void exit(@Advice.Argument(0)List<String> roots){LiveEnvironmentAgent.filter(roots);}}
 public static final class BeforeLoad {@Advice.OnMethodEnter public static void enter(@Advice.This Object filesystem,@Advice.Argument(0) String name){LiveEnvironmentAgent.beforeLoad(filesystem,name);}}
 public static final class DefaultRead {@Advice.OnMethodExit public static void exit(@Advice.Return Object active){LiveEnvironmentAgent.defaultLoaded(active);}}
 public static final class Copied {@Advice.OnMethodExit public static void exit(@Advice.This Object target,@Advice.Argument(0) Object source,@Advice.FieldValue("id") String id,@Advice.FieldValue("mods") ArrayList<String> ids){LiveEnvironmentAgent.copied(target,source,id,ids);}}
 public static final class ReadSource {@Advice.OnMethodEnter public static void enter(@Advice.Argument(0) String file,@Advice.Argument(1) Object active){LiveEnvironmentAgent.readSource(file,active);}}
 public static final class WriteSource {@Advice.OnMethodEnter public static void enter(@Advice.Argument(value=0,readOnly=false) String file,@Advice.Argument(1) Object active){file=LiveEnvironmentAgent.writeSource(file,active);}}
 public static final class ExecutionGate {
  @Advice.OnMethodEnter(skipOn=Advice.OnNonDefaultValue.class) public static boolean enter(@Advice.Argument(0)Path jar,@Advice.Argument(1)String pkg){return LiveEnvironmentAgent.denied(jar,pkg);}
  @Advice.OnMethodExit public static void exit(@Advice.Enter boolean denied,@Advice.Return(readOnly=false)boolean result){if(denied)result=false;}
 }
}
