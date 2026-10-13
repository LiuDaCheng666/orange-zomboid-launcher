package orange.ads;
import me.zed_0xff.zombie_buddy.Patch;
@Patch(className="zombie.ui.UIManager",methodName="render")
public final class AddonRender {
 @Patch.OnExit(suppress=Throwable.class) public static void exit(){RuntimeBridge.renderNotice();}
}
