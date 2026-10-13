package orange.ads;
import java.nio.file.*;
import java.security.*;
import java.util.*;
public final class Preflight {
 public static void main(String[] args)throws Exception{
  Path author=Path.of(args[0]).toRealPath();if(!Files.isRegularFile(author)||Files.size(author)==0)throw new IllegalStateException("missing author JAR");
  Class<?> ads=Class.forName("iseethem.ADS",false,ClassLoader.getSystemClassLoader());
  ads.getMethod("configure",boolean.class,double.class,double.class,double.class);ads.getMethod("configureAimStyle",int.class,double.class);ads.getMethod("configureModes",boolean.class,double.class,int.class,int.class);ads.getMethod("configureInput",boolean.class,boolean.class);ads.getMethod("configureView",boolean.class);ads.getMethod("configureScope",boolean.class,int.class,int.class,boolean.class);ads.getMethod("configureHipLens",boolean.class);ads.getMethod("tick");
  Class<?> core=Class.forName("zombie.core.Core",false,ClassLoader.getSystemClassLoader());core.getMethod("addKeyBinding",String.class,int.class,int.class,boolean.class,boolean.class,boolean.class);core.getMethod("getKey",String.class);
  ads.getMethod("reset");ads.getMethod("showOriginalReticle",boolean.class);ads.getMethod("showMuzzleMarker",boolean.class);core.getField("currentTextEntryBox");Class.forName("zombie.ui.UITextEntryInterface",false,ClassLoader.getSystemClassLoader()).getMethod("isDoingTextEntry");Class.forName("zombie.input.GameKeyboard",false,ClassLoader.getSystemClassLoader()).getMethod("isKeyDownRaw",int.class);Class.forName("zombie.ui.UIManager",false,ClassLoader.getSystemClassLoader()).getMethod("render");Class<?> tm=Class.forName("zombie.ui.TextManager",false,ClassLoader.getSystemClassLoader());tm.getField("instance");tm.getMethod("DrawString",double.class,double.class,String.class,double.class,double.class,double.class,double.class);
  Class<?> fs=Class.forName("zombie.ZomboidFileSystem",false,ClassLoader.getSystemClassLoader());fs.getMethod("getModIDs");fs.getField("activeFileMap");
  Class.forName("zombie.gameStates.IngameState",false,ClassLoader.getSystemClassLoader()).getMethod("update");
  Path ui=author.getParent().getParent().getParent().resolve("ui/ISeeThem").toRealPath();long count;try(var files=Files.walk(ui)){count=files.filter(Files::isRegularFile).filter(p->p.toString().toLowerCase(Locale.ROOT).endsWith(".png")).count();}if(count==0)throw new IllegalStateException("no original UI assets");
  System.out.println("PASS: current author/game public API signatures; original UI PNG files="+count+". No game initialized, textures rendered or server joined. API compatibility is not rendering compatibility.");
 }
}
