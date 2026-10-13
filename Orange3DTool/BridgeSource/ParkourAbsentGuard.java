package orange.ads;

import java.util.Map;
import java.util.WeakHashMap;

/** Process-local prerequisite check; does not initialize parkour classes. */
public final class ParkourAbsentGuard {
 private static final String[] CLASSES = {
  "com.zomboidparkour.Slide", "com.zomboidparkour.LedgeGrab", "com.zomboidparkour.Ladder",
  "com.zomboidparkour.WallRun", "com.zomboidparkour.WallClimb"
 };
 private static final Map<ClassLoader,Boolean> ABSENT = new WeakHashMap<>();
 private ParkourAbsentGuard() {}
 public static boolean shouldSkip(ClassLoader loader) {
  synchronized (ABSENT) {
   Boolean cached = ABSENT.get(loader);
   if (cached != null) return cached;
   boolean absent = allAbsent(loader);
   ABSENT.put(loader, absent);
   return absent;
  }
 }
 private static boolean allAbsent(ClassLoader loader) {
  for (String name : CLASSES) {
   try {
    Class.forName(name, false, loader);
    return false;
   } catch (ClassNotFoundException missing) {
    // A wrapped loader/dependency failure is not proof the requested class is absent.
    if (missing.getCause() != null) return false;
   } catch (LinkageError | RuntimeException uncertain) {
    return false;
   }
  }
  return true;
 }
}
