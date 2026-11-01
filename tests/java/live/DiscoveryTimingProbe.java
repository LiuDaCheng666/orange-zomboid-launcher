package ozl.environment;
import java.nio.file.*;import java.nio.charset.*;import java.util.*;import java.util.Base64;
/** Real read-only production filtering over an exclusively owned mod tree; no game entry. */
public final class DiscoveryTimingProbe {
 static String record(String key,String value){return key+"\t"+Base64.getEncoder().encodeToString(value.getBytes(StandardCharsets.UTF_8))+"\n";}
 public static void main(String[] args)throws Exception{
  Path base=Paths.get(args[0]).toAbsolutePath(),game=base.resolve("game"),user=base.resolve("user"),context=base.resolve("context"),choices=context.resolve("session");Files.createDirectories(game);Files.createDirectories(user);Files.createDirectories(choices.resolve("mods"));
  for(String name:Arrays.asList("default.txt","loaded.txt"))Files.write(choices.resolve("mods").resolve(name),new byte[0]);
  String text="OZLLIVE1\n"+record("game",game.toString())+record("user",user.toString())+record("context",context.toString())+record("choices",choices.toString())+record("vanilla","false");Path manifest=context.resolve("live.manifest");Files.write(manifest,text.getBytes(StandardCharsets.UTF_8));
  List<String> roots=new ArrayList<String>();for(int i=0;i<470;i++){Path root=base.resolve("workshop/content/108600/"+(3000000000L+i)+"/mods/Mod"+i);Files.createDirectories(root.resolve("42.21"));Files.write(root.resolve("42.21/mod.info"),("id=OwnedBenchmark"+i+"\n").getBytes(StandardCharsets.UTF_8));roots.add(root.toString());}
  LiveEnvironmentManifest env=LiveEnvironmentManifest.read(manifest,LiveEnvironmentManifest.sha(manifest),game);
  for(int i=0;i<3;i++){List<String> copy=new ArrayList<String>(roots);long start=System.nanoTime();env.filter(copy,false,Collections.emptySet());long elapsed=(System.nanoTime()-start)/1000000; if(!copy.equals(roots))throw new AssertionError("discovery changed identities/order");System.out.println("FILTER pass="+i+" roots="+roots.size()+" millis="+elapsed);}
 }
}
