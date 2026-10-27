package ozl.environment;
import java.io.*;import java.nio.file.*;import java.nio.charset.StandardCharsets;import java.security.MessageDigest;import java.util.*;
/** Applies choices after parsing and before currentGame raw-field bookkeeping. */
final class LiveActiveSelection {
 private final LiveEnvironmentManifest environment;
 private List<String> defaults;
 private final Map<Object,Path> sourceFiles=new WeakHashMap<Object,Path>();
 LiveActiveSelection(LiveEnvironmentManifest environment){this.environment=environment;}
 synchronized void defaultLoaded(List<String> ids){environment.normalize(ids,null);defaults=ids;}
 synchronized void copied(Object target,Object source,String id,List<String> ids,boolean client){
  if(client||!"currentGame".equals(id))return;
  if(environment.override&&defaults==null)throw new IllegalStateException("owned default has not completed reading");
  environment.normalize(ids,environment.override?new ArrayList<String>(defaults):null);
  Path original=sourceFiles.get(source);if(original==null)sourceFiles.remove(target);else sourceFiles.put(target,original);
 }
 synchronized void loading(String id,List<String> ids,boolean client){if(!client&&"currentGame".equals(id))environment.normalize(ids,null);}
 private Path saveMetadata(String value)throws IOException{
  Path path=Paths.get(value).toAbsolutePath().normalize(),root=environment.user.resolve("Saves");
  if(!path.startsWith(root)||root.relativize(path).getNameCount()!=3||!path.getFileName().toString().equals("mods.txt"))return null;
  SafePaths.canonical(path.getParent(),true,true);if(Files.exists(path))SafePaths.canonical(path,true,false);return path;
 }
 synchronized void readSource(String file,Object active)throws IOException{Path path=saveMetadata(file);if(path!=null)sourceFiles.put(active,path);}
 synchronized String writePath(String file,Object active,String id)throws Exception{
  if(!"currentGame".equals(id))return file;Path original=sourceFiles.get(active);if(original==null)return file;
  Path actual=saveMetadata(file);if(actual==null||!original.equals(actual))return file;
  Path root=environment.choices.resolve("save-lists");Files.createDirectories(root);SafePaths.canonical(root,true,true);
  StringBuilder key=new StringBuilder();for(byte b:MessageDigest.getInstance("SHA-256").digest(original.toString().getBytes(StandardCharsets.UTF_8)))key.append(String.format("%02x",b));
  Path output=root.resolve(key+".txt");if(Files.exists(output))SafePaths.canonical(output,true,false);return output.toString();
 }
}
