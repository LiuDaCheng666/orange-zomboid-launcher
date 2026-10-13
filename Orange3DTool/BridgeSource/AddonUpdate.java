package orange.ads;
import me.zed_0xff.zombie_buddy.Patch;
@Patch(className="zombie.gameStates.IngameState",methodName="update")
public final class AddonUpdate {
 @Patch.OnExit(suppress=Throwable.class) public static void exit(){RuntimeBridge.afterUpdate();}
}
