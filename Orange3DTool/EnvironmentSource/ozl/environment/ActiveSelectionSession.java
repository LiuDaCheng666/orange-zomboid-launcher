package ozl.environment;
import java.io.*;import java.nio.file.*;import java.nio.charset.StandardCharsets;import java.security.MessageDigest;import java.util.*;
/** Applies choices after parsing and before currentGame raw-field bookkeeping. */
class ActiveSelectionSession {
 private final Path user,choices;private final boolean override;private final java.util.function.BiConsumer<List<String>,List<String>> normalize;
 private List<String> defaults;
 private final Map<Object,Boolean> ownedReads=new WeakHashMap<Object,Boolean>();
 private final Map<Object,Path> sourceFiles=new WeakHashMap<Object,Path>();
 ActiveSelectionSession(Path user,Path choices,boolean override,java.util.function.BiConsumer<List<String>,List<String>> normalize){this.user=user;this.choices=choices;this.override=override;this.normalize=normalize;}
 synchronized void defaultLoaded(List<String> ids){normalize.accept(ids,null);defaults=ids;}
 synchronized void copied(Object target,Object source,String id,List<String> ids,boolean client){
  if(client||!"currentGame".equals(id))return;
  if(override&&defaults==null)throw new IllegalStateException("owned default has not completed reading");
  normalize.accept(ids,override&&!sourceFiles.containsKey(source)?new ArrayList<String>(defaults):null);
  Path original=sourceFiles.get(source);if(original==null)sourceFiles.remove(target);else sourceFiles.put(target,original);
 }
 synchronized void loading(String id,List<String> ids,boolean client){if(!client&&"currentGame".equals(id))normalize.accept(ids,null);}
 private Path saveMetadata(String value)throws IOException{
  Path path=Paths.get(value).toAbsolutePath().normalize(),root=user.resolve("Saves");
  if(!path.startsWith(root)||root.relativize(path).getNameCount()!=3||!path.getFileName().toString().equals("mods.txt"))return null;
  SafePaths.canonical(path.getParent(),true,true);if(Files.exists(path))SafePaths.canonical(path,true,false);return path;
 }
 synchronized void readSource(String file,Object active)throws IOException{readPath(file,active);}
 synchronized String readPath(String file,Object active)throws IOException{
  sourceFiles.remove(active);ownedReads.remove(active);Path path=saveMetadata(file);if(path==null)return file;
  sourceFiles.put(active,path);Path output=output(path,false);boolean exists=Files.exists(output);ownedReads.put(active,exists);
  if(exists){SafePaths.canonical(output,true,false);return output.toString();}return file;
 }
 synchronized void readCompleted(Object active,List<String> ids,boolean success,boolean client){
  if(!success||client||!sourceFiles.containsKey(active))return;
  boolean original=!Boolean.TRUE.equals(ownedReads.get(active));
  if(override&&original&&defaults==null)throw new IllegalStateException("owned default has not completed reading");
  normalize.accept(ids,override&&original?new ArrayList<String>(defaults):null);
 }
 private Path output(Path original,boolean create)throws IOException{
  Path root=choices.resolve("save-lists");if(create)Files.createDirectories(root);SafePaths.canonical(root,create,true);
  try{StringBuilder key=new StringBuilder();for(byte b:MessageDigest.getInstance("SHA-256").digest(original.toString().getBytes(StandardCharsets.UTF_8)))key.append(String.format("%02x",b));return root.resolve(key+".txt");}catch(java.security.NoSuchAlgorithmException impossible){throw new IllegalStateException(impossible);}
 }
 synchronized String writePath(String file,Object active,String id)throws Exception{
  if(!"currentGame".equals(id))return file;Path original=sourceFiles.get(active);if(original==null)return file;
  Path actual=saveMetadata(file);if(actual==null||!original.equals(actual))return file;
  Path output=output(original,true);if(Files.exists(output))SafePaths.canonical(output,true,false);return output.toString();
 }
}
