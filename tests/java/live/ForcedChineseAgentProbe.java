package ozl.environment.tests;
import java.nio.file.*;import java.util.*;import zombie.ZomboidFileSystem;import zombie.modding.ActiveMods;
public final class ForcedChineseAgentProbe {
 static void check(boolean ok,String message){if(!ok)throw new AssertionError(message);}
 @SuppressWarnings("unchecked") static Map<String,Object> cache(Object target,Class<?> type,String name)throws Exception{java.lang.reflect.Field f=type.getDeclaredField(name);f.setAccessible(true);return (Map<String,Object>)f.get(target);}
 public static void main(String[] args)throws Exception{
  String local=args[0],server=args[1];boolean serverSession=!args[2].equals("normal");ZomboidFileSystem fs=new ZomboidFileSystem();List<String> roots=new ArrayList<String>();fs.getAllModFolders(roots);check(roots.equals(Arrays.asList(local)),"menu resolves forced local translation");
  fs.loadMods("default");check(fs.loaded.equals(serverSession?Arrays.asList("B42Trans_CN"):Arrays.asList("Ordinary","B42Trans_CN")),"menu default enables forced translation last");ActiveMods save=new ActiveMods("save");save.getMods().add("SaveOnly");ActiveMods current=ActiveMods.getById("currentGame");current.copyFrom(save);fs.loadMods("currentGame");check(fs.loaded.equals(serverSession?Arrays.asList("B42Trans_CN"):Arrays.asList("SaveOnly","B42Trans_CN")),"save copy and actual loading agree");
  Map<String,Object> dirs=cache(fs,ZomboidFileSystem.class,"modIdToDir"),details=cache(null,zombie.gameStates.ChooseGameInfo.class,"Mods");dirs.put("B42Trans_CN",local);dirs.put("Other","keep");details.put("B42Trans_CN","cached-local");details.put("Other","keep");
  zombie.network.GameClient.client=true;zombie.network.GameClient.instance.serverMods.addAll(Arrays.asList("ServerB","B42Trans_CN","ServerA"));fs.loadModsAux(zombie.network.GameClient.instance.serverMods,new ArrayList<String>());check(!dirs.containsKey("B42Trans_CN")&&!details.containsKey("B42Trans_CN")&&dirs.containsKey("Other")&&details.containsKey("Other"),"transformed dependency boundary clears only Chinese caches");
  fs.getAllModFolders(roots);check(roots.equals(Arrays.asList(server)),"server same-ID root replaces local menu source");fs.loadMods("client");check(fs.loaded.equals(Arrays.asList("ServerB","B42Trans_CN","ServerA")),"existing server order remains exact without duplicate");
  zombie.network.GameClient.instance.serverMods.clear();zombie.network.GameClient.instance.serverMods.addAll(Arrays.asList("ServerB","ServerA"));fs.loadMods("client");check(fs.loaded.equals(Arrays.asList("ServerB","ServerA","B42Trans_CN")),"server missing Chinese appends exactly once");
  System.out.println("PASS transformed forced Chinese menu/save/server activation, same-ID cache/source and exact server order (isolated JVM)");
 }
}
