package ozl.environment.tests;
import java.nio.file.*;import java.nio.charset.StandardCharsets;import java.util.*;import zombie.ZomboidFileSystem;import zombie.modding.*;import zombie.gameStates.ChooseGameInfo;
public final class FrozenSelectionProbe {
 static int checks;static void check(boolean ok,String why){checks++;if(!ok)throw new AssertionError(why);}
 public static void main(String[] args)throws Exception{
  Path user=Paths.get(args[0]),original=user.resolve("Saves/Sandbox/Original/mods.txt");ZomboidFileSystem fs=new ZomboidFileSystem();fs.defaultText();
  ActiveMods read=new ActiveMods("save"),current=ActiveMods.getById("currentGame");new ActiveModsFile().read(original.toString(),read);
  check(read.getMods().equals(Arrays.asList("Component","NewMod")),"save validation uses captured selection and component policy");
  current.copyFrom(read);fs.loadMods("currentGame");check(fs.loaded.equals(read.getMods())&&ActiveMods.loaded.getMods().equals(read.getMods()),"copied raw loaded fields use selected snapshot list");
  current.source="mod=Edited";new ActiveModsFile().write(original.toString(),current);new ActiveModsFile().read(original.toString(),read);current.copyFrom(read);
  check(current.getMods().equals(Arrays.asList("Component","Edited")),"confirmation reread preserves session edits");check(new String(Files.readAllBytes(original),StandardCharsets.UTF_8).equals("mod=Unavailable\nmod=Disabled"),"original save metadata remains byte identical");
  ChooseGameInfo.Mod info=new ChooseGameInfo.Mod("copied");ArrayList<String> raw=new ArrayList<String>(Arrays.asList("OldMod","SkippedMod"));info.setRequire(raw);check(info.getRequire().equals(Arrays.asList("NewMod"))&&raw.equals(Arrays.asList("OldMod","SkippedMod")),"local dependency choices use a copy and retain raw metadata");
  zombie.network.GameClient.client=true;zombie.network.GameClient.instance.serverMods.addAll(Arrays.asList("OldMod","SkippedMod","Disabled"));fs.loadMods("client");check(fs.loaded.equals(Arrays.asList("OldMod","SkippedMod","Disabled","Component")),"server list receives components only");check(info.getRequire().equals(raw),"menu to server transition observes original dependency metadata");
  System.out.println("PASS "+checks+" frozen selected-phase assertions");
 }
}
