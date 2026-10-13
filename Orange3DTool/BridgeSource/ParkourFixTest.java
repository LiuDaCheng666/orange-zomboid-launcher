package orange.ads;

import java.io.*;
import java.lang.instrument.*;
import java.lang.reflect.*;
import java.util.*;
import net.bytebuddy.ByteBuddy;
import net.bytebuddy.asm.Advice;
import net.bytebuddy.dynamic.ClassFileLocator;
import net.bytebuddy.dynamic.DynamicType;
import net.bytebuddy.pool.TypePool;
import static net.bytebuddy.jar.asm.Opcodes.*;

public final class ParkourFixTest {
 static final String[] NAMES = {"Slide", "LedgeGrab", "Ladder", "WallRun", "WallClimb"};
 static byte[] adviceBytes;
 static Class<?> adviceClass;
 static final class Loader extends ClassLoader {
  final Map<String,Integer> probes = new HashMap<>();
  final Map<String,byte[]> resources = new HashMap<>();
  final String present, failure;
  final int failureKind;
  Loader(String present, String failure, int kind) { super(ParkourFixTest.class.getClassLoader()); this.present=present; this.failure=failure; failureKind=kind; }
  @Override protected Class<?> loadClass(String name, boolean resolve) throws ClassNotFoundException {
   if (!name.startsWith("com.zomboidparkour.")) return super.loadClass(name, resolve);
   probes.merge(name, 1, Integer::sum);
   if (name.equals("com.zomboidparkour."+failure)) {
    if (failureKind==1) throw new NoClassDefFoundError("missing dependency, not missing requested class");
    if (failureKind==2) throw new SecurityException("denied");
    if (failureKind==3) throw new IllegalStateException("loader unavailable");
    if (failureKind==4) throw new ClassNotFoundException(name, new LinkageError("wrapped dependency failure"));
   }
   if (name.equals("com.zomboidparkour."+present)) {
    Class<?> found=findLoadedClass(name); if(found==null){byte[] b=ParkourTestFixtures.parkour(name); found=defineClass(name,b,0,b.length);} return found;
   }
   throw new ClassNotFoundException(name);
  }
  @Override public InputStream getResourceAsStream(String name) { byte[] bytes=resources.get(name); return bytes==null?super.getResourceAsStream(name):new ByteArrayInputStream(bytes); }
  Class<?> define(String name, byte[] bytes) { resources.put(name.replace('.','/')+".class",bytes); return defineClass(name, bytes, 0, bytes.length); }
 }
 static void check(boolean ok, String label) { if(!ok)throw new AssertionError(label); System.out.println("PASS: "+label); }
 static boolean guard(Loader loader) throws Exception { return (boolean)Class.forName("orange.ads.ParkourAbsentGuard").getMethod("shouldSkip",ClassLoader.class).invoke(null,loader); }
 static void convertBuddyPatch() throws Exception {
  Class<?> patch;
  try { patch=Class.forName("orange.ads.ParkourBusyPatch"); } catch(ClassNotFoundException baseline) { return; }
  // Run the actual installed Buddy annotation converter without a javaagent/redefinition.
  Instrumentation capture=(Instrumentation)Proxy.newProxyInstance(ParkourFixTest.class.getClassLoader(),new Class<?>[]{Instrumentation.class},(proxy,method,args)->{
   if(method.getName().equals("redefineClasses")){adviceBytes=((ClassDefinition[])args[0])[0].getDefinitionClassFile();return null;}
   throw new UnsupportedOperationException(method.getName());
  });
  Class<?> converter=Class.forName("me.zed_0xff.zombie_buddy.PatchTransformer");
  Method method=converter.getDeclaredMethod("transformPatchClass",Class.class,Instrumentation.class,int.class,boolean.class); method.setAccessible(true); method.invoke(null,patch,capture,0,false);
  check(adviceBytes!=null,"actual Buddy converter produced bytecode");
  adviceClass=new Loader(null,null,0).define(patch.getName(),adviceBytes);
  check(adviceClass.getMethod("enter",Method.class,Object[].class).getAnnotation(Advice.OnMethodEnter.class).skipOn()==Advice.OnNonDefaultValue.class,"Buddy converts skipOn=true to non-default skip");
  boolean allArguments=false;
  for(Method entry:adviceClass.getDeclaredMethods())for(var annotations:entry.getParameterAnnotations())for(var annotation:annotations)if(annotation.annotationType()==Advice.AllArguments.class)allArguments=true;
  check(allArguments,"Buddy recognizes wildcard argument binding instead of inferring Origin Method as a target argument");
 }
 static Class<?> target(Loader loader,int access,String descriptor) throws Exception {
  byte[] bytes=ParkourTestFixtures.busy(access,descriptor);
  if(adviceClass!=null) {
   ClassFileLocator locator=new ClassFileLocator.Compound(ClassFileLocator.Simple.of("iseethem.ParkourCompat",bytes),ClassFileLocator.ForClassLoader.of(ParkourFixTest.class.getClassLoader()));
   var type=TypePool.Default.of(locator).describe("iseethem.ParkourCompat").resolve();
   DynamicType.Builder<?> builder=new ByteBuddy().redefine(type,locator);
   // Invoke installed Buddy's real matcher/advice application without scanning or starting the game.
   Method apply=Arrays.stream(Class.forName("me.zed_0xff.zombie_buddy.PatchEngine").getDeclaredMethods()).filter(m->m.getName().equals("lambda$applyPatches$1")).findFirst().orElseThrow();
   apply.setAccessible(true);
   builder=(DynamicType.Builder<?>)apply.invoke(null,Map.of("busy",List.of(adviceClass)),"iseethem.ParkourCompat",Map.of(),builder,type,loader,null,null);
   bytes=builder.make().getBytes();
  }
  return loader.define("iseethem.ParkourCompat",bytes);
 }
 static void regression() throws Exception {
  Loader loader=new Loader(null,null,0); Class<?> t=target(loader,ACC_PUBLIC|ACC_STATIC,"()Z"); t.getField("result").setBoolean(null,true);
  Method busy=t.getMethod("busy"); boolean result=false; for(int i=0;i<200;i++)result=(boolean)busy.invoke(null);
  check(loader.probes.getOrDefault("com.zomboidparkour.Slide",0)<=1,"200 repeated busy calls probe missing Slide at most once (observed "+loader.probes.getOrDefault("com.zomboidparkour.Slide",0)+")");
  check(!result&&t.getField("calls").getInt(null)==0,"all absent: transformed busy skips original and returns false");
  for(String name:NAMES)check(loader.probes.getOrDefault("com.zomboidparkour."+name,0)==1,"all absent probes "+name+" once");
 }
 static void guardBranches() throws Exception {
  for(String name:NAMES){Loader l=new Loader(name,null,0); check(!guard(l)&&!guard(l),"guard preserves each individually present "+name+" without initialization");}
  for(int kind=1;kind<=4;kind++){Loader l=new Loader(null,"WallClimb",kind);check(!guard(l)&&!guard(l),"guard preserves non-missing failure kind "+kind);}
  Loader a=new Loader(null,null,0),b=new Loader(null,null,0),c=new Loader("WallClimb",null,0);
  check(guard(a)&&guard(a)&&guard(b)&&!guard(c),"independent/new classloaders re-evaluate absence and presence");
  check(a.probes.values().stream().mapToInt(Integer::intValue).sum()==5&&b.probes.values().stream().mapToInt(Integer::intValue).sum()==5,"absence cached once per loader");
 }
 static void preserveOriginal() throws Exception {
  for(String name:NAMES)for(boolean value:new boolean[]{false,true}){
   Class<?> t=target(new Loader(name,null,0),ACC_PUBLIC|ACC_STATIC,"()Z");t.getField("result").setBoolean(null,value);
   boolean actual=(boolean)t.getMethod("busy").invoke(null);check(actual==value&&t.getField("calls").getInt(null)==1,"present "+name+": transformed original preserves "+value);
  }
  for(int kind=1;kind<=4;kind++){Class<?> t=target(new Loader(null,"WallClimb",kind),ACC_PUBLIC|ACC_STATIC,"()Z");t.getField("result").setBoolean(null,true);check((boolean)t.getMethod("busy").invoke(null)&&t.getField("calls").getInt(null)==1,"transformed original runs on non-missing failure "+kind);}
 }
 static void unsupported() throws Exception {
  int[] access={ACC_PUBLIC|ACC_STATIC,ACC_PUBLIC,ACC_PRIVATE|ACC_STATIC,ACC_PUBLIC|ACC_STATIC}; String[] descriptors={"(I)Z","()Z","()Z","()I"};
  for(int i=0;i<access.length;i++){
   Loader l=new Loader(null,null,0);Class<?> t=target(l,access[i],descriptors[i]);t.getField("result").setBoolean(null,true);
   Method m=t.getDeclaredMethod("busy",i==0?new Class<?>[]{int.class}:new Class<?>[0]);m.setAccessible(true);Object receiver=i==1?t.getConstructor().newInstance():null;
   Object result=m.invoke(receiver,i==0?new Object[]{123}:new Object[0]);check(result.equals(i==3?Integer.valueOf(7):Boolean.TRUE)&&t.getField("calls").getInt(null)==1,"unsupported signature "+descriptors[i]+" access "+access[i]+" retains body and result");
   check(l.probes.size()==1,"unsupported signature does not run five-class guard");
  }
 }
 public static void main(String[] args)throws Exception { convertBuddyPatch(); regression(); guardBranches(); preserveOriginal(); unsupported(); System.out.println("PARKOUR FIX TESTS PASS"); }
}
