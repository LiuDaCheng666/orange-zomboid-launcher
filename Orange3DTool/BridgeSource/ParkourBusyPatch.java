package orange.ads;

import java.lang.reflect.Method;
import java.lang.reflect.Modifier;
import me.zed_0xff.zombie_buddy.Patch;
import net.bytebuddy.asm.Advice;

@Patch(className="iseethem.ParkourCompat", methodName="busy")
public final class ParkourBusyPatch {
 // Buddy retains native ByteBuddy parameter annotations while converting OnEnter.
 // Inspect the actual matched method because Buddy can match overloads by name.
 @Patch.OnEnter(skipOn=true)
 public static boolean enter(@Advice.Origin Method method, @Patch.AllArguments Object[] arguments) {
  try {
   int modifiers = method.getModifiers();
   return Modifier.isPublic(modifiers) && Modifier.isStatic(modifiers)
    && method.getReturnType() == boolean.class && method.getParameterCount() == 0
    && ParkourAbsentGuard.shouldSkip(method.getDeclaringClass().getClassLoader());
  } catch (LinkageError | RuntimeException uncertain) {
   return false;
  }
 }
}
