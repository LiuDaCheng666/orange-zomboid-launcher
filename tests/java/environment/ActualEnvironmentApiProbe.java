package ozl.environment.tests;
import java.net.*;
import java.nio.file.*;
import java.util.List;
/** Loads only class metadata; no game entry point, static initialization or Steam. */
public final class ActualEnvironmentApiProbe {
 public static void main(String[] args)throws Exception{
  try(URLClassLoader loader=new URLClassLoader(new URL[]{Paths.get(args[0]).toUri().toURL()},ActualEnvironmentApiProbe.class.getClassLoader())){
   Class<?> type=Class.forName("zombie.ZomboidFileSystem",false,loader);
   if(type.getDeclaredMethod("getAllModFolders",List.class).getReturnType()!=void.class)throw new AssertionError("Unsupported actual discovery API");
   type.getDeclaredMethod("readDefaultModsTxt");type.getDeclaredMethod("readLoadedDotTxt");type.getDeclaredMethod("saveModsFile");
   System.out.println("PASS actual installed ZomboidFileSystem transformation/API without game initialization");
  }
 }
}
