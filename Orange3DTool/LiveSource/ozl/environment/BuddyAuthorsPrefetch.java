package ozl.environment;
import java.lang.instrument.Instrumentation;import java.lang.reflect.*;import java.nio.file.*;import java.util.*;import java.util.concurrent.*;import java.util.jar.*;import java.io.*;import java.security.ProtectionDomain;
import net.bytebuddy.agent.builder.AgentBuilder;import net.bytebuddy.asm.Advice;import static net.bytebuddy.matcher.ElementMatchers.*;
/** Moves only Buddy's original remote body request earlier. Native verification stays native. */
public final class BuddyAuthorsPrefetch {
 static final String LOADER="me.zed_0xff.zombie_buddy.Loader",AUTHORS="me.zed_0xff.zombie_buddy.KnownAuthors";
 private static final ConcurrentHashMap<Class<?>,Pending> pending=new ConcurrentHashMap<Class<?>,Pending>();
 private static final ThreadLocal<Class<?>> worker=new ThreadLocal<Class<?>>();
 private static final Set<ClassLoader> ready=Collections.newSetFromMap(new ConcurrentHashMap<ClassLoader,Boolean>());
 public static final class Result {public final String body;public final Throwable failure;Result(String value,Throwable error){body=value;failure=error;}}
 private static final class Pending implements Runnable {
  final Class<?> owner;final String configuration;final CompletableFuture<Result> result=new CompletableFuture<Result>();final Thread thread;
  Pending(Class<?> type,String config){owner=type;configuration=config;thread=new Thread(this,"OZL Buddy authors request");thread.setDaemon(true);}
  public void run(){worker.set(owner);try{Method fetch=owner.getDeclaredMethod("fetchRemoteBody");fetch.setAccessible(true);result.complete(new Result((String)fetch.invoke(null),null));}catch(InvocationTargetException error){result.complete(new Result(null,error.getCause()));}catch(Throwable error){result.complete(new Result(null,error));}finally{worker.remove();}}
 }
 static boolean start(Class<?> owner,String configuration){Pending request=new Pending(owner,configuration);if(pending.putIfAbsent(owner,request)!=null)return false;try{request.thread.start();return true;}catch(Throwable error){pending.remove(owner,request);return false;}}
 public static Result consume(Class<?> owner,String configuration){if(worker.get()==owner)return null;Pending request=pending.get(owner);if(request==null||!request.configuration.equals(configuration)||!pending.remove(owner,request))return null;if(Thread.currentThread().isInterrupted()){request.thread.interrupt();return new Result(null,null);}try{return request.result.get();}catch(InterruptedException error){request.thread.interrupt();Thread.currentThread().interrupt();return new Result(null,null);}catch(ExecutionException error){return new Result(null,error.getCause());}}
 private static String configuration(Class<?> owner)throws Exception{Class<?> agent=Class.forName("me.zed_0xff.zombie_buddy.Agent",false,owner.getClassLoader());return ((Path)agent.getMethod("configDir").invoke(null)).toAbsolutePath().normalize().toString();}
 public static void begin(Class<?> loader){try{Class<?> authors=Class.forName(AUTHORS,false,loader.getClassLoader());if(ready.contains(authors.getClassLoader()))start(authors,configuration(authors));}catch(Throwable failure){unavailable(failure);}}
 public static Result requested(Class<?> owner){if(worker.get()==owner)return null;try{return consume(owner,configuration(owner));}catch(Throwable failure){unavailable(failure);return null;}}
 private static void unavailable(Throwable failure){System.err.println("[OZL-LIVE] author request prefetch unavailable; native Buddy request retained ("+failure.getClass().getSimpleName()+")");}
 public static void install(Instrumentation instrumentation,Path buddy){if(buddy==null)return;try{
  final Path expected=buddy.toRealPath();if(!LiveEnvironmentManifest.sha(expected).equals(LiveEnvironmentManifest.BUDDY_HASH))return;
  try(JarFile jar=new JarFile(expected.toFile())){for(String name:Arrays.asList(LOADER,AUTHORS)){JarEntry entry=jar.getJarEntry(name.replace('.','/')+".class");try(InputStream in=jar.getInputStream(entry);ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] block=new byte[8192];int n;while((n=in.read(block))!=-1)out.write(block,0,n);BuddyAuthorsContract.verify(name,out.toByteArray());}}}
  new AgentBuilder.Default().disableClassFormatChanges().with(AgentBuilder.InjectionStrategy.Disabled.INSTANCE).with(AgentBuilder.InitializationStrategy.NoOp.INSTANCE)
   .with(new AgentBuilder.Listener.Adapter(){@Override public void onTransformation(net.bytebuddy.description.type.TypeDescription type,ClassLoader loader,net.bytebuddy.utility.JavaModule module,boolean loaded,net.bytebuddy.dynamic.DynamicType dynamic){if(type.getName().equals(AUTHORS))ready.add(loader);}@Override public void onError(String name,ClassLoader loader,net.bytebuddy.utility.JavaModule module,boolean loaded,Throwable error){ready.remove(loader);unavailable(error);}})
   .type(new AgentBuilder.RawMatcher(){public boolean matches(net.bytebuddy.description.type.TypeDescription type,ClassLoader loader,net.bytebuddy.utility.JavaModule module,Class<?> previous,ProtectionDomain domain){if(!type.getName().equals(LOADER)&&!type.getName().equals(AUTHORS))return false;try{return loader!=null&&domain!=null&&domain.getCodeSource()!=null&&Paths.get(domain.getCodeSource().getLocation().toURI()).toRealPath().equals(expected)&&Class.forName(BuddyAuthorsPrefetch.class.getName(),false,loader)==BuddyAuthorsPrefetch.class;}catch(Exception failure){unavailable(failure);return false;}}}).transform((builder,type,loader,module,domain)->{
    if(type.getName().equals(LOADER))return builder.visit(Advice.to(ConfigReady.class).on(named("initConfig").and(isStatic()).and(takesArguments(0)).and(returns(void.class))));
    return builder.visit(Advice.to(RemoteBody.class).on(named("fetchRemoteBody").and(isStatic()).and(takesArguments(0)).and(returns(String.class))));}).installOn(instrumentation);
 }catch(Throwable failure){unavailable(failure);}}
 public static final class ConfigReady {@Advice.OnMethodExit public static void exit(@Advice.Origin Class<?> loader){BuddyAuthorsPrefetch.begin(loader);}}
 public static final class RemoteBody {@Advice.OnMethodEnter(skipOn=Advice.OnNonDefaultValue.class) public static Result enter(@Advice.Origin Class<?> owner){return BuddyAuthorsPrefetch.requested(owner);}@Advice.OnMethodExit(onThrowable=Throwable.class) public static void exit(@Advice.Enter Result early,@Advice.Return(readOnly=false)String body,@Advice.Thrown(readOnly=false)Throwable error){if(early!=null){body=early.body;error=early.failure;}}}
}
