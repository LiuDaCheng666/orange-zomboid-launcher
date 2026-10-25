package zombie.core.opengl;
public final class RenderThread {
 private static boolean waitForRenderState=true;
 public static void flushInvokeQueue(){}
 public static void setWaitForRenderState(boolean value){waitForRenderState=value;}
 public static boolean isWaitForRenderState(){return waitForRenderState;}
}
