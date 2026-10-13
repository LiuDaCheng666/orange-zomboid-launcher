package orange.ads;
import java.lang.reflect.*;
import java.nio.file.*;
public final class AddonMetadataProbe {
 public static void main(String[] args)throws Exception{
  Class<?> info=Class.forName("me.zed_0xff.zombie_buddy.JavaModInfo");Method parse=info.getDeclaredMethod("parse",Path.class);parse.setAccessible(true);Object result=parse.invoke(null,Path.of(args[0]));if(result==null)throw new IllegalStateException("Buddy rejected addon metadata");Method jar=info.getDeclaredMethod("jarPath");jar.setAccessible(true);Path path=(Path)jar.invoke(result);if(!Files.isRegularFile(path))throw new IllegalStateException("metadata points to missing JAR: "+path);System.out.println("PASS: actual Buddy parsed addon metadata and resolved "+path);
  if(!AddonUpdate.class.isAnnotationPresent(me.zed_0xff.zombie_buddy.Patch.class)||!AddonRender.class.isAnnotationPresent(me.zed_0xff.zombie_buddy.Patch.class))throw new IllegalStateException("patch annotations absent");System.out.println("PASS: addon patch annotations present; no game started and no approval changed.");
 }
}
