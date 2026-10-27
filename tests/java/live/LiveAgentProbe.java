package ozl.environment.tests;
import java.nio.file.*;import java.nio.charset.StandardCharsets;import java.util.*;import java.lang.reflect.*;import zombie.ZomboidFileSystem;import zombie.modding.ActiveMods;
public final class LiveAgentProbe {
 static int checks;static void check(boolean ok,String s){checks++;if(!ok)throw new AssertionError(s);}static String read(Path p)throws Exception{return new String(Files.readAllBytes(p),StandardCharsets.UTF_8);}
 public static void main(String[] args)throws Exception{
  Path selected=Paths.get(args[0]),lua=Paths.get(args[1]),choices=Paths.get(args[2]),user=Paths.get(args[3]);ZomboidFileSystem fs=new ZomboidFileSystem();List<String> roots=new ArrayList<String>();fs.getAllModFolders(roots);
  boolean vanilla=Boolean.parseBoolean(args[4]);check(roots.equals(vanilla?Collections.emptyList():Arrays.asList(lua.toString(),selected.toString())),"effective current root discovery");check(fs.liveScans==1,"live discovery still runs");check(fs.defaultText().equals("owned-default"),"owned default read");check(fs.loadedText().equals(Arrays.asList("owned-loaded")),"owned loaded read");ActiveMods.getById("default").source="owned-write";fs.saveModsFile();check(read(choices.resolve("mods/default.txt")).equals("owned-write")&&read(user.resolve("mods/default.txt")).equals("original-default"),"native save writes owned files only");check(fs.getCacheDir().equals(user.toString()),"cachedir stays real");
  ActiveMods active=new ActiveMods("save");active.getMods().addAll(Arrays.asList("Lua","Disabled"));check(active.getMods().equals(vanilla?Collections.emptyList():Arrays.asList("Viewpoint","Lua")),"disabled components filtered from saves; vanilla clears all");
  if(!vanilla){Files.write(lua.resolve("mod.info"),"id=Viewpoint\n".getBytes(StandardCharsets.UTF_8));fs.getAllModFolders(roots);check(roots.equals(Arrays.asList(selected.toString())),"current source update rediscovered");}
  System.out.println("PASS "+checks+" native live agent assertions");
 }
}
