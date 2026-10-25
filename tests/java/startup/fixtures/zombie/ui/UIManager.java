package zombie.ui;
import zombie.core.SpriteRenderer;
public final class UIManager {public static boolean useUiFbo=true;public static void render(){SpriteRenderer.instance.queue.add("controls");}}
