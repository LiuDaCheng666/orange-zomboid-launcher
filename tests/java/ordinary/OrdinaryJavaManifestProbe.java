package ozl.environment;
import java.nio.file.*;import java.lang.reflect.*;import java.util.*;
/** Loads only the production manifest classes; never initializes game classes. */
public final class OrdinaryJavaManifestProbe {
 public static void main(String[] args)throws Exception{
  boolean live=args[0].equals("live"),expected=Boolean.parseBoolean(args[6]);
  Class<?> type=Class.forName(live?"ozl.environment.LiveEnvironmentManifest":"ozl.environment.EnvironmentManifest");
  Method read=live?type.getDeclaredMethod("read",Path.class,String.class,Path.class):type.getDeclaredMethod("read",Path.class,String.class,Path.class,Path.class);read.setAccessible(true);
  Object manifest=live?read.invoke(null,Paths.get(args[1]),args[2],Paths.get(args[3])):read.invoke(null,Paths.get(args[1]),args[2],Paths.get(args[3]),Paths.get(args[7]));
  Method allows=type.getDeclaredMethod("allows",Path.class,String.class);allows.setAccessible(true);
  if(((Boolean)allows.invoke(manifest,Paths.get(args[4]),args[5]))!=expected)throw new AssertionError("ordinary Java execution selection differs from prepared membership");
  if((Boolean)allows.invoke(manifest,Paths.get(args[4]),"wrong.package"))throw new AssertionError("wrong package authorized");
  if(live&&expected){Method normalize=type.getDeclaredMethod("normalize",List.class);normalize.setAccessible(true);List<String> ids=new ArrayList<String>(Arrays.asList("Lua","OrdinaryJava"));normalize.invoke(manifest,ids);if(!ids.contains("OrdinaryJava")||!ids.contains("ZombieBuddy"))throw new AssertionError("ordinary Java lost during actual live normalization");}
  System.out.println("PASS production "+(live?"live":"frozen")+" JVM manifest path/package/hash gate; allowed="+expected+"; no game classes initialized");
 }
}
