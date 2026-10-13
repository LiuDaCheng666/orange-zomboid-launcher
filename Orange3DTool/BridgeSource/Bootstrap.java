package orange.ads;
import java.lang.instrument.Instrumentation;
import java.nio.file.*;
import java.util.jar.JarFile;
public final class Bootstrap {
 public static void premain(String args,Instrumentation inst){try{Path self=Path.of(Bootstrap.class.getProtectionDomain().getCodeSource().getLocation().toURI());Path buddy=self.getParent().resolve("ZombieBuddy.jar");if(!Files.isRegularFile(buddy))throw new IllegalStateException("Buddy loader missing next to owned bridge");inst.appendToSystemClassLoaderSearch(new JarFile(buddy.toFile()));Hook.install(inst);System.out.println("[ORANGE-ADS] update hook installed; presentation not yet verified");}catch(Throwable e){System.out.println("[ORANGE-ADS] disabled: "+e);}}
}
