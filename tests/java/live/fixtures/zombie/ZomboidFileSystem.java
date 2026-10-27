package zombie;
import java.util.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import zombie.core.Core;
import zombie.modding.*;
/** Engine boundary fixture; only file discovery/read-write surfaces are present. */
public final class ZomboidFileSystem {
 public int liveScans;public List<String> loaded=new ArrayList<String>();
 public void loadMods(String id){if(zombie.network.GameClient.client){ArrayList<String> rows=new ArrayList<String>();rows.addAll(zombie.network.GameClient.instance.serverMods);loadMods(rows);return;}ActiveMods active=ActiveMods.getById(id);if(!"default".equalsIgnoreCase(id)){ActiveMods.setLoadedMods(active);loadMods(active.getMods());return;}active=readDefaultModsTxt();ActiveMods.setLoadedMods(active);loadMods(active.getMods());}
 public void loadMods(List<String> ids){loaded=new ArrayList<String>(ids);}

 public synchronized void getAllModFolders(List<String> folders){liveScans++;folders.clear();folders.add(Core.getMyDocumentFolder()+"/mods/ConflictingMod");folders.add(System.getProperty("ozl.fixture.workshop"));}
 private ActiveMods readDefaultModsTxt(){ActiveMods value=ActiveMods.getById("default");new ActiveModsFile().read(Core.getMyDocumentFolder()+java.io.File.separator+"mods"+java.io.File.separator+"default.txt",value);return value;}
 private ArrayList<String> readLoadedDotTxt(){try{return new ArrayList<String>(Files.readAllLines(Paths.get(Core.getMyDocumentFolder(),"mods","loaded.txt"),StandardCharsets.UTF_8));}catch(java.io.IOException e){throw new IllegalStateException(e);}}
 public void saveModsFile(){new java.io.File(Core.getMyDocumentFolder(),"mods").mkdirs();new ActiveModsFile().write(Core.getMyDocumentFolder()+java.io.File.separator+"mods"+java.io.File.separator+"default.txt",ActiveMods.getById("default"));}
 public String defaultText(){return readDefaultModsTxt().source;}
 public List<String> loadedText(){return readLoadedDotTxt();}
 public String getSaveDir(){return Core.getMyDocumentFolder()+java.io.File.separator+"Saves";}
 public String getCacheDir(){return Core.getMyDocumentFolder();}
}
