package ozl.environment.tests;
import java.nio.file.*;import java.nio.charset.StandardCharsets;import java.util.*;
import zombie.ZomboidFileSystem;import zombie.network.GameClient;
/** Real production transformer; synthetic game boundary and exclusively owned files. */
public final class DiscoveryScopeProbe {
 static int checks;static Path root,pin,extra;static ZomboidFileSystem fs=new ZomboidFileSystem();
 static void check(boolean ok,String why){checks++;if(!ok)throw new AssertionError(why);}
 static void identity(String value){try{Files.write(root.resolve("mod.info"),("id="+value+"\n").getBytes(StandardCharsets.UTF_8));}catch(Exception e){throw new RuntimeException(e);}}
 static List<String> scan(ZomboidFileSystem owner){List<String> r=new ArrayList<String>(Arrays.asList("stale-input"));owner.getAllModFolders(r);return r;}
 static void ordinary(List<String> r,String why){check(r.equals(Arrays.asList(root.toString(),pin.toString())),why+": "+r);}
 static void replaced(List<String> r,String why){check(r.equals(Arrays.asList(pin.toString())),why+": "+r);}
 static void aux(Runnable run){Runnable previous=ZomboidFileSystem.discoveryProbe;ZomboidFileSystem.discoveryProbe=run;try{fs.loadModsAux(new ArrayList<String>(),new ArrayList<String>());}finally{ZomboidFileSystem.discoveryProbe=previous;}}
 public static void main(String[] args)throws Exception{
  root=Paths.get(args[0]);pin=Paths.get(args[1]);extra=Paths.get(args[2]);System.setProperty("ozl.fixture.discovery",root.toString());GameClient.client=true;GameClient.instance.serverMods.add("Ordinary");
  boolean initialGate=(Boolean)Class.forName("ozl.environment.LiveEnvironmentAgent").getMethod("denied",Path.class,String.class).invoke(null,pin.resolve("unused.jar"),"fixture");
  if(args.length>3&&args[3].equals("gate-only")){
   check(initialGate,"normal-menu Java gate denies outside String load");
   aux(()->{try{check((Boolean)Class.forName("ozl.environment.LiveEnvironmentAgent").getMethod("denied",Path.class,String.class).invoke(null,pin.resolve("unused.jar"),"fixture"),"aux cache must not grant normal-menu client Java execution");}catch(Exception e){throw new RuntimeException(e);}});
   ZomboidFileSystem.discoveryProbe=()->{try{check(!(Boolean)Class.forName("ozl.environment.LiveEnvironmentAgent").getMethod("denied",Path.class,String.class).invoke(null,pin.resolve("unused.jar"),"fixture"),"original String client load permission remains scoped");}catch(Exception e){throw new RuntimeException(e);}};fs.loadMods("discovery-cache");ZomboidFileSystem.discoveryProbe=null;
   check((Boolean)Class.forName("ozl.environment.LiveEnvironmentAgent").getMethod("denied",Path.class,String.class).invoke(null,pin.resolve("unused.jar"),"fixture"),"String completion restores denial");System.out.println("PASS "+checks+" normal-menu auxiliary Java gate assertions");return;
  }
  identity("Ordinary");ordinary(scan(fs),"initial uncached selection");
  aux(()->{ordinary(scan(fs),"aux first selection");identity("Pinned");ordinary(scan(fs),"same aux operation must reuse checked metadata instead of scanning all mod.info again");});
  aux(()->replaced(scan(fs),"next aux operation must re-read changed metadata"));
  identity("Ordinary");ZomboidFileSystem.discoveryProbe=()->{ordinary(scan(fs),"String load first");identity("Pinned");ordinary(scan(fs),"String load must reuse discovery");};fs.loadMods("discovery-cache");ZomboidFileSystem.discoveryProbe=null;
  replaced(scan(fs),"outside String load does not reuse completed cache");
  identity("Ordinary");aux(()->{ordinary(scan(fs),"outer first");identity("Pinned");aux(()->replaced(scan(fs),"nested operation gets a fresh index"));ordinary(scan(fs),"nested exit restores outer snapshot");});
  identity("Ordinary");try{aux(()->{ordinary(scan(fs),"exception first");identity("Pinned");throw new IllegalStateException("owned fixture");});throw new AssertionError("expected failure");}catch(IllegalStateException expected){}
  replaced(scan(fs),"exception releases discovery cache");
  identity("Ordinary");aux(()->{ordinary(scan(fs),"before native roots change");System.setProperty("ozl.fixture.discovery",root+"|"+extra);check(scan(fs).equals(Arrays.asList(root.toString(),extra.toString(),pin.toString())),"changed native roots must miss cache");System.setProperty("ozl.fixture.discovery",root.toString());identity("Pinned");GameClient.instance.serverMods.add("Pinned");check(scan(fs).equals(Arrays.asList(root.toString())),"changed server IDs must miss cache");GameClient.instance.serverMods.remove("Pinned");replaced(scan(fs),"server selection change must rebuild");});
  identity("Ordinary");aux(()->{ordinary(scan(fs),"first owner");identity("Pinned");replaced(scan(new ZomboidFileSystem()),"different filesystem owner must not reuse cached roots");});
  identity("Ordinary");aux(()->{ordinary(scan(fs),"before thread change");identity("Pinned");final Throwable[] failure={null};Thread t=new Thread(()->{try{replaced(scan(fs),"different thread must not inherit cache");}catch(Throwable e){failure[0]=e;}});t.start();try{t.join(10000);}catch(InterruptedException e){throw new RuntimeException(e);}check(!t.isAlive()&&failure[0]==null,"thread-local cache isolation: "+failure[0]);});
  identity("Ordinary");aux(()->{ordinary(scan(fs),"before client state changes");GameClient.client=false;check(scan(fs).isEmpty(),"menu state must not reuse client discovery");GameClient.client=true;ordinary(scan(fs),"client state rebuilds after menu");});
  identity("Ordinary");aux(()->{List<String> first=scan(fs);ordinary(first,"before caller mutates list");first.clear();ordinary(scan(fs),"caller mutation cannot alter cached output");identity("Pinned");try{java.lang.reflect.Field ef=Class.forName("ozl.environment.LiveEnvironmentAgent").getDeclaredField("environment");ef.setAccessible(true);Object env=ef.get(null);java.lang.reflect.Field ready=env.getClass().getDeclaredField("clientSelectionReady");ready.setAccessible(true);ready.setBoolean(env,!ready.getBoolean(env));}catch(Exception e){throw new RuntimeException(e);}replaced(scan(fs),"readiness transition must rebuild metadata");});
  check(initialGate==(Boolean)Class.forName("ozl.environment.LiveEnvironmentAgent").getMethod("denied",Path.class,String.class).invoke(null,pin.resolve("unused.jar"),"fixture"),"aux scope must not grant Java execution permission");
  System.out.println("PASS "+checks+" discovery scope assertions (real agent; synthetic game boundary)");
 }
}
