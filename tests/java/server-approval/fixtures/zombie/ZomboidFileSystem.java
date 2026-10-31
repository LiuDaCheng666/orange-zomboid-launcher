package zombie;
import java.util.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import zombie.core.Core;
import zombie.modding.*;
/** Game boundary fixture retaining the production bytecode contract and discovery call. */
public final class ZomboidFileSystem {
 public static final ZomboidFileSystem instance=new ZomboidFileSystem();
 public int liveScans;public List<String> loaded=new ArrayList<String>();
 public void loadMods(String id){
  if("diagnostic-nested".equals(id)){getAllModFolders(new ArrayList<String>());zombie.network.GameClient.client=true;try{loadMods("diagnostic-child");}finally{zombie.network.GameClient.client=false;}getAllModFolders(new ArrayList<String>());return;}
  if("diagnostic-child".equals(id)){getAllModFolders(new ArrayList<String>());return;}
  if("diagnostic-exception".equals(id)){getAllModFolders(new ArrayList<String>());throw new IllegalStateException("owned diagnostic failure");}
  if("diagnostic-single".equals(id)){getAllModFolders(new SlowDiscovery());return;}
  if("diagnostic-empty".equals(id))return;
  if("throwBeforeServer".equals(id))throw new IllegalStateException("owned fixture before load");if(zombie.network.GameClient.client){ArrayList<String> rows=new ArrayList<String>();rows.addAll(zombie.network.GameClient.instance.serverMods);loadMods(rows);return;}ActiveMods active=ActiveMods.getById(id);if(!"default".equalsIgnoreCase(id)){ActiveMods.setLoadedMods(active);loadMods(active.getMods());return;}active=readDefaultModsTxt();ActiveMods.setLoadedMods(active);loadMods(active.getMods());}
 /** Only the known diagnostic input iterator is delayed, inside the real filter, to test elapsed accounting without a performance threshold. */
 private static final class SlowDiscovery extends ArrayList<String> {@Override public Iterator<String> iterator(){try{Thread.sleep(20);}catch(InterruptedException failure){throw new IllegalStateException(failure);}return super.iterator();}}
 public void loadMods(List<String> ids){loaded=new ArrayList<String>(ids);}
 public synchronized void getAllModFolders(List<String> folders){liveScans++;folders.clear();String paths=System.getProperty("ozl.fixture.discovery","");if(!paths.isEmpty())folders.addAll(Arrays.asList(paths.split("\\|")));}
 private ActiveMods readDefaultModsTxt(){ActiveMods value=ActiveMods.getById("default");new ActiveModsFile().read(Core.getMyDocumentFolder()+java.io.File.separator+"mods"+java.io.File.separator+"default.txt",value);return value;}
 private ArrayList<String> readLoadedDotTxt(){try{return new ArrayList<String>(Files.readAllLines(Paths.get(Core.getMyDocumentFolder(),"mods","loaded.txt"),StandardCharsets.UTF_8));}catch(java.io.IOException e){throw new IllegalStateException(e);}}
 public void saveModsFile(){new java.io.File(Core.getMyDocumentFolder(),"mods").mkdirs();new ActiveModsFile().write(Core.getMyDocumentFolder()+java.io.File.separator+"mods"+java.io.File.separator+"default.txt",ActiveMods.getById("default"));}
 public String getCacheDir(){return Core.getMyDocumentFolder();}
}
