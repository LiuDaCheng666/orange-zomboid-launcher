package ozl.environment.tests;
import java.nio.file.*;import java.nio.charset.StandardCharsets;import java.util.*;import zombie.ZomboidFileSystem;import zombie.modding.*;
public final class FrozenCapturePersistenceProbe {
 public static void main(String[] args)throws Exception{
  Path snapshot=Paths.get(args[0]),user=Paths.get(args[1]),save=user.resolve("Saves/Sandbox/Source/mods.txt");byte[] original=Files.readAllBytes(save);List<String> expected=Arrays.asList(args[2].split(","));ZomboidFileSystem fs=new ZomboidFileSystem();fs.loadMods("default");ActiveMods defaults=ActiveMods.getById("default");if(!fs.loaded.equals(expected))throw new AssertionError("captured default mismatch "+fs.loaded+" expected "+expected);
  ActiveMods read=new ActiveMods("save");new ActiveModsFile().read(save.toString(),read);ActiveMods.getById("currentGame").copyFrom(read);fs.loadMods("currentGame");if(!fs.loaded.equals(expected))throw new AssertionError("captured save mismatch "+fs.loaded);
  StringBuilder text=new StringBuilder("mods\n{\n");for(String id:defaults.getMods())text.append(" mod = ").append(id).append(",\n");text.append("}\nmaps\n{\n}\n");defaults.source=text.toString();fs.saveModsFile();Files.write(snapshot.resolve("runtime/mods/loaded.txt"),defaults.getMods(),StandardCharsets.UTF_8);
  zombie.network.GameClient.client=true;zombie.network.GameClient.instance.serverMods.add("ServerOnly");fs.loadMods("client");List<String> serverExpected=new ArrayList<String>();serverExpected.add("ServerOnly");serverExpected.addAll(args.length>3?(args[3].length()==0?Collections.<String>emptyList():Arrays.asList(args[3].split(","))):Arrays.asList("NewComponent"));if(!fs.loaded.equals(serverExpected))throw new AssertionError("skipped/replaced component leaked into server "+fs.loaded);
  if(!Arrays.equals(original,Files.readAllBytes(save)))throw new AssertionError("source save changed");System.out.println("PASS captured JVM default/save/persistence/server/source identity");
 }
}
