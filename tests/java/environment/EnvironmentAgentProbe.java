package ozl.environment.tests;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import zombie.ZomboidFileSystem;
import zombie.modding.ActiveMods;

public final class EnvironmentAgentProbe {
 static int checks;static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
 static String read(Path path)throws Exception{return new String(Files.readAllBytes(path),StandardCharsets.UTF_8);}
 public static void main(String[] args)throws Exception{
  String mode=args[0];Path user=Paths.get(args[1]),first=Paths.get(args[2]),second=Paths.get(args[3]),mods=Paths.get(args[4]);boolean bridge=Boolean.parseBoolean(args[5]);
  if(mode.equals("delete-root")){Files.move(second,second.resolveSibling("removed-root"));new ZomboidFileSystem().getAllModFolders(new ArrayList<String>());throw new AssertionError("missing frozen root fell through");}
  if(mode.equals("foreign-bridge")){Path folder=Files.createDirectories(user.resolve("mods/OrangeLauncherSession"));Files.write(folder.resolve("mod.info"),"id=ForeignMod\n".getBytes(StandardCharsets.UTF_8));new ZomboidFileSystem().getAllModFolders(new ArrayList<String>());throw new AssertionError("foreign bridge accepted");}
  if(mode.equals("null-output")){new ZomboidFileSystem().getAllModFolders(null);throw new AssertionError("invalid output ignored");}
  if(mode.equals("delete-settings")){Files.move(mods,mods.resolveSibling("removed-settings"));new ZomboidFileSystem().defaultText();throw new AssertionError("missing frozen mod settings fell through");}
  ZomboidFileSystem fs=new ZomboidFileSystem();List<String> folders=new ArrayList<String>(Arrays.asList("untrusted cached root"));fs.getAllModFolders(folders);
  List<String> expected=new ArrayList<String>(Arrays.asList(first.toString(),second.toString()));if(bridge)expected.add(user.resolve("mods/OrangeLauncherSession").toString());
  check(folders.equals(expected),"native discovery clears and returns only full frozen roots in captured order plus owned bridge");
  check(fs.liveScans==0,"discovery never executes Steam/Workshop/local scanning");
  check(java.lang.reflect.Modifier.isSynchronized(ZomboidFileSystem.class.getMethod("getAllModFolders",List.class).getModifiers()),"instrumentation retains native synchronized discovery semantics");
  check(read(second.resolve("media/frozen.txt")).equals("original-flat-bytes"),"B41 flat mod contents remain copied");
  check(read(first.resolve("common/media/frozen.txt")).equals("original-common-bytes")&&read(first.resolve("42/media/frozen.txt")).equals("original-versioned-bytes"),"B42 common and versioned trees remain attached to their full copied root");
  Files.write(user.resolve("mods/ConflictingMod/mod.info"),"id=FrozenFlat\nname=Updated live replacement\n".getBytes(StandardCharsets.UTF_8));Files.write(user.resolve("mods/ConflictingMod/media/frozen.txt"),"Steam updated this live mod".getBytes(StandardCharsets.UTF_8));
  fs.getAllModFolders(folders);check(folders.equals(expected)&&read(second.resolve("media/frozen.txt")).equals("original-flat-bytes"),"later live updates never substitute a copied mod with the same ID");
  check(fs.defaultText().equals("frozen-default"),"native default parser receives snapshot runtime default.txt");
  check(fs.loadedText().equals(Arrays.asList("frozen-loaded")),"legacy loaded.txt reads snapshot runtime directory");
  Files.write(user.resolve("mods/default.txt"),"live-default-updated".getBytes(StandardCharsets.UTF_8));check(fs.defaultText().equals("frozen-default"),"live global mod-list changes cannot leak into frozen boot");
  ActiveMods.getById("default").source="saved-frozen";fs.saveModsFile();check(read(mods.resolve("default.txt")).equals("saved-frozen")&&read(user.resolve("mods/default.txt")).equals("live-default-updated"),"native global list writes stay in frozen runtime directory");
  check(fs.getSaveDir().equals(user.resolve("Saves").toString())&&fs.getCacheDir().equals(user.toString()),"save path and explicit original cachedir remain live");
  boolean noPresentation=false;try{Class.forName("ozl.startup.Agent");}catch(ClassNotFoundException expectedMissing){noPresentation=true;}check(noPresentation,"mandatory isolation works without optional loading presentation classes");
  System.out.println("PASS "+checks+" mandatory frozen discovery/default/save-path checks");
 }
}
