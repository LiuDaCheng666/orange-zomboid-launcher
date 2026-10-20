import java.lang.reflect.*;
import java.nio.file.*;
import java.util.*;
import java.util.function.Function;

/** Calls only the installed game's text parser; never opens a game window or joins a server. */
public final class NativeTranslationProbe {
 public static void main(String[] args) throws Exception {
  Path user=Path.of(args[0]).toAbsolutePath().normalize();
  if(!user.toString().contains("artifacts"))throw new IllegalArgumentException("isolated test directory required");
  System.setProperty("user.home",user.toString());
  Class<?> language=Class.forName("zombie.core.Language");
  Constructor<?> constructor=language.getDeclaredConstructor(String.class,String.class,String.class,boolean.class);constructor.setAccessible(true);
  Object cn=constructor.newInstance("CN","Chinese","EN",false);
  Class<?> translator=Class.forName("zombie.core.Translator");
  Method parse=translator.getDeclaredMethod("tryFillMapFromFile",String.class,String.class,Map.class,language,Function.class);parse.setAccessible(true);
  Class<?> fsType=Class.forName("zombie.ZomboidFileSystem"),folderType=Class.forName("zombie.ZomboidFileSystem$PZFolder");
  Constructor<?> fsConstructor=fsType.getDeclaredConstructor();fsConstructor.setAccessible(true);Object fs=fsConstructor.newInstance();
  Method valid=fsType.getDeclaredMethod("isValidTranslationModSubDir",folderType);valid.setAccessible(true);
  Map<String,String> map=new HashMap<>();
  for(String id:List.of("B42ModTrans_CN","B42Trans_CN"))for(String part:List.of("common","42")){
   Path root=user.resolve("mods/OZLServerTranslations_"+id+"/"+part);
   if(!Files.isRegularFile(root.resolve("media/lua/shared/Translate/CN/UI.json")))throw new AssertionError("C# generated file missing");
   Object folder=folderType.getConstructor().newInstance();folderType.getMethod("set",java.io.File.class).invoke(folder,root.toFile());
   if(!(Boolean)valid.invoke(fs,folder))throw new AssertionError("game rejected generated mod as client-only translation: "+root);
   parse.invoke(null,root.toString(),"UI",map,cn,Function.identity());
  }
  for(String id:List.of("B42ModTrans_CN","B42Trans_CN"))for(String part:List.of("common","version"))if(!"kept".equals(map.get(id+"_"+part)))throw new AssertionError("native parser lost disjoint key "+id+"_"+part);
  if(!"B42Trans_CN_version".equals(map.get("overlap")))throw new AssertionError("native overlap precedence changed: "+map);
  System.out.println("PASS: installed game accepts generated text-only mod folders and merges all four common/version dictionaries, preserving disjoint keys and last nonempty overlap");
 }
}
