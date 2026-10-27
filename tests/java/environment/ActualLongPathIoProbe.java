package ozl.environment.tests;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.util.jar.JarFile;
public final class ActualLongPathIoProbe {
 public static void main(String[] args)throws Exception {
  Path game=Paths.get(args[0]),payload=Paths.get(args[1]);
  if(payload.toString().length()<=260||!"copied long-path payload".equals(new String(Files.readAllBytes(payload),StandardCharsets.UTF_8)))throw new AssertionError("JVM did not read exact copied payload beyond MAX_PATH");
  if(!Paths.get(System.getProperty("user.dir")).equals(game))throw new AssertionError("JVM working directory differs from the selected game root");
  try(JarFile jar=new JarFile(game.resolve("projectzomboid.jar").toFile())){if(jar.getEntry("zombie/modding/ActiveMods.class")==null)throw new AssertionError("Copied actual game JAR is unavailable");}
  System.out.println("PASS actual copied JRE reads "+payload.toString().length()+"-character snapshot child and actual game JAR with the selected working directory");
 }
}
