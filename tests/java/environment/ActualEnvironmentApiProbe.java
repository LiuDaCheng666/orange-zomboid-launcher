package ozl.environment.tests;
import java.net.*;
import java.nio.file.*;
import java.util.List;
import java.util.ArrayList;
import java.util.Arrays;
/** Calls real mod-list APIs in an owned fixture; no game entry point, UI or Steam. */
public final class ActualEnvironmentApiProbe {
 public static void main(String[] args)throws Exception{
  try(URLClassLoader loader=new URLClassLoader(new URL[]{Paths.get(args[0]).toUri().toURL()},ActualEnvironmentApiProbe.class.getClassLoader())){
   Class<?> type=Class.forName("zombie.ZomboidFileSystem",false,loader);
   if(type.getDeclaredMethod("getAllModFolders",List.class).getReturnType()!=void.class)throw new AssertionError("Unsupported actual discovery API");
   type.getDeclaredMethod("readDefaultModsTxt");type.getDeclaredMethod("readLoadedDotTxt");type.getDeclaredMethod("saveModsFile");
   Class<?> active=Class.forName("zombie.modding.ActiveMods",true,loader);Object save=active.getConstructor(String.class).newInstance("ozl-isolated-api-probe");
   @SuppressWarnings("unchecked") ArrayList<String> mods=(ArrayList<String>)active.getMethod("getMods").invoke(save);
   mods.addAll(Arrays.asList("Keep","OldMod","SkippedMod","NewMod"));Object same=active.getMethod("getMods").invoke(save);
   if(same!=mods||!mods.equals(Arrays.asList("Keep","NewMod")))throw new AssertionError("Actual save ActiveMods did not apply explicit choices in place");
   Path saveFile=Paths.get(args[0]).getParent().getParent().resolve("isolated-save-mods.txt");
   Files.write(saveFile,"VERSION = 1,\nmods\n{\n mod = Keep,\n mod = OldMod,\n mod = SkippedMod,\n mod = NewMod,\n}\nmaps\n{\n}\n".getBytes(java.nio.charset.StandardCharsets.UTF_8));byte[] original=Files.readAllBytes(saveFile);
   Class<?> format=Class.forName("zombie.modding.ActiveModsFile",true,loader);Object file=format.getConstructor().newInstance();
   if(!Boolean.TRUE.equals(format.getMethod("read",String.class,active).invoke(file,saveFile.toString(),save)))throw new AssertionError("Actual saved mod list could not be parsed");
   if(active.getMethod("getMods").invoke(save)!=mods||!mods.equals(Arrays.asList("Keep","NewMod"))||!Arrays.equals(original,Files.readAllBytes(saveFile)))throw new AssertionError("Actual save-file parser did not apply choices or changed source bytes");
   mods.add("OrangeLauncherSession");if(active.getMethod("getMods").invoke(save)!=mods||!mods.equals(Arrays.asList("Keep","NewMod","OrangeLauncherSession")))throw new AssertionError("Actual ActiveMods lost idempotence or mutable bridge insertion");
   Class<?> mod=Class.forName("zombie.gameStates.ChooseGameInfo$Mod",true,loader);Object dependent=mod.getConstructor(String.class).newInstance(Paths.get(args[0]).getParent().toString());
   @SuppressWarnings("unchecked") ArrayList<String> requires=(ArrayList<String>)mod.getMethod("getRequire").invoke(dependent);
   if(requires==null){requires=new ArrayList<String>();mod.getMethod("setRequire",ArrayList.class).invoke(dependent,requires);}requires.addAll(Arrays.asList("OldMod","SkippedMod","Keep","NewMod"));
   if(mod.getMethod("getRequire").invoke(dependent)!=requires||!requires.equals(Arrays.asList("NewMod","Keep")))throw new AssertionError("Actual dependency getter did not apply explicit choices in place");
   ArrayList<String> parsed=new ArrayList<String>(Arrays.asList(" OldMod "," SkippedMod ","Keep","NewMod"));mod.getMethod("setRequire",ArrayList.class).invoke(dependent,parsed);java.lang.reflect.Field field=mod.getDeclaredField("require");field.setAccessible(true);
   if(field.get(dependent)!=parsed||!parsed.equals(Arrays.asList("NewMod","Keep")))throw new AssertionError("Actual parsed require setter leaves old IDs for direct-field availability checks");
   System.out.println("PASS actual installed discovery, saved ActiveMods parser and dependency APIs without game/UI/Steam entry point");
  }
 }
}
